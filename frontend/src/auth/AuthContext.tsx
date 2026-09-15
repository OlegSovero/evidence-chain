import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { issueToken } from '../api/auth';
import { UNAUTHORIZED_EVENT } from '../api/client';
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
        // depende de quién está autenticado. invalidateQueries (no clear: clear()
        // vacía la caché pero no obliga a los componentes montados a refetchear)
        // marca todo como obsoleto y dispara el refetch de lo que sigue en pantalla.
        void queryClient.invalidateQueries();
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
    void queryClient.invalidateQueries();
  }, [queryClient]);

  // Ante un token vencido o inválido (ver UNAUTHORIZED_EVENT en api/client.ts),
  // cierra sesión igual que un logout manual: vuelve al selector en vez de
  // dejar cada página con su propio error de "no autorizado" y el usuario
  // todavía marcado como activo en el header.
  useEffect(() => {
    window.addEventListener(UNAUTHORIZED_EVENT, logout);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, logout);
  }, [logout]);

  const value = useMemo<AuthContextValue>(
    () => ({ user: session?.user ?? null, isSwitching, error, login, logout }),
    [session, isSwitching, error, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
