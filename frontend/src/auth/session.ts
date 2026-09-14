import type { DemoUser } from '../api/types';

// sessionStorage (no localStorage): el token no debe sobrevivir a cerrar la pestaña,
// y cambiar de usuario demo en una pestaña no debe afectar a otra.
const TOKEN_KEY = 'evidence-chain.token';
const USER_KEY = 'evidence-chain.user';

export interface Session {
  token: string;
  user: DemoUser;
}

export function readSession(): Session | null {
  try {
    const token = sessionStorage.getItem(TOKEN_KEY);
    const rawUser = sessionStorage.getItem(USER_KEY);
    if (!token || !rawUser) {
      return null;
    }
    return { token, user: JSON.parse(rawUser) as DemoUser };
  } catch {
    return null;
  }
}

export function writeSession(session: Session): void {
  sessionStorage.setItem(TOKEN_KEY, session.token);
  sessionStorage.setItem(USER_KEY, JSON.stringify(session.user));
}

export function clearSession(): void {
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(USER_KEY);
}

export function getToken(): string | null {
  return readSession()?.token ?? null;
}
