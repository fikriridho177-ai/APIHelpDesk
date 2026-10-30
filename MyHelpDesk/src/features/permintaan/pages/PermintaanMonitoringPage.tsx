import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { getLayananReport } from '../../layanan/api/layananApi'
import { PriorityBadge, StatusBadge } from '../../layanan/components/Badges'
import RequestDetailModal from '../../layanan/components/RequestDetailModal'
import { exportExcel, exportPdf } from '../../layanan/exportUtils'
import { useDepartmentNames } from '../../layanan/hooks/useLayanan'
import { formatDate, formatDateTime } from '../../layanan/layananConfig'
import type { LynReport, LynReportFilter, LynReportRow } from '../../layanan/types/layanan'
import { NoAccess, SensusHero, StatTiles } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import {
  GroupTable,
  PMT_MENU,
  ReportFilterBar,
  filterText,
  groupExport,
  rowsExport,
  rowsPerDivision,
  todayIso,
  useJenisOptions,
} from '../permintaanShared'
import '../../layanan/layanan.css'
import '../../sensus/sensus.css'

type Tab = 'ringkasan' | 'antrian' | 'terlambat' | 'data'

// /permintaan/monitoring : status, antrian, dan tiket lewat deadline semua divisi + export Excel / PDF.
export default function PermintaanMonitoringPage() {
  const { permission, loaded } = useMenuPermission(PMT_MENU.monitoring)
  const { divisions, jenis } = useJenisOptions()
  const departmentNames = useDepartmentNames()

  // ?division= & ?jenisId= dari kartu masalah di halaman divisi (mis. GIS).
  const [params] = useSearchParams()
  const [filter, setFilter] = useState<LynReportFilter>({
    dateFrom: todayIso(-89),
    dateTo: todayIso(),
    divisionCode: params.get('division') || undefined,
    jenisId: Number(params.get('jenisId')) || undefined,
  })
  const [data, setData] = useState<LynReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [tab, setTab] = useState<Tab>('ringkasan')
  const [openedId, setOpenedId] = useState<number | null>(null)

  const canRead = !!permission?.canRead
  const canExport = !!permission?.canExport

  const load = useCallback(() => getLayananReport(filter).then(setData), [filter])

  useEffect(() => {
    if (!canRead) return
    setLoading(true)
    const timer = setTimeout(() => {
      load()
        .catch(error => console.error('Gagal mengambil monitoring permintaan:', error))
        .finally(() => setLoading(false))
    }, 300)
    return () => clearTimeout(timer)
  }, [canRead, load])

  // Antrian: tiket yang punya nomor antrian, urut divisi lalu nomor.
  const antrian = useMemo(
    () =>
      (data?.rows ?? [])
        .filter(r => r.queueNo != null)
        .sort((a, b) => a.divisionName.localeCompare(b.divisionName) || (a.queueNo ?? 0) - (b.queueNo ?? 0)),
    [data],
  )
  const terlambat = useMemo(() => (data?.rows ?? []).filter(r => r.terlambat), [data])

  if (!canRead) return <NoAccess loaded={loaded} />

  const subtitle = filterText(filter, divisions, jenis)

  function excel() {
    if (!data) return
    exportExcel('monitoring-permintaan', [
      groupExport('Per Divisi', 'Divisi', data.perDivisi),
      groupExport('Per Jenis', 'Jenis Permintaan', data.perJenis, true),
      rowsExport('Antrian', antrian),
      rowsExport('Lewat Deadline', terlambat),
      rowsExport('Semua Data', data.rows),
      ...(filter.divisionCode ? [] : rowsPerDivision(data.rows)),
    ])
  }

  function pdf() {
    if (!data) return
    void exportPdf('monitoring-permintaan', 'Monitoring Permintaan', subtitle, [
      groupExport('Ringkasan per divisi', 'Divisi', data.perDivisi),
      groupExport('Ringkasan per jenis permintaan', 'Jenis Permintaan', data.perJenis, true),
      rowsExport('Antrian', antrian, true),
      rowsExport('Lewat deadline', terlambat, true),
      rowsExport('Semua tiket', data.rows, true),
    ])
  }

  const t = data?.total

  return (
    <section className="lyn-page">
      <SensusHero
        title="Monitoring Permintaan"
        subtitle="Pantau tiket permintaan semua divisi: menunggu approval, antrian, dalam proses, selesai, dan yang lewat deadline."
      />

      <div className="lyn-card">
        <ReportFilterBar value={filter} onChange={setFilter} divisions={divisions} jenis={jenis} />
      </div>

      <StatTiles
        items={[
          { label: 'Total tiket', value: t?.total ?? '-', tone: 'indigo' },
          { label: 'Menunggu approval', value: t?.menungguApproval ?? '-', tone: 'blue' },
          { label: 'Disetujui / dijadwalkan', value: t?.disetujui ?? '-', tone: 'purple' },
          { label: 'Dalam proses', value: t?.dalamProses ?? '-', tone: 'amber' },
          { label: 'Selesai', value: t?.selesai ?? '-', tone: 'green' },
          { label: 'Lewat deadline', value: t?.terlambat ?? '-', tone: 'red' },
        ]}
      />

      <div className="sns-tabs">
        {(
          [
            ['ringkasan', 'Ringkasan'],
            ['antrian', `Antrian (${antrian.length})`],
            ['terlambat', `Lewat Deadline (${terlambat.length})`],
            ['data', `Semua Tiket (${data?.rows.length ?? 0})`],
          ] as [Tab, string][]
        ).map(([key, label]) => (
          <button key={key} type="button" className={tab === key ? 'lyn-chip active' : 'lyn-chip'} onClick={() => setTab(key)}>
            {label}
          </button>
        ))}

        {canExport && data && (
          <span className="sns-tabs-action pmt-actions">
            <button type="button" className="lyn-button ghost" onClick={excel}>
              <DashboardIcon name="download" /> Excel
            </button>
            <button type="button" className="lyn-button ghost" onClick={pdf}>
              <DashboardIcon name="download" /> PDF
            </button>
          </span>
        )}
      </div>

      {loading || !data ? (
        <div className="lyn-empty">Memuat monitoring...</div>
      ) : tab === 'ringkasan' ? (
        <>
          <div className="lyn-card">
            <h3>Per divisi</h3>
            <GroupTable rows={data.perDivisi} labelHeader="Divisi" />
          </div>
          <div className="lyn-card">
            <h3>Per jenis permintaan</h3>
            <GroupTable rows={data.perJenis} labelHeader="Jenis permintaan" showDivision />
          </div>
        </>
      ) : (
        <div className="lyn-card">
          <TicketTable
            rows={tab === 'antrian' ? antrian : tab === 'terlambat' ? terlambat : data.rows}
            emptyText={tab === 'antrian' ? 'Tidak ada tiket dalam antrian.' : tab === 'terlambat' ? 'Tidak ada tiket yang lewat deadline.' : 'Belum ada tiket.'}
            onOpen={row => setOpenedId(row.id)}
          />
        </div>
      )}

      {openedId !== null && (
        <RequestDetailModal
          requestId={openedId}
          departmentNames={departmentNames}
          onClose={() => setOpenedId(null)}
          onChanged={() => void load()}
        />
      )}
    </section>
  )
}

export function TicketTable({ rows, emptyText, onOpen }: { rows: LynReportRow[]; emptyText: string; onOpen: (row: LynReportRow) => void }) {
  if (rows.length === 0) return <div className="lyn-empty">{emptyText}</div>

  return (
    <div className="table-container">
      <table className="sns-table">
        <thead>
          <tr>
            <th>No pengajuan</th>
            <th className="num">Antrian</th>
            <th>Divisi</th>
            <th>Jenis</th>
            <th>Pemohon</th>
            <th>Lokasi</th>
            <th>Prioritas</th>
            <th>Status</th>
            <th>Dibuat</th>
            <th>Deadline</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.id} className="sns-row-click" onClick={() => onOpen(r)} style={{ cursor: 'pointer' }}>
              <td className="sns-no">{r.requestNo ?? `#${r.id}`}</td>
              <td className="num">{r.queueNo ?? '-'}</td>
              <td>{r.divisionName}</td>
              <td>{r.jenisName}</td>
              <td>{r.requesterName}</td>
              <td>{r.location || '-'}</td>
              <td>
                <PriorityBadge priority={r.priority} />
              </td>
              <td>
                <StatusBadge status={r.status} />
              </td>
              <td>{formatDateTime(r.createdAt)}</td>
              <td className={r.terlambat ? 'pmt-late' : ''}>{formatDate(r.neededDate)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
