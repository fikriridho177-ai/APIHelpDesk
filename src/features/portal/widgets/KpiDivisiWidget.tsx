import { pickIconName } from '../../dashboard/components/DashboardIcon'
import { getKpi } from '../api/portalApi'
import { WidgetFrame, useWidgetData } from '../components/WidgetFrame'
import type { WidgetProps } from '../types/portal'

const DAYS = 30

// Tiket per divisi dalam 30 hari terakhir + rata-rata waktu selesai.
export default function KpiDivisiWidget({ menu, refreshKey }: WidgetProps) {
  const { data, loading, error, reload } = useWidgetData(() => getKpi(DAYS), [refreshKey])

  return (
    <WidgetFrame
      title={`${menu.name} · ${DAYS} hari`}
      icon={pickIconName(menu)}
      loading={loading}
      error={error}
      onRetry={reload}
      empty={data?.divisions.length === 0 ? `Belum ada tiket dalam ${DAYS} hari terakhir.` : null}
    >
      {data && (
        <div className="portal-table-wrap">
          <table className="portal-table">
            <thead>
              <tr>
                <th>Divisi</th>
                <th>Masuk</th>
                <th>Terbuka</th>
                <th>Selesai</th>
                <th>Rata-rata selesai</th>
              </tr>
            </thead>
            <tbody>
              {data.divisions.map(row => (
                <tr key={row.division}>
                  <td>{row.division}</td>
                  <td>{row.masuk}</td>
                  <td>{row.terbuka}</td>
                  <td>{row.selesai}</td>
                  <td>{row.rataHariSelesai == null ? '-' : `${row.rataHariSelesai.toLocaleString('id-ID')} hari`}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </WidgetFrame>
  )
}
