import { useMemo, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { exportExcel, exportPdf, type ExportTable } from '../exportUtils'
import { PRIORITY_CONFIG, STATUS_CONFIG, formatDateTime } from '../layananConfig'
import type { LynRequestItem, LynStatus } from '../types/layanan'
import { PriorityBadge, StatusBadge } from './Badges'

type Props = {
  requests: LynRequestItem[]
  loading?: boolean
  currentUserId: number
  showService?: boolean
  canExport?: boolean
  title?: string
  onOpen: (request: LynRequestItem) => void
}

type Scope = 'all' | 'mine' | 'others'

// Daftar yang sedang tampil -> tabel export Excel / PDF (permission Export).
function toTable(title: string, rows: LynRequestItem[]): ExportTable {
  return {
    title,
    header: ['No Pengajuan', 'Antrian', 'Divisi', 'Jenis Permintaan', 'Judul', 'Pemohon', 'Prioritas', 'Status', 'Dibuat', 'Jadwal'],
    rows: rows.map(r => [
      r.requestNo,
      r.queueNo ?? '',
      r.divisionName,
      r.serviceName,
      r.title,
      r.requesterName,
      PRIORITY_CONFIG[r.priority]?.label ?? r.priority,
      STATUS_CONFIG[r.status]?.label ?? r.status,
      formatDateTime(r.createdAt),
      formatDateTime(r.scheduledAt),
    ]),
  }
}

export default function RequestList({ requests, loading, currentUserId, showService, canExport, title = 'Daftar Pengajuan', onOpen }: Props) {
  const [status, setStatus] = useState<LynStatus | ''>('')
  const [scope, setScope] = useState<Scope>('all')
  const [search, setSearch] = useState('')

  const hasOthers = requests.some(r => r.requesterUserId !== currentUserId)

  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase()

    return requests.filter(r => {
      if (status && r.status !== status) return false
      if (scope === 'mine' && r.requesterUserId !== currentUserId) return false
      if (scope === 'others' && r.requesterUserId === currentUserId) return false
      if (!keyword) return true
      return `${r.requestNo} ${r.title} ${r.requesterName} ${r.serviceName}`.toLowerCase().includes(keyword)
    })
  }, [requests, status, scope, search, currentUserId])

  // Jumlah per status untuk chip filter.
  const countByStatus = useMemo(() => {
    const counts: Partial<Record<LynStatus, number>> = {}
    requests.forEach(r => (counts[r.status] = (counts[r.status] ?? 0) + 1))
    return counts
  }, [requests])

  return (
    <div className="lyn-card">
      <div className="lyn-card-header">
        <div>
          <h3>{title}</h3>
          <p>{requests.length} pengajuan</p>
        </div>

        <div className="lyn-list-tools">
          <label className="lyn-search">
            <DashboardIcon name="search" />
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Cari no, judul, pemohon..." />
          </label>

          {hasOthers && (
            <select value={scope} onChange={e => setScope(e.target.value as Scope)}>
              <option value="all">Semua</option>
              <option value="mine">Pengajuan saya</option>
              <option value="others">Pengajuan masuk</option>
            </select>
          )}

          {canExport && filtered.length > 0 && (
            <>
              <button type="button" className="lyn-button ghost" onClick={() => exportExcel('pengajuan-layanan', [toTable(title, filtered)])}>
                <DashboardIcon name="download" /> Excel
              </button>
              <button
                type="button"
                className="lyn-button ghost"
                onClick={() => void exportPdf('pengajuan-layanan', title, `${filtered.length} pengajuan · dicetak ${formatDateTime(new Date().toISOString())}`, [toTable(title, filtered)])}
              >
                <DashboardIcon name="download" /> PDF
              </button>
            </>
          )}
        </div>
      </div>

      <div className="lyn-status-filter">
        <button type="button" className={status === '' ? 'lyn-chip active' : 'lyn-chip'} onClick={() => setStatus('')}>
          Semua <b>{requests.length}</b>
        </button>
        {(Object.keys(STATUS_CONFIG) as LynStatus[])
          .filter(s => countByStatus[s])
          .map(s => (
            <button
              key={s}
              type="button"
              className={status === s ? `lyn-chip tone-${STATUS_CONFIG[s].tone} active` : 'lyn-chip'}
              onClick={() => setStatus(s)}
            >
              {STATUS_CONFIG[s].label} <b>{countByStatus[s]}</b>
            </button>
          ))}
      </div>

      {loading ? (
        <div className="lyn-empty">Memuat pengajuan...</div>
      ) : filtered.length === 0 ? (
        <div className="lyn-empty">
          <DashboardIcon name="inbox" />
          <p>Belum ada pengajuan.</p>
        </div>
      ) : (
        <div className="lyn-request-list">
          {filtered.map(r => (
            <button key={r.id} type="button" className="lyn-request-row" onClick={() => onOpen(r)}>
              <div className="lyn-request-main">
                <div className="lyn-request-meta">
                  <span className="lyn-request-no">{r.requestNo ?? `#${r.id}`}</span>
                  {r.queueNo != null && <span className="lyn-queue">Antrian ke-{r.queueNo}</span>}
                  {showService && (
                    <span className="lyn-request-service">
                      {r.divisionName ? `${r.divisionName} · ` : ''}
                      {r.serviceName}
                    </span>
                  )}
                </div>
                <strong>{r.title}</strong>
                <small>
                  {r.requesterUserId === currentUserId ? 'Anda' : r.requesterName} · {formatDateTime(r.createdAt)}
                  {r.scheduledAt && <> · Jadwal {formatDateTime(r.scheduledAt)}</>}
                </small>
              </div>

              <div className="lyn-request-side">
                <StatusBadge status={r.status} />
                <PriorityBadge priority={r.priority} />
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
