import { useEffect, useState, type ReactElement } from "react";
import { Navigate, useLocation } from "react-router-dom";

import { getNavigationMenus } from "../features/menus/api/menuApi";
import type { NavigationMenuItem } from "../features/menus/types/menu";
import SensusApprovalPage from "../features/sensus/pages/SensusApprovalPage";
import SensusMonitoringPage from "../features/sensus/pages/SensusMonitoringPage";
import SensusAnalisisPage from "../features/sensus/pages/SensusAnalisisPage";
import MasterLokasiPage from "../features/sensus/pages/MasterLokasiPage";
import MasterJenisPage from "../features/sensus/pages/MasterJenisPage";
import LayananServicePage from "../features/layanan/pages/LayananServicePage";
import IoTDashboardPage from "../features/iot/pages/IoTDashboardPage";
import IoTMapMonitoringPage from "../features/iot/pages/IoTMapMonitoringPage";
import IoTMonitoringPage from "../features/iot/pages/IoTMonitoringPage";
import IoTVendorDevicesPage from "../features/iot/pages/IoTVendorDevicesPage";
import WeatherForecastPage from "../features/weather/pages/WeatherForecastPage";
import PermintaanApprovalPage from "../features/permintaan/pages/PermintaanApprovalPage";
import PermintaanMonitoringPage from "../features/permintaan/pages/PermintaanMonitoringPage";
import PermintaanAnalisisPage from "../features/permintaan/pages/PermintaanAnalisisPage";
import MasterJenisPermintaanPage from "../features/permintaan/pages/MasterJenisPermintaanPage";
import GisPetaPage from "../features/webgis/pages/GisPetaPage";
import { ECS_MENUS, EcsPage } from "../features/ecs/ecsConfig";

// Halaman dikenali dari KODE menu, bukan dari route.
// Jadi bila route menu berubah (misal dipindah ke parent lain di Manajemen Menu),
// halaman yang dibuka tetap sama seperti sebelum dipindahkan.
const PAGE_BY_MENU_CODE: Record<string, () => ReactElement> = {
  SNS_APPROVAL: () => <SensusApprovalPage />,
  SNS_MONITORING: () => <SensusMonitoringPage />,
  SNS_ANALISIS: () => <SensusAnalisisPage />,
  SNS_MASTER_LOKASI: () => <MasterLokasiPage />,
  SNS_MASTER_JENIS: () => <MasterJenisPage />,
  IOT_MANAGEMENT: () => (
    <Navigate to="/iot-management/dashboard-iot" replace />
  ),
  MANAGEMEN_IOT: () => (
    <Navigate to="/iot-management/dashboard-iot" replace />
  ),
  DASHBOARD_IOT: () => <IoTDashboardPage />,
  PETA_MONITORING: () => <IoTMapMonitoringPage />,
  'PETA-MONITORING': () => <IoTMapMonitoringPage />,
  RIWAYAT_DATA_SENSOR: () => (
    <IoTMonitoringPage
      title="Riwayat Data Sensor"
      subtitle="Analisis pembacaan sensor berdasarkan rentang harian, mingguan, bulanan, atau tahunan."
      eyebrow="IOT MONITORING / SENSOR HISTORY"
    />
  ),
  PRAKIRAAN_CUACA: () => <WeatherForecastPage />,
  'PRAKIRAAN-CUACA': () => <WeatherForecastPage />,
  DAFTAR_PERANGKAT_VENDOR: () => <IoTVendorDevicesPage />,
  DAFTAR_PERANGKAT: () => <IoTVendorDevicesPage />,
  DAFTAR_PERANGKAT_INTERNAL: () => (
    <Navigate to="/iot-management/daftar-perangkat" replace />
  ),
  MONITORING_SENSOR_VENDOR: () => (
    <Navigate to="/iot-management/monitoring-sensor" replace />
  ),
  MONITORING_SENSOR: () => (
    <Navigate to="/iot-management/monitoring-sensor" replace />
  ),
  PMT_APPROVAL: () => <PermintaanApprovalPage />,
  PMT_MONITORING: () => <PermintaanMonitoringPage />,
  PMT_ANALISIS: () => <PermintaanAnalisisPage />,
  PMT_MASTER_JENIS: () => <MasterJenisPermintaanPage />,
  GIS_PETA: () => <GisPetaPage />,
  ...Object.fromEntries(
    ECS_MENUS.map((m) => [m.code, () => <EcsPage code={m.code} />]),
  ),
};

const normalize = (route: string) => {
  const value = decodeURIComponent(route.trim()).toLowerCase();
  return value.length > 1 ? value.replace(/\/+$/, "") : value;
};

// Dipakai untuk path yang tidak terdaftar di AppRoutes, dan untuk /layanan/... :
// menu ber-kode di atas (misal PMT_MASTER_JENIS di route /layanan/...) membuka halamannya sendiri,
// selain itu tampil `fallback` (halaman layanan) atau kembali ke dashboard.
export default function MenuRouteResolver({
  fallback,
}: {
  fallback?: ReactElement;
}) {
  const location = useLocation();
  const [menus, setMenus] = useState<NavigationMenuItem[] | null>(null);

  useEffect(() => {
    getNavigationMenus()
      .then(setMenus)
      .catch(() => setMenus([]));
  }, []);

  if (menus === null) return <div className="lyn-empty">Memuat halaman...</div>;

  const path = normalize(location.pathname);
  const menu = menus.find((m) => m.route && normalize(m.route) === path);
  // Sub-layanan (kode LYN_...) yang dipindah ke parent lain tetap membuka halaman layanannya.
  const page = menu
    ? (PAGE_BY_MENU_CODE[menu.code] ??
      (menu.code.startsWith("LYN_") ? () => <LayananServicePage /> : undefined))
    : undefined;

  return page ? page() : (fallback ?? <Navigate to="/" replace />);
}
