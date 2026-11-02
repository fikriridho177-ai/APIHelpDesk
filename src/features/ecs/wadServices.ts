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
    description: 'Buka monitoring sensor dengan fokus tinggi muka air tanah.',
    icon: 'water',
    available: true,
    route: '/ecs/stasiun-monitoring?filter=tmat',
  },
  {
    code: 'WAD_TMAK',
    label: 'Pantauan TMAK',
    description: 'Buka monitoring sensor dengan fokus tinggi muka air kanal.',
    icon: 'water',
    available: true,
    route: '/ecs/stasiun-monitoring?filter=tmak',
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
  {
    code: 'WAD_CURAH_HUJAN',
    label: 'Pengecekan Curah Hujan',
    description: 'Buka monitoring sensor dengan fokus curah hujan.',
    icon: 'water',
    available: true,
    route: '/ecs/stasiun-monitoring?filter=curah-hujan',
  },
] satisfies readonly WadServiceItem[]
