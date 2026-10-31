import { NavLink, Outlet, useNavigate } from 'react-router-dom'

// Catatan: layout sidebar lama, saat ini tidak dipakai (AppRoutes memakai MainLayout).
export default function Layout() {
  const navigate = useNavigate()
  const auth = JSON.parse(localStorage.getItem('auth') || '{}')

  function logout() {
    localStorage.clear()
    navigate('/login')
  }

  return (
    <div className="app">
      <aside>
        <h2>HelpDesk-ASD</h2>
        <small>
          {auth.name || 'User'}
          <br />
          {auth.group || ''}
        </small>

        <nav>
          <NavLink to="/">Dashboard</NavLink>
          <NavLink to="/utility/groups">Master Group</NavLink>
          <NavLink to="/utility/users">Master User</NavLink>
          <NavLink to="/utility/security">Master Security</NavLink>
          <NavLink to="/change-password">Ganti Password</NavLink>
        </nav>

        <button className="secondary" onClick={logout}>
          Logout
        </button>
      </aside>

      <main>
        <Outlet />
      </main>
    </div>
  )
}
