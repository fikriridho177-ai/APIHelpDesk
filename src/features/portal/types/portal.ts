import type { NavigationMenuItem } from '../../menus/types/menu'
import type { LynRequestItem } from '../../layanan/types/layanan'

// GET /layanan/queue
export interface PortalQueue {
  total: number
  items: LynRequestItem[]
}

export interface DivisionKpi {
  division: string
  masuk: number
  terbuka: number
  selesai: number
  rataHariSelesai: number | null
}

// GET /layanan/kpi
export interface PortalKpi {
  days: number
  divisions: DivisionKpi[]
}

export type WidgetSize = 'full' | 'half'

// Props yang diterima setiap widget dari PortalPage.
export interface WidgetProps {
  // Baris UtlMenu widget ini (nama dipakai sebagai judul).
  menu: NavigationMenuItem
  // Semua menu yang boleh diakses user (dari /menus/navigation).
  menus: NavigationMenuItem[]
  // Bertambah setiap ada perubahan tiket, supaya widget tiket memuat ulang datanya.
  refreshKey: number
  // Buka detail pengajuan di portal.
  onOpenRequest: (id: number) => void
}
