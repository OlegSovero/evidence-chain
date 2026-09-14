import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { server } from '../../test/server';
import { API } from '../../test/handlers';
import { renderWithProviders } from '../../test/renderWithProviders';
import { InboxPage } from './InboxPage';

function evidenceItem(code: string, description: string) {
  return {
    id: code === 'EV-SLOW' ? 1 : 2,
    code,
    description,
    currentCustodian: { id: 1, userName: 'ana.custodio', displayName: 'Ana Custodio' },
    lastEventAtUtc: '2026-09-01T10:00:00.000Z',
    integrityStatus: 'Integra',
  };
}

// Requisito del enunciado: una respuesta de búsqueda lenta que llega tarde NO
// debe reemplazar el resultado del filtro actual. Simulamos que el usuario
// busca "slow" (respuesta con 600 ms de retraso) y corrige a "fast" (respuesta
// inmediata) antes de que la primera responda; la lenta llega después de que
// "fast" ya se renderizó y no debe pisar esa vista.
describe('InboxPage', () => {
  it('ignora una respuesta de búsqueda obsoleta que llega después del filtro actual', async () => {
    server.use(
      http.get(`${API}/api/v1/evidence`, async ({ request }) => {
        const url = new URL(request.url);
        const q = url.searchParams.get('q');
        if (q === 'slow') {
          await delay(600);
          return HttpResponse.json({ items: [evidenceItem('EV-SLOW', 'No debería verse')], nextCursor: null });
        }
        if (q === 'fast') {
          return HttpResponse.json({ items: [evidenceItem('EV-FAST', 'Resultado correcto')], nextCursor: null });
        }
        return HttpResponse.json({ items: [], nextCursor: null });
      }),
    );

    const user = userEvent.setup();
    renderWithProviders(<InboxPage />, { route: '/evidencias', path: '/evidencias' });

    const search = await screen.findByLabelText('Buscar');
    await user.type(search, 'slow');

    // Deja que el debounce (400 ms) dispare la búsqueda lenta antes de corregir.
    await new Promise((resolve) => setTimeout(resolve, 450));
    await user.clear(search);
    await user.type(search, 'fast');

    await waitFor(() => expect(screen.getByText('EV-FAST')).toBeInTheDocument(), { timeout: 2000 });

    // La respuesta lenta llega ~600 ms después de iniciada; esperamos ese
    // margen y confirmamos que nunca reemplazó el resultado ya mostrado.
    await new Promise((resolve) => setTimeout(resolve, 700));
    expect(screen.queryByText('EV-SLOW')).not.toBeInTheDocument();
    expect(screen.getByText('EV-FAST')).toBeInTheDocument();
  }, 10000);
});
