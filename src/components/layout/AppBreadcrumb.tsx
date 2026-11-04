import { Link, useLocation, useNavigate } from "react-router-dom";
import { getBreadcrumbBackPath } from "./breadcrumbNavigation";

type RouteItem = { label: string; path?: string };

const ECS_BREADCRUMB_LABELS: Record<string, string> = {
  "/ecs/stasiun-monitoring": "Monitoring Sensor",
  "/ecs/sensor-detail": "Detail Sensor",
  "/ecs/monitoring-perangkat": "Status Perangkat IoT",
  "/ecs/peta-monitoring": "Peta Monitoring Sensor",
  "/ecs/prakiraan-hujan": "Prediksi Hujan 14 Hari",
  "/ecs/monitor-curahhujan": "Monitor Curah Hujan",
  "/ecs/data-historis": "Data Historis",
  "/ecs/kalender-curah-hujan": "Kalender Data Sensor",
  "/ecs/peta": "Peta Sensor",
  "/ecs/curahhujan": "Curah Hujan",
  "/ecs/tmak": "Tinggi Muka Air Kanal (TMAK)",
  "/ecs/kelembapan": "Kelembaban Tanah",
};

function resolveItems(pathname: string): RouteItem[] {
  if (pathname === "/utility/users") return [{ label: "Master User" }];
  if (pathname === "/utility/groups") return [{ label: "Hak Akses" }];
  if (/^\/utility\/groups\/\d+\/menus$/.test(pathname))
    return [
      { label: "Hak Akses", path: "/utility/groups" },
      { label: "Akses Menu" },
    ];
  if (/^\/utility\/groups\/\d+\/companies$/.test(pathname))
    return [
      { label: "Hak Akses", path: "/utility/groups" },
      { label: "Akses Perusahaan Anak" },
    ];
  if (/^\/utility\/groups\/\d+\/departments$/.test(pathname))
    return [
      { label: "Hak Akses", path: "/utility/groups" },
      { label: "Akses Departemen" },
    ];
  if (/^\/utility\/groups\/\d+\/item-sub-categories$/.test(pathname))
    return [
      { label: "Hak Akses", path: "/utility/groups" },
      { label: "Akses Item Sub Kategori" },
    ];
  if (pathname === "/utility/security")
    return [
      { label: "Hak Akses", path: "/utility/groups" },
      { label: "Master Security" },
    ];
  if (pathname === "/utility/menus") return [{ label: "Manajemen Menu" }];
  if (pathname === "/change-password") return [{ label: "Ganti Password" }];
  if (pathname === "/layanan") return [{ label: "Layanan" }];
  if (pathname.startsWith("/layanan/"))
    return [
      { label: "Layanan", path: "/layanan" },
      { label: "Detail Layanan" },
    ];
  if (pathname === "/pengajuan")
    return [
      { label: "Layanan", path: "/layanan" },
      { label: "Daftar Pengajuan" },
    ];
  if (pathname === "/ecs") return [{ label: "Pusat Layanan WAD" }];
  if (pathname.startsWith("/ecs/"))
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: ECS_BREADCRUMB_LABELS[pathname] ?? "Detail Monitoring" },
    ];
  if (pathname === "/iot-management/dashboard-iot")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Dashboard IoT" },
    ];
  if (pathname === "/iot-management/peta-monitoring")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Peta Monitoring" },
    ];
  if (pathname === "/iot-management/prakiraan-cuaca")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Prediksi Hujan 14 Hari" },
    ];
  if (pathname === "/iot-management/daftar-perangkat")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Daftar Perangkat IoT" },
    ];
  if (pathname === "/iot-management/monitoring-sensor")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Monitoring Sensor IoT" },
    ];
  if (pathname === "/iot-management/riwayat-data-sensor")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Riwayat Data Sensor" },
    ];
  if (pathname === "/iot-management/iot-vendor/daftar-perangkat-vendor")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Daftar Perangkat IoT" },
    ];
  if (pathname === "/iot-management/iot-vendor/monitoring-sensor-vendor")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Monitoring Sensor IoT" },
    ];
  if (pathname === "/iot-management/logger-lokal/daftar-perangkat-internal")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Daftar Perangkat IoT" },
    ];
  if (pathname === "/iot-management/logger-lokal/monitoring-sensor")
    return [
      { label: "Pusat Layanan WAD", path: "/ecs" },
      { label: "Monitoring Sensor IoT" },
    ];
  return [{ label: "Halaman" }];
}

export default function AppBreadcrumb() {
  const location = useLocation();
  const navigate = useNavigate();
  if (location.pathname === "/") return null;
  const items = resolveItems(location.pathname);
  const fixedBackPath = getBreadcrumbBackPath(location.pathname);

  return (
    <div className="breadcrumb-container">
      <button
        type="button"
        className="breadcrumb-back"
        onClick={() => fixedBackPath ? navigate(fixedBackPath) : navigate(-1)}
        title={fixedBackPath ? "Kembali ke halaman ECS" : "Kembali ke halaman sebelumnya"}
      >
        ← Kembali
      </button>
      <div className="breadcrumb">
        <Link to="/">Dasbor</Link>
        {items.map((item, index) => {
          const isLast = index === items.length - 1;
          return (
            <span
              key={`${location.pathname}-${index}`}
              className="breadcrumb-item"
            >
              <span className="breadcrumb-separator">/</span>
              {item.path && !isLast ? (
                <Link to={item.path}>{item.label}</Link>
              ) : (
                <span className="breadcrumb-current">{item.label}</span>
              )}
            </span>
          );
        })}
      </div>
    </div>
  );
}
