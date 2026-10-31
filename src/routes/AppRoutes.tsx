import { Navigate, Route, Routes } from 'react-router-dom'
import MainLayout from '../components/layout/MainLayout'
import LoginPage from '../features/auth/pages/LoginPage'
import ChangePasswordPage from '../features/auth/pages/ChangePasswordPage'
import UsersPage from '../features/users/pages/UsersPage'
import GroupsPage from '../features/groups/pages/GroupsPage'
import GroupMenuAccessPage from '../features/groups/pages/GroupMenuAccessPage'
import GroupScopeAccessPage from '../features/groups/pages/GroupScopeAccessPage'
import SecurityPage from '../features/security/pages/SecurityPage'
import MenusPage from '../features/menus/pages/MenusPage'
import SplashPage from '../features/auth/pages/SplashPage'
import PortalPage from '../features/portal/pages/PortalPage'
import LiveChatRoute from '../features/chat/pages/LiveChatRoute'
import LayananHomePage from '../features/layanan/pages/LayananHomePage'
import LayananServicePage from '../features/layanan/pages/LayananServicePage'
import PengajuanPage from '../features/layanan/pages/PengajuanPage'
import PermintaanApprovalPage from '../features/permintaan/pages/PermintaanApprovalPage'
import PermintaanMonitoringPage from '../features/permintaan/pages/PermintaanMonitoringPage'
import PermintaanAnalisisPage from '../features/permintaan/pages/PermintaanAnalisisPage'
import MasterJenisPermintaanPage from '../features/permintaan/pages/MasterJenisPermintaanPage'
import DivisiHubPage from '../features/divisi/pages/DivisiHubPage'
import GisPetaPage from '../features/webgis/pages/GisPetaPage'
import MenuRouteResolver from './MenuRouteResolver'
import SensusApprovalPage from '../features/sensus/pages/SensusApprovalPage'
import SensusMonitoringPage from '../features/sensus/pages/SensusMonitoringPage'
import SensusAnalisisPage from '../features/sensus/pages/SensusAnalisisPage'
import MasterLokasiPage from '../features/sensus/pages/MasterLokasiPage'
import MasterJenisPage from '../features/sensus/pages/MasterJenisPage'
import { ECS_MENUS, EcsPage } from '../features/ecs/ecsConfig'
import IoTDashboardPage from '../features/iot/pages/IoTDashboardPage'
import IoTMonitoringPage from '../features/iot/pages/IoTMonitoringPage'
import IoTMapMonitoringPage from '../features/iot/pages/IoTMapMonitoringPage'
import IoTVendorDevicesPage from '../features/iot/pages/IoTVendorDevicesPage'
import WeatherForecastPage from '../features/weather/pages/WeatherForecastPage'

const Private = ({ children }: { children: React.ReactNode }) =>
  localStorage.getItem('token') ? <>{children}</> : <Navigate to="/login" replace />

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/splash" element={<Private><SplashPage /></Private>} />
      <Route element={<Private><MainLayout /></Private>}>
        <Route index element={<PortalPage />} />
        <Route path="utility/groups" element={<GroupsPage />} />
        <Route path="utility/groups/:groupId/menus" element={<GroupMenuAccessPage />} />
        <Route path="utility/groups/:groupId/companies" element={<GroupScopeAccessPage kind="companies" />} />
        <Route path="utility/groups/:groupId/departments" element={<GroupScopeAccessPage kind="departments" />} />
        <Route path="utility/groups/:groupId/item-sub-categories" element={<GroupScopeAccessPage kind="item-sub-categories" />} />
        <Route path="utility/users" element={<UsersPage />} />
        <Route path="utility/security" element={<SecurityPage />} />
        <Route path="utility/menus" element={<MenusPage />} />
        <Route path="live-chat" element={<LiveChatRoute />} />
        <Route path="layanan" element={<LayananHomePage />} />
        <Route path="layanan/*" element={<MenuRouteResolver fallback={<LayananServicePage />} />} />
        <Route path="pengajuan" element={<PengajuanPage />} />
        <Route path="permintaan/approval" element={<PermintaanApprovalPage />} />
        <Route path="permintaan/monitoring" element={<PermintaanMonitoringPage />} />
        <Route path="permintaan/analisis" element={<PermintaanAnalisisPage />} />
        <Route path="permintaan/master-jenis" element={<MasterJenisPermintaanPage />} />
        {/* Halaman divisi (kartu Katalog dashboard) & Peta GIS (viewer WebGIS) */}
        <Route path="divisi/:slug" element={<DivisiHubPage />} />
        <Route path="gis/peta" element={<GisPetaPage />} />
        <Route path="sensus" element={<Navigate to="/sensus/approval" replace />} />
        <Route path="sensus/approval" element={<SensusApprovalPage />} />
        <Route path="sensus/monitoring" element={<SensusMonitoringPage />} />
        <Route path="sensus/analisis" element={<SensusAnalisisPage />} />
        <Route path="sensus/master/lokasi" element={<MasterLokasiPage />} />
        <Route path="sensus/master/jenis" element={<MasterJenisPage />} />
        <Route path="iot-management" element={<Navigate to="/iot-management/dashboard-iot" replace />} />
        <Route path="iot-management/dashboard-iot" element={<IoTDashboardPage />} />
        <Route path="iot-management/peta-monitoring" element={<IoTMapMonitoringPage />} />
        <Route path="iot-management/prakiraan-cuaca" element={<WeatherForecastPage />} />
        <Route path="iot-management/daftar-perangkat" element={<IoTVendorDevicesPage />} />
        <Route path="iot-management/monitoring-sensor" element={<IoTMonitoringPage />} />
        <Route path="iot-management/riwayat-data-sensor" element={<IoTMonitoringPage title="Riwayat Data Sensor" subtitle="Analisis pembacaan sensor berdasarkan rentang harian, mingguan, bulanan, atau tahunan." eyebrow="IOT MONITORING / SENSOR HISTORY" />} />
        {/* Alias root lama: menu database lama masih dapat mengirim /managemen-iot. */}
        <Route path="managemen-iot" element={<Navigate to="/iot-management/dashboard-iot" replace />} />
        <Route path="manajemen-iot" element={<Navigate to="/iot-management/dashboard-iot" replace />} />
        <Route path="managemen-iot/dashboard-iot" element={<Navigate to="/iot-management/dashboard-iot" replace />} />
        <Route path="managemen-iot/daftar-perangkat" element={<Navigate to="/iot-management/daftar-perangkat" replace />} />
        <Route path="managemen-iot/monitoring-sensor" element={<Navigate to="/iot-management/monitoring-sensor" replace />} />
        <Route path="managemen-iot/peta-monitoring" element={<Navigate to="/iot-management/peta-monitoring" replace />} />
        <Route path="managemen-iot/prakiraan-cuaca" element={<Navigate to="/iot-management/prakiraan-cuaca" replace />} />
        <Route path="manajemen-iot/*" element={<Navigate to="/iot-management/dashboard-iot" replace />} />
        <Route
          path="iot-management/iot-vendor/daftar-perangkat-vendor"
          element={<Navigate to="/iot-management/daftar-perangkat" replace />}
        />
        <Route
          path="iot-management/iot-vendor/monitoring-sensor-vendor"
          element={<Navigate to="/iot-management/monitoring-sensor" replace />}
        />
        <Route
          path="iot-management/logger-lokal/daftar-perangkat-internal"
          element={<Navigate to="/iot-management/daftar-perangkat" replace />}
        />
        <Route
          path="iot-management/logger-lokal/monitoring-sensor"
          element={<Navigate to="/iot-management/monitoring-sensor" replace />}
        />
        {/* ECS Monitoring (akses WAD) */}
        {ECS_MENUS.map(m => (
          <Route key={m.code} path={m.route.slice(1)} element={<EcsPage code={m.code} />} />
        ))}
        <Route path="change-password" element={<ChangePasswordPage />} />
        {/* Route lain: dicari dari Manajemen Menu (berdasarkan kode menu), selain itu kembali ke dashboard */}
        <Route path="*" element={<MenuRouteResolver />} />
      </Route>
    </Routes>
  )
}
