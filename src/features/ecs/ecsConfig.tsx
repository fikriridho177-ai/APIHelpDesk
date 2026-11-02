import type { ReactElement } from 'react'

import { NoAccess } from '../sensus/components/SensusParts'
import { useMenuPermission } from '../sensus/hooks/useSensus'
import CurahHujanMonitorPage from './pages/CurahHujanMonitorPage'
import CurahHujanPage from './pages/CurahHujanPage'
import DataHistorisPage from './pages/DataHistorisPage'
import KalenderCurahHujanPage from './pages/KalenderCurahHujanPage'
import KelembapanPage from './pages/KelembapanPage'
import OverviewPage from './pages/OverviewPage'
import PetaPage from './pages/PetaPage'
import StasiunMonitoringPage from './pages/StasiunMonitoringPage'
import TmakPage from './pages/TmakPage'
import WadDeviceMonitoringPage from './pages/WadDeviceMonitoringPage'
import './ecs.css'

/* =========================================================
   ECS MONITORING (akses WAD) - data sensor dari database MyKTTATest, HANYA BACA.
   Daftarkan menu di Manajemen Menu dengan KODE & ROUTE berikut,
   lalu atur Hak Akses (Read) per grup.
========================================================= */

type EcsMenu = {
  code: string
  route: string
  label: string
  page: () => ReactElement
  permissionCode?: string
}

export const ECS_MENUS: EcsMenu[] = [
  { code: 'WAD_ECS_DASHBOARD', route: '/ecs', label: 'Pusat Layanan WAD', page: () => <OverviewPage /> },
  { code: 'WAD_ECS_STASIUN', route: '/ecs/stasiun-monitoring', label: 'Monitoring Sensor', page: () => <StasiunMonitoringPage /> },
  // Perangkat adalah sudut pandang operasional dari monitoring sensor. Gunakan
  // permission stasiun sebagai fallback agar akses WAD yang sudah ada tetap berlaku.
  { code: 'WAD_ECS_PERANGKAT', permissionCode: 'WAD_ECS_STASIUN', route: '/ecs/monitoring-perangkat', label: 'Status Perangkat IoT', page: () => <WadDeviceMonitoringPage /> },
  { code: 'WAD_ECS_MONITOR_CH', route: '/ecs/monitor-curahhujan', label: 'Monitor Curah Hujan', page: () => <CurahHujanMonitorPage /> },
  // Histori dibuka dari detail sensor, jadi gunakan hak akses monitoring sensor
  // yang sudah menjadi pintu utama WAD. Tidak perlu permission baru terpisah.
  { code: 'WAD_ECS_DATA_HISTORIS', permissionCode: 'WAD_ECS_STASIUN', route: '/ecs/data-historis', label: 'Data Historis', page: () => <DataHistorisPage /> },
  { code: 'WAD_ECS_KALENDER_CH', route: '/ecs/kalender-curah-hujan', label: 'Kalender Curah Hujan', page: () => <KalenderCurahHujanPage /> },
  { code: 'WAD_ECS_PETA', route: '/ecs/peta', label: 'Peta Sensor', page: () => <PetaPage /> },
  { code: 'WAD_ECS_CURAH_HUJAN', route: '/ecs/curahhujan', label: 'Curah Hujan', page: () => <CurahHujanPage /> },
  { code: 'WAD_ECS_TMAK', route: '/ecs/tmak', label: 'Tinggi Muka Air Kanal (TMAK)', page: () => <TmakPage /> },
  { code: 'WAD_ECS_KELEMBAPAN', route: '/ecs/kelembapan', label: 'Kelembaban Tanah', page: () => <KelembapanPage /> },
]

// Halaman ECS + cek Hak Akses (Read) pada kode menunya.
export function EcsPage({ code }: { code: string }) {
  const menu = ECS_MENUS.find(m => m.code === code)
  const { permission, loaded } = useMenuPermission(menu?.permissionCode ?? code)

  if (!permission?.canRead || !menu) return <NoAccess loaded={loaded} />

  return <div className="ecs-root">{menu.page()}</div>
}
