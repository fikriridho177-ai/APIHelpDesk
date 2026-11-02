import { Outlet, useLocation } from 'react-router-dom'

import TopNavbar from './TopNavbar'
import AppBreadcrumb from './AppBreadcrumb'
import AppFooter from './AppFooter'
import FloatingChat from '../../features/chat/components/FloatingChat'

export default function MainLayout() {
  const location = useLocation()
  const isWadOverview = location.pathname === '/ecs'
  const isSensorDetail = location.pathname === '/ecs/sensor-detail'
  const layoutClass = [
    'app-layout',
    isWadOverview ? 'app-layout-overview' : '',
    isSensorDetail ? 'app-layout-sensor-detail' : '',
  ].filter(Boolean).join(' ')

  return (
    <div className={layoutClass}>
      <TopNavbar />

      <main className="main-content">
        <AppBreadcrumb />
        <Outlet />
      </main>

      <AppFooter />

      {/* Tombol Live Chat melayang di pojok kanan bawah (semua halaman) */}
      <FloatingChat />
    </div>
  )
}
