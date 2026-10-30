import { Outlet } from 'react-router-dom'

import TopNavbar from './TopNavbar'
import AppBreadcrumb from './AppBreadcrumb'
import AppFooter from './AppFooter'
import FloatingChat from '../../features/chat/components/FloatingChat'

export default function MainLayout() {
  return (
    <div className="app-layout">
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
