import { useEffect, useMemo, useState } from 'react'
import { NavLink, useLocation, useNavigate } from 'react-router-dom'

import { getNavigationMenus } from '../../features/menus/api/menuApi'
import type { NavigationMenuItem } from '../../features/menus/types/menu'
import OnlineUsers from '../../features/presence/components/OnlineUsers'

type AuthInfo = {
  name?: string
  group?: string
}

function getAuthInfo(): AuthInfo {
  try {
    return JSON.parse(localStorage.getItem('auth') ?? '{}')
  } catch {
    return {}
  }
}

// LIVE_CHAT punya tombol sendiri; SNS_MOBILE hanya hak kirim data dari Android (tidak punya halaman web).
// Halaman IoT Vendor/Internal sudah digabung; item Internal lama tidak lagi menjadi shortcut terpisah.
const HIDDEN_NAVBAR_MENUS = new Set([
  'LIVE_CHAT',
  'SNS_MOBILE',
  'PORTAL',
  'PRAKIRAAN_CUACA',
  'PRAKIRAAN-CUACA',
  'NOTIFIKASI_PERINGATAN',
  'DAFTAR_PERANGKAT_INTERNAL',
  'MONITORING_SENSOR',
  'IOT_VENDOR',
  'IOT_INTERNAL',
  'LOGGER_LOKAL',
  'LOCAL_LOGGER',
])

const UNIFIED_IOT_MENU_OVERRIDES: Record<string, Partial<Pick<NavigationMenuItem, 'name' | 'route'>>> = {
  MANAGEMEN_IOT: {
    name: 'Manajemen IoT',
    route: '/iot-management',
  },
  IOT_MANAGEMENT: {
    name: 'Manajemen IoT',
    route: '/iot-management',
  },
  DASHBOARD_IOT: {
    name: 'Dashboard',
    route: '/iot-management/dashboard-iot',
  },
  PETA_MONITORING: {
    name: 'Peta Monitoring',
    route: '/iot-management/peta-monitoring',
  },
  'PETA-MONITORING': {
    name: 'Peta Monitoring',
    route: '/iot-management/peta-monitoring',
  },
  DAFTAR_PERANGKAT_VENDOR: {
    name: 'Manajemen Perangkat',
    route: '/iot-management/daftar-perangkat',
  },
  DAFTAR_PERANGKAT: {
    name: 'Manajemen Perangkat',
    route: '/iot-management/daftar-perangkat',
  },
  MONITORING_SENSOR_VENDOR: {
    name: 'Monitoring Sensor',
    route: '/iot-management/monitoring-sensor',
  },
  RIWAYAT_DATA_SENSOR: {
    name: 'Riwayat Data Sensor',
    route: '/iot-management/riwayat-data-sensor',
  },
}

function normalizeNavigationMenus(data: NavigationMenuItem[]) {
  return data
    .filter(
      menu =>
        !HIDDEN_NAVBAR_MENUS.has(menu.code) &&
        menu.menuType !== 'WIDGET' &&
        menu.route !== '/iot-management/logger-lokal',
    )
    .map(menu => {
      const override = UNIFIED_IOT_MENU_OVERRIDES[menu.code]
      return override ? { ...menu, ...override } : menu
    })
}

const bySortOrder =(a: NavigationMenuItem, b: NavigationMenuItem) => a.sortOrder - b.sortOrder

export default function TopNavbar() {
  const navigate = useNavigate()
  const location = useLocation()

  const [menus, setMenus] = useState<NavigationMenuItem[]>([])
  const [openMenu, setOpenMenu] = useState<number | null>(null)
  const [userOpen, setUserOpen] = useState(false)

  const auth = getAuthInfo()

  /* ---------- Load menu navigasi sesuai hak akses ---------- */

  useEffect(() => {
    getNavigationMenus()
      // Live Chat tidak tampil di navbar; aksesnya lewat tombol melayang di pojok kanan bawah.
      // Menu LIVE_CHAT tetap ada di Manajemen Menu & Hak Akses untuk mengatur siapa yang boleh memakai.
      // Widget portal (dan induknya PORTAL) tampil di halaman utama, bukan di navbar.
      .then(data => setMenus(normalizeNavigationMenus(data)))
      .catch(error => console.error('Gagal mengambil navigation menu:', error))
  }, [])

  const rootMenus = useMemo(() => menus.filter(menu => menu.parentId == null).sort(bySortOrder), [menus])

  function getChildren(parentId: number) {
    return menus.filter(menu => menu.parentId === parentId).sort(bySortOrder)
  }

  // Group ditandai aktif jika salah satu child-nya sedang dibuka.
  function groupIsActive(parent: NavigationMenuItem) {
    return getChildren(parent.id).some(
      child =>
        child.route &&
        (location.pathname === child.route || location.pathname.startsWith(`${child.route}/`)),
    )
  }

  function logout() {
    localStorage.removeItem('token')
    localStorage.removeItem('auth')
    navigate('/login')
  }

  /* ---------- Submenu (rekursif, mendukung banyak level) ---------- */

  function renderSubMenu(parentId: number) {
    return getChildren(parentId).map(child => {
      // Menu yang masih punya child -> tampilkan submenu bertingkat.
      if (getChildren(child.id).length > 0) {
        return (
          <div key={child.id} className="submenu-item-wrapper">
            <div className="dropdown-item submenu-parent">
              <span>{child.name}</span>
              <span className="submenu-arrow">›</span>
            </div>
            <div className="submenu-dropdown">{renderSubMenu(child.id)}</div>
          </div>
        )
      }

      // Group kosong (semua child-nya dipindah / tidak ada akses) tidak ditampilkan.
      if (child.menuType === 'GROUP' && !child.route) return null

      // Menu leaf yang punya route -> link.
      if (child.route) {
        return (
          <NavLink
            key={child.id}
            to={child.route}
            className={({ isActive }) => (isActive ? 'dropdown-item active' : 'dropdown-item')}
            onClick={() => setOpenMenu(null)}
          >
            {child.name}
          </NavLink>
        )
      }

      // Menu leaf tanpa route tetap ditampilkan sebagai label.
      return (
        <div key={child.id} className="dropdown-item submenu-label">
          {child.name}
        </div>
      )
    })
  }

  /* ---------- Render ---------- */

  return (
    <header className="top-navbar">
      <div className="top-navbar-inner">
        <div className="navbar-brand">HelpDesk-ASD</div>

        <nav className="navbar-menu">
          {rootMenus.map(menu => {
            const hasDropdown = menu.menuType === 'GROUP' || getChildren(menu.id).length > 0

            // Menu group -> tombol dropdown.
            if (hasDropdown) {
              if (getChildren(menu.id).length === 0) return null

              return (
                <div
                  key={menu.id}
                  className="nav-dropdown"
                  onMouseEnter={() => setOpenMenu(menu.id)}
                  onMouseLeave={() => setOpenMenu(null)}
                >
                  <button
                    type="button"
                    className={groupIsActive(menu) ? 'top-menu dropdown-button active' : 'top-menu dropdown-button'}
                    onClick={() => setOpenMenu(openMenu === menu.id ? null : menu.id)}
                  >
                    {menu.name}
                    <span className="dropdown-arrow">▾</span>
                  </button>

                  {openMenu === menu.id && <div className="dropdown-menu">{renderSubMenu(menu.id)}</div>}
                </div>
              )
            }

            // Menu langsung (contoh: Dashboard).
            if (!menu.route) return null

            return (
              <NavLink
                key={menu.id}
                to={menu.route}
                className={({ isActive }) => (isActive ? 'top-menu active' : 'top-menu')}
              >
                {menu.name}
              </NavLink>
            )
          })}
        </nav>

        <OnlineUsers />

        {/* Menu user (ganti password / logout) */}
        <div
          className="navbar-user-wrapper"
          onMouseEnter={() => setUserOpen(true)}
          onMouseLeave={() => setUserOpen(false)}
        >
          <button type="button" className="navbar-user-button" onClick={() => setUserOpen(!userOpen)}>
            <div className="user-info">
              <span className="user-name">{auth.name ?? 'User'}</span>
              <span className="user-group">{auth.group ?? ''}</span>
            </div>
            <span>▾</span>
          </button>

          {userOpen && (
            <div className="user-dropdown">
              <NavLink to="/change-password" className="dropdown-item">
                Ganti Password
              </NavLink>
              <div className="dropdown-divider" />
              <button type="button" className="dropdown-item logout-item" onClick={logout}>
                Logout
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
