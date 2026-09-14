import { apiFetch } from './client';
import type { AuthResponse, ListDemoUsersResponse } from './types';

export function listDemoUsers(signal?: AbortSignal) {
  return apiFetch<ListDemoUsersResponse>('/api/v1/auth/users', { signal }).then((r) => r.data);
}

export function issueToken(userName: string, signal?: AbortSignal) {
  return apiFetch<AuthResponse>('/api/v1/auth/token', {
    method: 'POST',
    body: { userName },
    signal,
  }).then((r) => r.data);
}
