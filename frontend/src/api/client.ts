import { getToken } from '../auth/session';
import type { ProblemDetails } from './types';

const API_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:5059';

// AuthContext escucha este evento para cerrar sesión y volver al selector.
// Solo se dispara cuando la petición SÍ llevaba un token (sesión vencida o
// inválida) — una petición sin token que recibe 401 es un estado normal de
// "sin sesión", no una sesión que expiró, y no debe repetir el ciclo.
export const UNAUTHORIZED_EVENT = 'evidence-chain:unauthorized';

// Error tipado que conserva el status y el problem+json completo (incluido
// currentState en los 409) para que cada llamador decida cómo reaccionar.
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Error ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

export type QueryValue = string | number | boolean | undefined | null;

export interface ApiFetchOptions {
  method?: string;
  query?: Record<string, QueryValue>;
  body?: unknown;
  signal?: AbortSignal;
  headers?: Record<string, string>;
}

export interface ApiResult<T> {
  data: T;
  response: Response;
}

function buildUrl(path: string, query?: Record<string, QueryValue>): string {
  const url = new URL(path, API_URL);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        url.searchParams.set(key, String(value));
      }
    }
  }
  return url.toString();
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('json')) {
    try {
      return (await response.json()) as ProblemDetails;
    } catch {
      // cae al genérico de abajo
    }
  }
  return { title: response.statusText, status: response.status, detail: response.statusText };
}

export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<ApiResult<T>> {
  const headers: Record<string, string> = { Accept: 'application/json', ...options.headers };
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  const token = getToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(buildUrl(path, options.query), {
    method: options.method ?? 'GET',
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options.signal,
  });

  if (!response.ok) {
    const problem = await readProblem(response);
    if (response.status === 401 && token) {
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
    }
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) {
    return { data: undefined as T, response };
  }

  const contentType = response.headers.get('content-type') ?? '';
  const data = contentType.includes('json') ? ((await response.json()) as T) : (undefined as T);
  return { data, response };
}
