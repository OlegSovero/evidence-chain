import { describe, expect, it } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../test/server';
import { API } from '../../test/handlers';
import { PendingInboxPage } from './PendingInboxPage';

const TRANSFER = {
  id: '11111111-1111-1111-1111-111111111111',
  evidence: { id: 1, code: 'EV-1', description: 'Disco duro incautado' },
  status: 'Pendiente',
  reason: 'Custodia temporal para peritaje',
  requestedAtUtc: '2026-09-10T08:00:00.000Z',
  respondedAtUtc: null,
  responseNote: null,
  fromCustodian: { id: 2, userName: 'ana.investigadora', displayName: 'Ana Investigadora' },
  toCustodian: { id: 3, userName: 'luis.custodio', displayName: 'Luis Custodio' },
  requestedBy: { id: 2, userName: 'ana.investigadora', displayName: 'Ana Investigadora' },
  respondedBy: null,
  version: '"0x0000000000000001"',
  anomaly: null,
};

const CONFLICT_BODY = {
  type: 'https://evidence-chain/errors/transfer-already-resolved',
  title: 'La transferencia ya fue resuelta',
  status: 409,
  detail: 'otro.custodio ya la marcó como Aceptada el 2026-09-10T09:00:00Z.',
  currentState: {
    transferId: TRANSFER.id,
    status: 'Aceptada',
    version: '"0x0000000000000002"',
    respondedBy: 'otro.custodio',
    respondedAtUtc: '2026-09-10T09:00:00.000Z',
  },
};

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <PendingInboxPage />
    </QueryClientProvider>,
  );
}

// Requisito del enunciado: ante un 409 al responder una transferencia, la
// actualización optimista debe revertirse y el error debe explicarse; nunca
// debe quedar como confirmada una operación que el servidor rechazó.
describe('PendingInboxPage', () => {
  it('el aviso del 409 sobrevive a la reconciliación aunque el item ya no vuelva a la lista', async () => {
    // Mock con estado: un 409 de este endpoint siempre significa que la
    // transferencia ya salió de "Pendiente" (la resolvió otro custodio), así
    // que el refetch en segundo plano que dispara onSettled ya no debe
    // traerla. Un mock estático (como tenía este test antes) no reproduce
    // este caso y no detecta que el aviso desaparecía junto con el item.
    let stillPending = true;
    server.use(
      http.get(`${API}/api/v1/custody-transfers`, () =>
        HttpResponse.json({ items: stillPending ? [TRANSFER] : [] }),
      ),
    );

    let resolveAccept: (() => void) | undefined;
    server.use(
      http.post(`${API}/api/v1/custody-transfers/:id/accept`, async () => {
        await new Promise<void>((resolve) => {
          resolveAccept = resolve;
        });
        stillPending = false;
        return HttpResponse.json(CONFLICT_BODY, { status: 409 });
      }),
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByText('EV-1');

    await user.click(screen.getByRole('button', { name: 'Aceptar' }));

    // Fase optimista: la solicitud desaparece de la bandeja de inmediato,
    // antes de que el servidor haya respondido nada todavía.
    await waitFor(() => expect(screen.queryByText('EV-1')).not.toBeInTheDocument());

    // El servidor responde 409: la transferencia ya fue aceptada por otro custodio.
    resolveAccept?.();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('otro.custodio ya la marcó como Aceptada');

    // La reconciliación de fondo confirma que ya no está pendiente: la bandeja
    // queda vacía, pero el aviso del 409 debe seguir visible.
    await waitFor(() =>
      expect(screen.getByText('No tienes transferencias pendientes.')).toBeInTheDocument(),
    );
    expect(screen.getByRole('alert')).toHaveTextContent('otro.custodio ya la marcó como Aceptada');
  }, 10000);
});
