import { pickIconName } from '../../dashboard/components/DashboardIcon'
import { DIVISI_ROUTES } from '../../divisi/divisiConfig'
import { DivisionCards } from '../../layanan/components/ServiceCards'
import { useLayananServices } from '../../layanan/hooks/useLayanan'
import { WidgetFrame } from '../components/WidgetFrame'
import type { WidgetProps } from '../types/portal'

// Kartu divisi layanan (RNA, GIS, WAD, QC, NAT).
// RNA & GIS dibuka ke halaman divisi (/divisi/...), WAD ke halaman ECS, lainnya ke halaman tiket layanan.
export default function KatalogWidget({ menu }: WidgetProps) {
  const { divisions, loading } = useLayananServices()

  return (
    <WidgetFrame
      title={menu.name}
      icon={pickIconName(menu)}
      loading={loading}
      empty={divisions.length === 0 ? 'Belum ada layanan yang dapat Anda akses.' : null}
    >
      <DivisionCards divisions={divisions} routeOverrides={{ ...DIVISI_ROUTES, LYN_WAD: '/ecs' }} />
    </WidgetFrame>
  )
}
