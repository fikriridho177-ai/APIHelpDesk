import type { IconName } from '../dashboard/components/DashboardIcon'

export interface WadServiceItem {
  code: string
  label: string
  description: string
  icon: IconName
  available: boolean
  route?: string
}

export const WAD_SERVICE_ITEMS = [
  {
    code: 'WAD_TMAT',
    label: 'Pantauan TMAT',
    description: 'Fokuskan pembacaan logger pada tinggi muka air tanah.',
    icon: 'water',
    available: true,
    route: '/ecs/stasiun-monitoring?filter=tmat',
  },
  {
    code: 'WAD_TMAK',
    label: 'Pantauan TMAK',
    description: 'Fokuskan pembacaan logger pada tinggi muka air kanal.',
    icon: 'water',
    available: true,
    route: '/ecs/stasiun-monitoring?filter=tmak',
  },
  {
    code: 'WAD_PERANGKAT',
    label: 'Status Perangkat IoT',
    description: 'Cek keaktifan logger dan waktu payload terakhir diterima.',
    icon: 'desktop',
    available: true,
    route: '/ecs/monitoring-perangkat',
  },
  {
    code: 'WAD_CURAH_HUJAN',
    label: 'Pengecekan Curah Hujan',
    description: 'Fokuskan pembacaan logger pada curah hujan terbaru.',
    icon: 'water',
    available: true,
    route: '/ecs/stasiun-monitoring?filter=curah-hujan',
  },
  {
    code: 'WAD_PINTU_AIR',
    label: 'Kondisi Pintu Air',
    description: 'Pantau kondisi dan kesiapan pintu air di lapangan.',
    icon: 'settings',
    available: false,
  },
  {
    code: 'WAD_SIPHON_GORONG',
    label: 'Kondisi Siphon / Gorong-gorong',
    description: 'Periksa aliran dan kondisi titik lintasan air.',
    icon: 'map',
    available: false,
  },
  {
    code: 'WAD_SUBSIDEN_GAMBUT',
    label: 'Pengecekan Subsiden Gambut',
    description: 'Pantau perubahan elevasi dan indikasi penurunan lahan gambut.',
    icon: 'chart',
    available: false,
  },
] satisfies readonly WadServiceItem[]
