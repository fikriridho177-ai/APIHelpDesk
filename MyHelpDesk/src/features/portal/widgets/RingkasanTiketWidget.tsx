import { pickIconName } from '../../dashboard/components/DashboardIcon'
import { getLayananSummary } from '../../layanan/api/layananApi'
import SummaryTiles from '../../layanan/components/SummaryTiles'
import { WidgetFrame, useWidgetData } from '../components/WidgetFrame'
import type { WidgetProps } from '../types/portal'

// Jumlah tiket per status (milik sendiri + pengajuan masuk yang boleh dilihat).
export default function RingkasanTiketWidget({ menu, refreshKey }: WidgetProps) {
  const { data, loading, error, reload } = useWidgetData(() => getLayananSummary(), [refreshKey])

  return (
    <WidgetFrame title={menu.name} icon={pickIconName(menu)} loading={loading} error={error} onRetry={reload}>
      <SummaryTiles summary={data} />
    </WidgetFrame>
  )
}
