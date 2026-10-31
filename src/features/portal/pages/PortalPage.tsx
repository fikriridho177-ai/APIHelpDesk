import { useEffect, useState } from 'react'

import { openLiveChat } from '../../chat/openLiveChat'
import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import RequestDetailModal from '../../layanan/components/RequestDetailModal'
import { useDepartmentNames } from '../../layanan/hooks/useLayanan'
import { getNavigationMenus } from '../../menus/api/menuApi'
import type { NavigationMenuItem } from '../../menus/types/menu'
import { WIDGETS } from '../widgetRegistry'
import '../../layanan/layanan.css'
import '../../dashboard/pages/DashboardPage.css'
import './PortalPage.css'

/* =========================================================
   HELPER
========================================================= */

function getLoggedInName() {
  try {
    const auth = JSON.parse(localStorage.getItem('auth') ?? '{}')
    return (auth.name as string | undefined)?.trim() || 'Pengguna'
  } catch {
    return 'Pengguna'
  }
}

function getGreeting(hour: number) {
  if (hour < 11) return 'Selamat pagi'
  if (hour < 15) return 'Selamat siang'
  if (hour < 18) return 'Selamat sore'
  return 'Selamat malam'
}

/* =========================================================
   HALAMAN
========================================================= */

// Halaman utama. Widget yang tampil = menu bertipe WIDGET yang boleh dilihat user
// (Hak Akses > Akses Menu, kolom View), urut SortOrder di Manajemen Menu.
export default function PortalPage() {
  const [menus, setMenus] = useState<NavigationMenuItem[]>([])
  const [loading, setLoading] = useState(true)
  const [openedId, setOpenedId] = useState<number | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const departmentNames = useDepartmentNames()

  const now = new Date()

  useEffect(() => {
    getNavigationMenus()
      .then(setMenus)
      .catch(error => console.error('Gagal mengambil menu portal:', error))
      .finally(() => setLoading(false))
  }, [])

  const hasLiveChat = menus.some(menu => menu.code === 'LIVE_CHAT')

  // Kode widget yang belum punya komponen di frontend dilewati.
  const widgets = menus
    .filter(menu => menu.menuType === 'WIDGET' && WIDGETS[menu.code])
    .sort((a, b) => a.sortOrder - b.sortOrder)

  return (
    <section className="dashboard-page">
      {/* SAMBUTAN */}
      <header className="dashboard-hero">
        <div>
          <p className="dashboard-hero-date">
            {now.toLocaleDateString('id-ID', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}
          </p>
          <h1>
            {getGreeting(now.getHours())}, {getLoggedInName()}
          </h1>
          <p className="dashboard-hero-subtitle">Selamat datang di HelpDesk-ASD. Pilih layanan di bawah untuk mulai bekerja.</p>
        </div>

        {/* Tombol Live Chat hanya tampil jika user punya akses menu LIVE_CHAT */}
        {hasLiveChat && (
          <button type="button" className="dashboard-chat-button" onClick={openLiveChat}>
            <DashboardIcon name="chat" />
            <span>
              <strong>Live Chat</strong>
              <small>Ngobrol langsung dengan tim</small>
            </span>
            <DashboardIcon name="arrow" />
          </button>
        )}
      </header>

      {/* WIDGET */}
      {loading ? (
        <div className="dashboard-empty">Memuat portal...</div>
      ) : widgets.length === 0 ? (
        <div className="dashboard-empty">Portal Anda belum diatur. Hubungi administrator.</div>
      ) : (
        <div className="portal-grid">
          {widgets.map(menu => {
            const { component: Widget, size } = WIDGETS[menu.code]
            return (
              <div key={menu.id} className={`portal-cell size-${size}`}>
                <Widget menu={menu} menus={menus} refreshKey={refreshKey} onOpenRequest={setOpenedId} />
              </div>
            )
          })}
        </div>
      )}

      {openedId !== null && (
        <RequestDetailModal
          requestId={openedId}
          departmentNames={departmentNames}
          onClose={() => setOpenedId(null)}
          onChanged={() => setRefreshKey(key => key + 1)}
        />
      )}
    </section>
  )
}
