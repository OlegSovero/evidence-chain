import { NavLink, Outlet } from 'react-router-dom';
import { UserSwitcher } from './auth/UserSwitcher';

export function Layout() {
  return (
    <div className="app">
      <header className="app__header">
        <h1 className="app__title">Evidence Chain</h1>
        <nav className="app__nav" aria-label="Principal">
          <NavLink to="/evidencias" className={({ isActive }) => (isActive ? 'active' : undefined)}>
            Evidencias
          </NavLink>
          <NavLink to="/transferencias" className={({ isActive }) => (isActive ? 'active' : undefined)}>
            Transferencias
          </NavLink>
        </nav>
        <UserSwitcher />
      </header>
      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
