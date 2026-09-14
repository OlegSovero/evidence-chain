import { createContext, useCallback, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { issueToken } from '../api/auth';
import type { DemoUser } from '../api/types';
import { clearSession, readSession, writeSession, type Session } from './session';

export interface AuthContextValue {
  user: DemoUser | null;
  isSwitching: boolean;
  error: string | null;
  login: (userName: string) => Promise<void>;
  logout: () => void;
}

// eslint-disable-next-line react-refresh/only-export-components
export const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => readSession());
  const [isSwitching, setIsSwitching] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const login = useCallback(
    async (userName: string) => {
      setIsSwitching(true);
      setError(null);
      try {
        const response = await issueToken(userName);
        const next: Session = { token: response.accessToken, user: response.user };
        writeSession(next);
        setSession(next);
        // Todo lo cacheado (bandeja de transferencias "mine=true", detalle, etc.)
        // depende de quién está autenticado: al cambiar de usuario se descarta.
        queryClient.clear();
      } catch {
        setError(`No se pudo iniciar sesión como "${userName}".`);
      } finally {
        setIsSwitching(false);
      }
    },
    [queryClient],
  );

  const logout = useCallback(() => {
    clearSession();
    setSession(null);
    queryClient.clear();
  }, [queryClient]);

  const value = useMemo<AuthContextValue>(
    () => ({ user: session?.user ?? null, isSwitching, error, login, logout }),
    [session, isSwitching, error, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
