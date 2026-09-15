import { afterEach, describe, expect, it } from 'vitest';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../test/server';
import { API } from '../test/handlers';
import { AuthProvider } from './AuthContext';
import { useAuth } from './useAuth';
import { clearSession, writeSession } from './session';
import { apiFetch } from '../api/client';

function Probe() {
  const { user } = useAuth();
  useQuery({
    queryKey: ['probe'],
    queryFn: () => apiFetch('/api/v1/evidence'),
  });
  return <span>{user ? `logueado:${user.userName}` : 'sin-sesion'}</span>;
}

function renderProbe() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <Probe />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  cleanup();
  clearSession();
});

// Requisito implícito del enunciado (autorización validada en servidor): si
// el servidor deja de aceptar el token (vencido o inválido), la UI no debe
// quedarse con páginas mostrando "no autorizado" y el usuario todavía
// marcado como activo en el selector - debe volver al estado sin sesión.
describe('AuthContext', () => {
  it('cierra sesión y vuelve al selector cuando una petición autenticada recibe 401', async () => {
    writeSession({
      token: 'token-vencido',
      user: { id: 1, userName: 'ana.investigadora', displayName: 'Ana Investigadora', role: 'Investigador' },
    });

    server.use(
      http.get(`${API}/api/v1/evidence`, () =>
        HttpResponse.json(
          { type: 'about:blank', title: 'No autorizado', status: 401, detail: 'Token inválido o vencido.' },
          { status: 401 },
        ),
      ),
    );

    renderProbe();

    expect(screen.getByText('logueado:ana.investigadora')).toBeInTheDocument();

    await waitFor(() => expect(screen.getByText('sin-sesion')).toBeInTheDocument());
  });

  it('un 401 sin sesión activa no dispara el evento de logout', async () => {
    server.use(
      http.get(`${API}/api/v1/evidence`, () =>
        HttpResponse.json(
          { type: 'about:blank', title: 'No autorizado', status: 401, detail: 'Falta autenticación.' },
          { status: 401 },
        ),
      ),
    );

    renderProbe();

    expect(screen.getByText('sin-sesion')).toBeInTheDocument();
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(screen.getByText('sin-sesion')).toBeInTheDocument();
  });
});
