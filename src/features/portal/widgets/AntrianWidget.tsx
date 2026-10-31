import { useNavigate } from 'react-router-dom'

import { DashboardIcon, pickIconName } from '../../dashboard/components/DashboardIcon'
import { getQueue } from '../api/portalApi'
import { TicketList } from '../components/TicketList'
import { WidgetFrame, useWidgetData } from '../components/WidgetFrame'
import type { WidgetProps } from '../types/portal'

// Tiket yang menunggu tindakan user sebagai petugas (Terima / Jadwalkan / Proses).
export default function AntrianWidget({ menu, menus, refreshKey, onOpenRequest }: WidgetProps) {
  const navigate = useNavigate()
  const { data, loading, error, reload } = useWidgetData(() => getQueue(10), [refreshKey])

  const hasPengajuan = menus.some(m => m.code === 'LAYANAN_PENGAJUAN')
  const title = data && data.total > 0 ? `${menu.name} (${data.total})` : menu.name

  return (
    <WidgetFrame
      title={title}
      icon={pickIconName(menu)}
      loading={loading}
      error={error}
      onRetry={reload}
      empty={data?.items.length === 0 ? 'Tidak ada tiket yang menunggu tindakan Anda 🎉' : null}
      action={
        hasPengajuan && (
          <button type="button" className="lyn-button ghost" onClick={() => navigate('/pengajuan')}>
            Lihat semua <DashboardIcon name="arrow" />
          </button>
        )
      }
    >
      {data && <TicketList items={data.items} detailed onOpen={onOpenRequest} />}
    </WidgetFrame>
  )
}
