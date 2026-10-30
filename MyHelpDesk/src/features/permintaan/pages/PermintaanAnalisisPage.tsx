import { useEffect, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { getLayananReport } from '../../layanan/api/layananApi'
import { exportExcel, exportPdf, type ExportTable } from '../../layanan/exportUtils'
import { PRIORITY_CONFIG } from '../../layanan/layananConfig'
import type { LynPriority, LynReport, LynReportFilter, LynReportGroup } from '../../layanan/types/layanan'
import { BarList, NoAccess, SensusHero, StatTiles } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import {
  PMT_MENU,
  ReportFilterBar,
  filterText,
  groupExport,
  hariText,
  persenText,
  todayIso,
  useJenisOptions,
} from '../permintaanShared'
import '../../layanan/layanan.css'
import '../../sensus/sensus.css'

const BULAN = ['Jan', 'Feb', 'Mar', 'Apr', 'Mei', 'Jun', 'Jul', 'Agu', 'Sep', 'Okt', 'Nov', 'Des']
const bulanLabel = (month: string) => {
  const [year, m] = month.split('-')
  return `${BULAN[Number(m) - 1] ?? m} ${year}`
}

const persenSelesai = (g: LynReportGroup) => (g.total === 0 ? null : Math.round((g.selesai * 1000) / g.total) / 10)

// /permintaan/analisis : kinerja layanan per divisi, jenis, prioritas, dan tren bulanan + export Excel / PDF.
export default function PermintaanAnalisisPage() {
  const { permission, loaded } = useMenuPermission(PMT_MENU.analisis)
  const { divisions, jenis } = useJenisOptions()

  const [filter, setFilter] = useState<LynReportFilter>({ dateFrom: todayIso(-364), dateTo: todayIso() })
  const [data, setData] = useState<LynReport | null>(null)
  const [loading, setLoading] = useState(true)

  const canRead = !!permission?.canRead
  const canExport = !!permission?.canExport

  useEffect(() => {
    if (!canRead) return
    setLoading(true)
    const timer = setTimeout(() => {
      getLayananReport(filter)
        .then(setData)
        .catch(error => console.error('Gagal mengambil analisis permintaan:', error))
        .finally(() => setLoading(false))
    }, 300)
    return () => clearTimeout(timer)
  }, [canRead, filter])

  if (!canRead) return <NoAccess loaded={loaded} />

  function tables(): ExportTable[] {
    if (!data) return []
    const kinerja = (title: string, label: string, rows: LynReportGroup[], showDivision = false): ExportTable => ({
      title,
      header: [...(showDivision ? ['Divisi'] : []), label, 'Masuk', 'Selesai', 'Selesai (%)', 'Rata-rata Selesai (hari)', 'Tepat Waktu (%)', 'Lewat Deadline'],
      rows: rows.map(g => [
        ...(showDivision ? [g.division] : []),
        g.label,
        g.total,
        g.selesai,
        persenSelesai(g) ?? '',
        g.rataHariSelesai ?? '',
        g.persenTepatWaktu ?? '',
        g.terlambat,
      ]),
    })

    return [
      kinerja('Kinerja per Divisi', 'Divisi', data.perDivisi),
      kinerja('Kinerja per Jenis', 'Jenis Permintaan', data.perJenis, true),
      groupExport('Per Prioritas', 'Prioritas', data.perPrioritas),
      {
        title: 'Tren Bulanan',
        header: ['Bulan', 'Masuk', 'Selesai'],
        rows: data.perBulan.map(m => [bulanLabel(m.month), m.masuk, m.selesai]),
      },
    ]
  }

  const t = data?.total

  return (
    <section className="lyn-page">
      <SensusHero
        title="Analisis Permintaan"
        subtitle="Kinerja layanan tiap divisi: jumlah permintaan, lama penyelesaian, ketepatan terhadap deadline, dan tren bulanan."
      >
        {canExport && data && (
          <div className="pmt-actions">
            <button type="button" className="lyn-button ghost" onClick={() => exportExcel('analisis-permintaan', tables())}>
              <DashboardIcon name="download" /> Excel
            </button>
            <button
              type="button"
              className="lyn-button ghost"
              onClick={() => void exportPdf('analisis-permintaan', 'Analisis Permintaan', filterText(filter, divisions, jenis), tables())}
            >
              <DashboardIcon name="download" /> PDF
            </button>
          </div>
        )}
      </SensusHero>

      <div className="lyn-card">
        <ReportFilterBar value={filter} onChange={setFilter} divisions={divisions} jenis={jenis} showStatus={false} />
      </div>

      <StatTiles
        items={[
          { label: 'Permintaan masuk', value: t?.total ?? '-', tone: 'indigo' },
          { label: 'Selesai', value: t ? `${t.selesai} (${persenText(persenSelesai(t))})` : '-', tone: 'green' },
          { label: 'Rata-rata waktu selesai', value: t ? hariText(t.rataHariSelesai) : '-', tone: 'blue' },
          { label: 'Selesai tepat waktu', value: t ? persenText(t.persenTepatWaktu) : '-', tone: 'purple' },
          { label: 'Masih terbuka lewat deadline', value: t?.terlambat ?? '-', tone: 'red' },
        ]}
      />

      {loading || !data ? (
        <div className="lyn-empty">Memuat analisis...</div>
      ) : (
        <>
          <div className="sns-grid-2">
            <div className="lyn-card">
              <h3>Permintaan masuk per bulan</h3>
              <BarList rows={data.perBulan.map(m => ({ label: bulanLabel(m.month), value: m.masuk, tone: 'blue', hint: `${m.selesai} selesai` }))} />
            </div>
            <div className="lyn-card">
              <h3>Permintaan per divisi</h3>
              <BarList rows={data.perDivisi.map(g => ({ label: g.label, value: g.total, tone: 'indigo', hint: `${g.selesai} selesai` }))} />
            </div>
          </div>

          <div className="lyn-card">
            <h3>Kinerja per divisi</h3>
            <PerformanceTable rows={data.perDivisi} labelHeader="Divisi" />
          </div>

          <div className="lyn-card">
            <h3>Kinerja per jenis permintaan</h3>
            <PerformanceTable rows={data.perJenis} labelHeader="Jenis permintaan" showDivision />
          </div>

          <div className="lyn-card">
            <h3>Per skala prioritas</h3>
            <PerformanceTable rows={data.perPrioritas} labelHeader="Prioritas" />
          </div>

          <p className="lyn-muted">
            Rata-rata waktu selesai dihitung dari tiket berstatus Selesai (tanggal dibuat sampai selesai). Tepat waktu = tiket selesai
            pada atau sebelum Tanggal Deadline. Periode mengikuti tanggal tiket dibuat.
          </p>
        </>
      )}
    </section>
  )
}

function PerformanceTable({ rows, labelHeader, showDivision }: { rows: LynReportGroup[]; labelHeader: string; showDivision?: boolean }) {
  if (rows.length === 0) return <p className="lyn-muted">Belum ada data.</p>

  return (
    <div className="table-container">
      <table className="sns-table compact">
        <thead>
          <tr>
            {showDivision && <th>Divisi</th>}
            <th>{labelHeader}</th>
            <th className="num">Masuk</th>
            <th className="num">Selesai</th>
            <th className="num">% Selesai</th>
            <th className="num">Rata-rata selesai</th>
            <th className="num">Tepat waktu</th>
            <th className="num">Lewat deadline</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(g => (
            <tr key={`${g.division}-${g.label}`}>
              {showDivision && <td>{g.division}</td>}
              <td>{labelHeader === 'Prioritas' ? PRIORITY_CONFIG[g.label as LynPriority]?.label ?? g.label : g.label}</td>
              <td className="num">
                <b>{g.total}</b>
              </td>
              <td className="num">{g.selesai}</td>
              <td className="num">{persenText(persenSelesai(g))}</td>
              <td className="num">{hariText(g.rataHariSelesai)}</td>
              <td className="num">{persenText(g.persenTepatWaktu)}</td>
              <td className={g.terlambat > 0 ? 'num pmt-late' : 'num'}>{g.terlambat}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
