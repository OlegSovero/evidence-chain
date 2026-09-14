import { useQuery } from '@tanstack/react-query';
import { listDemoUsers } from '../api/auth';
import { useAuth } from './useAuth';

// Selector de usuario demo, siempre visible: la demo en vivo necesita poder
// alternar entre un Investigador que solicita y un Custodio que acepta en segundos.
export function UserSwitcher() {
  const { user, login, isSwitching, error } = useAuth();
  const { data, isLoading } = useQuery({
    queryKey: ['auth', 'demo-users'],
    queryFn: ({ signal }) => listDemoUsers(signal),
    staleTime: Infinity,
  });

  return (
    <div className="user-switcher">
      <label htmlFor="demo-user-select" className="user-switcher__label">
        Usuario demo
      </label>
      <select
        id="demo-user-select"
        className="user-switcher__select"
        value={user?.userName ?? ''}
        disabled={isLoading || isSwitching}
        onChange={(event) => {
          if (event.target.value) {
            void login(event.target.value);
          }
        }}
      >
        <option value="" disabled>
          {isLoading ? 'Cargando usuarios…' : 'Elige un usuario'}
        </option>
        {data?.items.map((demoUser) => (
          <option key={demoUser.userName} value={demoUser.userName}>
            {demoUser.displayName} — {demoUser.role}
          </option>
        ))}
      </select>
      {user && <span className="user-switcher__role">Rol activo: {user.role}</span>}
      {error && (
        <span role="alert" className="user-switcher__error">
          {error}
        </span>
      )}
    </div>
  );
}
