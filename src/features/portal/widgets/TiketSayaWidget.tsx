import { useNavigate } from 'react-router-dom'

import { DashboardIcon, pickIconName } from '../../dashboard/components/DashboardIcon'
import { getLayananRequests } from '../../layanan/api/layananApi'
import { TicketList } from '../components/TicketList'
import { WidgetFrame, useWidgetData } from '../components/WidgetFrame'
import type { WidgetProps } from '../types/portal'

const MAX_ITEMS = 5

// 5 pengajuan terakhir milik user.
export default function TiketSayaWidget({ menu, menus, refreshKey, onOpenRequest }: WidgetProps) {
  const navigate = useNavigate()
  const { data, loading, error, reload } = useWidgetData(
    async () => (await getLayananRequests({ mine: true })).slice(0, MAX_ITEMS),
    [refreshKey],
  )

  const hasPengajuan = menus.some(m => m.code === 'LAYANAN_PENGAJUAN')

  return (
    <WidgetFrame
      title={menu.name}
      icon={pickIconName(menu)}
      loading={loading}
      error={error}
      onRetry={reload}
      empty={data?.length === 0 ? 'Belum ada pengajuan. Buat dari Katalog Layanan.' : null}
      action={
        hasPengajuan && (
          <button type="button" className="lyn-button ghost" onClick={() => navigate('/pengajuan')}>
            Lihat semua <DashboardIcon name="arrow" />
          </button>
        )
      }
    >
      {data && <TicketList items={data} onOpen={onOpenRequest} />}
    </WidgetFrame>
  )
}
