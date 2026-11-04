import type { IconName } from '../dashboard/components/DashboardIcon'

export interface WadServiceItem {
  order: number
  code: string
  label: string
  description: string
  icon: IconName
  available: boolean
  route?: string
}

// TMAT, TMAK, dan curah hujan tersedia sebagai filter pada halaman Monitoring Sensor.
export const WAD_SENSOR_FOCUS_COUNT = 3

// Kartu pusat layanan hanya berisi workspace operasional. Fokus TMAT, TMAK,
// dan curah hujan tetap dipilih sebagai filter di halaman Monitoring Sensor.
export const WAD_SERVICE_ITEMS = [
  {
    order: 1,
    code: 'WAD_MONITORING_SENSOR',
    label: 'Monitoring Sensor',
    description: 'Pantau TMAT, TMAK, dan curah hujan dalam satu tampilan dengan filter.',
    icon: 'water',
    available: true,
    route: '/ecs/stasiun-monitoring?filter=all',
  },
  {
    order: 2,
    code: 'WAD_PETA_SENSOR',
    label: 'Peta Monitoring Sensor',
    description: 'Lihat sebaran logger, status, dan titik pembacaan sensor di lapangan.',
    icon: 'map',
    available: true,
    route: '/ecs/peta-monitoring',
  },
  {
    order: 3,
    code: 'WAD_PERANGKAT',
    label: 'Status Perangkat IoT',
    description: 'Cek keaktifan logger dan waktu payload terakhir diterima.',
    icon: 'desktop',
    available: true,
    route: '/ecs/monitoring-perangkat',
  },
  {
    order: 4,
    code: 'WAD_PREDIKSI_HUJAN',
    label: 'Prediksi Hujan 14 Hari',
    description: 'Lihat peluang hujan 14 hari berdasarkan koordinat logger terpilih.',
    icon: 'chart',
    available: true,
    route: '/ecs/prakiraan-hujan',
  },
  {
    order: 5,
    code: 'WAD_KALENDER_HUJAN',
    label: 'Kalender Data Sensor',
    description: 'Telusuri curah hujan serta rata-rata harian TMAK dan TMAT per sensor.',
    icon: 'calendar',
    available: true,
    route: '/ecs/kalender-curah-hujan',
  },
  {
    order: 6,
    code: 'WAD_PINTU_AIR',
    label: 'Kondisi Pintu Air',
    description: 'Pantau kondisi dan kesiapan pintu air di lapangan.',
    icon: 'settings',
    available: false,
  },
  {
    order: 7,
    code: 'WAD_SIPHON_GORONG',
    label: 'Kondisi Siphon / Gorong-gorong',
    description: 'Periksa aliran dan kondisi titik lintasan air.',
    icon: 'map',
    available: false,
  },
  {
    order: 8,
    code: 'WAD_SUBSIDEN_GAMBUT',
    label: 'Pengecekan Subsiden Gambut',
    description: 'Pantau perubahan elevasi dan indikasi penurunan lahan gambut.',
    icon: 'chart',
    available: false,
  },
] satisfies readonly WadServiceItem[]

// Angka ini menghitung kartu workspace. Tiga fokus sensor ditampilkan
// terpisah sebagai filter, bukan modul/kartu duplikat.
export const WAD_TOTAL_MODULE_COUNT = WAD_SERVICE_ITEMS.length
