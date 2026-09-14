import { http, HttpResponse } from 'msw';

export const API = 'http://localhost:5059';

// Handlers por defecto: solo lo mínimo para que un componente no falle por
// "unhandled request" cuando no le importa el resultado de esa llamada. Cada
// test agrega los suyos con server.use(...) para el escenario que le interesa.
export const handlers = [
  http.get(`${API}/api/v1/auth/users`, () => HttpResponse.json({ items: [] })),
  http.get(`${API}/api/v1/evidence`, () => HttpResponse.json({ items: [], nextCursor: null })),
  http.get(`${API}/api/v1/custody-transfers`, () => HttpResponse.json({ items: [] })),
];
