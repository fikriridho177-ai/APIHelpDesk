export type IotNavigationItem = {
  code: string
  label: string
  description: string
  route: string
  marker: string
}

// These are the canonical IoT links shown from Environmental Monitoring.
// The routes and permission code remain unchanged so existing bookmarks and
// access rules continue to work after the legacy navbar hierarchy is removed.
export const IOT_NAVIGATION_ITEMS: IotNavigationItem[] = [
  {
    code: 'DASHBOARD_IOT',
    label: 'Dashboard IoT',
    description: 'Ringkasan kesehatan logger dan pembacaan terbaru.',
    route: '/iot-management/dashboard-iot',
    marker: '01',
  },
  {
    code: 'PETA-MONITORING',
    label: 'Peta Monitoring',
    description: 'Posisi, status, dan detail sensor setiap logger.',
    route: '/iot-management/peta-monitoring',
    marker: '02',
  },
  {
    code: 'DAFTAR_PERANGKAT',
    label: 'Manajemen Perangkat',
    description: 'Daftar perangkat Vendor dan Internal yang terdaftar.',
    route: '/iot-management/daftar-perangkat',
    marker: '03',
  },
  {
    code: 'MONITORING_SENSOR_VENDOR',
    label: 'Monitoring Sensor',
    description: 'Pantau pembacaan parameter sensor secara langsung.',
    route: '/iot-management/monitoring-sensor',
    marker: '04',
  },
  {
    code: 'RIWAYAT_DATA_SENSOR',
    label: 'Riwayat Data Sensor',
    description: 'Analisis pembacaan sensor harian hingga tahunan.',
    route: '/iot-management/riwayat-data-sensor',
    marker: '05',
  },
]

// Legacy codes are retained for permission and route compatibility, but are
// not rendered as separate navbar or portal shortcuts anymore.
export const IOT_NAVIGATION_CODES = [
  'IOT_MANAGEMENT',
  'MANAGEMEN_IOT',
  'IOT_VENDOR',
  'IOT_INTERNAL',
  'IOT_DEVELOPMENT',
  'LOGGER_LOKAL',
  'LOCAL_LOGGER',
  'DASHBOARD_IOT',
  'PETA_MONITORING',
  'PETA-MONITORING',
  'DAFTAR_PERANGKAT',
  'DAFTAR_PERANGKAT_VENDOR',
  'DAFTAR_PERANGKAT_INTERNAL',
  'MONITORING_SENSOR_VENDOR',
  'MONITORING_SENSOR',
  'RIWAYAT_DATA_SENSOR',
  'PRAKIRAAN_CUACA',
  'PRAKIRAAN-CUACA',
  'NOTIFIKASI_PERINGATAN',
  'IOT_NOTIFICATIONS',
] as const
