import { Outlet, useLocation } from 'react-router-dom'

import TopNavbar from './TopNavbar'
import AppBreadcrumb from './AppBreadcrumb'
import AppFooter from './AppFooter'
import FloatingChat from '../../features/chat/components/FloatingChat'

export default function MainLayout() {
  const location = useLocation()
  const isWadOverview = location.pathname === '/ecs'
  const isWadSensorDetail = location.pathname === '/ecs/sensor-detail'

  return (
    <div className={`app-layout${isWadOverview ? ' app-layout-overview' : ''}${isWadSensorDetail ? ' app-layout-sensor-detail' : ''}`}>
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
