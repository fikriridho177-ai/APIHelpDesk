import { useEffect, useState } from 'react'

import { getJenisList } from '../layanan/api/layananApi'
import type { ExportTable } from '../layanan/exportUtils'
import { PRIORITY_CONFIG, STATUS_CONFIG, formatDate, formatDateTime } from '../layanan/layananConfig'
import type { LynDivisionOption, LynJenis, LynReportFilter, LynReportGroup, LynReportRow, LynStatus } from '../layanan/types/layanan'

// Kode menu halaman (Manajemen Menu). Hak Akses > Akses Menu menentukan siapa yang bisa membuka & export.
export const PMT_MENU = {
  approval: 'PMT_APPROVAL',
  monitoring: 'PMT_MONITORING',
  analisis: 'PMT_ANALISIS',
  masterJenis: 'PMT_MASTER_JENIS',
} as const

export function todayIso(offsetDays = 0) {
  const date = new Date()
  date.setDate(date.getDate() + offsetDays)
  const offset = date.getTimezoneOffset() * 60000
  return new Date(date.getTime() - offset).toISOString().slice(0, 10)
}

export const persenText = (value?: number | null) => (value == null ? '-' : `${value.toLocaleString('id-ID')}%`)
export const hariText = (value?: number | null) => (value == null ? '-' : `${value.toLocaleString('id-ID')} hari`)

// Pilihan filter divisi & jenis (divisi yang boleh dilihat user, jenis aktif).
export function useJenisOptions() {
  const [divisions, setDivisions] = useState<LynDivisionOption[]>([])
  const [jenis, setJenis] = useState<LynJenis[]>([])

  useEffect(() => {
    getJenisList()
      .then(data => {
        setDivisions(data.divisions)
        setJenis(data.items)
      })
      .catch(() => undefined)
  }, [])

  return { divisions, jenis }
}

/* ---------- Filter ---------- */

export function ReportFilterBar({
  value,
  onChange,
  divisions,
  jenis,
  showStatus = true,
}: {
  value: LynReportFilter
  onChange: (value: LynReportFilter) => void
  divisions: LynDivisionOption[]
  jenis: LynJenis[]
  showStatus?: boolean
}) {
  const set = (patch: Partial<LynReportFilter>) => onChange({ ...value, ...patch })
  const jenisOptions = jenis.filter(j => !value.divisionCode || j.divisionCode === value.divisionCode)

  return (
    <div className="sns-filter">
      <label className="lyn-field">
        <span>Divisi</span>
        <select value={value.divisionCode ?? ''} onChange={e => set({ divisionCode: e.target.value || undefined, jenisId: undefined })}>
          <option value="">Semua divisi</option>
          {divisions.map(d => (
            <option key={d.id} value={d.code}>
              {d.name}
            </option>
          ))}
        </select>
      </label>

      <label className="lyn-field">
        <span>Jenis permintaan</span>
        <select value={value.jenisId ?? ''} onChange={e => set({ jenisId: e.target.value ? Number(e.target.value) : undefined })}>
          <option value="">Semua jenis</option>
          {jenisOptions.map(j => (
            <option key={j.id} value={j.id}>
              {value.divisionCode ? j.name : `${j.divisionName} · ${j.name}`}
            </option>
          ))}
        </select>
      </label>

      <label className="lyn-field">
        <span>Dari tanggal</span>
        <input type="date" value={value.dateFrom ?? ''} onChange={e => set({ dateFrom: e.target.value || undefined })} />
      </label>

      <label className="lyn-field">
        <span>Sampai tanggal</span>
        <input type="date" value={value.dateTo ?? ''} onChange={e => set({ dateTo: e.target.value || undefined })} />
      </label>

      {showStatus && (
        <label className="lyn-field">
          <span>Status</span>
          <select value={value.status ?? ''} onChange={e => set({ status: e.target.value || undefined })}>
            <option value="">Semua status</option>
            {(Object.keys(STATUS_CONFIG) as LynStatus[]).map(s => (
              <option key={s} value={s}>
                {STATUS_CONFIG[s].label}
              </option>
            ))}
          </select>
        </label>
      )}
    </div>
  )
}

export function filterText(filter: LynReportFilter, divisions: LynDivisionOption[], jenis: LynJenis[]) {
  const division = divisions.find(d => d.code === filter.divisionCode)?.name ?? 'Semua divisi'
  const jenisName = jenis.find(j => j.id === filter.jenisId)?.name
  const status = filter.status ? STATUS_CONFIG[filter.status as LynStatus]?.label : null
  return [
    division,
    jenisName,
    status,
    `${formatDate(filter.dateFrom)} s/d ${formatDate(filter.dateTo)}`,
    `dicetak ${formatDateTime(new Date().toISOString())}`,
  ]
    .filter(Boolean)
    .join(' · ')
}

/* ---------- Tabel ringkasan ---------- */

export function GroupTable({ rows, labelHeader, showDivision }: { rows: LynReportGroup[]; labelHeader: string; showDivision?: boolean }) {
  if (rows.length === 0) return <p className="lyn-muted">Belum ada data.</p>

  return (
    <div className="table-container">
      <table className="sns-table compact">
        <thead>
          <tr>
            {showDivision && <th>Divisi</th>}
            <th>{labelHeader}</th>
            <th className="num">Total</th>
            <th className="num">Menunggu approval</th>
            <th className="num">Disetujui</th>
            <th className="num">Dalam proses</th>
            <th className="num">Selesai</th>
            <th className="num">Ditolak / batal</th>
            <th className="num">Lewat deadline</th>
            <th className="num">Rata-rata selesai</th>
            <th className="num">Tepat waktu</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={`${r.division}-${r.label}`}>
              {showDivision && <td>{r.division}</td>}
              <td>{labelHeader === 'Prioritas' ? PRIORITY_CONFIG[r.label as keyof typeof PRIORITY_CONFIG]?.label ?? r.label : r.label}</td>
              <td className="num">
                <b>{r.total}</b>
              </td>
              <td className="num">{r.menungguApproval}</td>
              <td className="num">{r.disetujui}</td>
              <td className="num">{r.dalamProses}</td>
              <td className="num">{r.selesai}</td>
              <td className="num">{r.ditolakBatal}</td>
              <td className={r.terlambat > 0 ? 'num pmt-late' : 'num'}>{r.terlambat}</td>
              <td className="num">{hariText(r.rataHariSelesai)}</td>
              <td className="num">{persenText(r.persenTepatWaktu)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/* ---------- Export ---------- */

export function groupExport(title: string, labelHeader: string, rows: LynReportGroup[], showDivision = false): ExportTable {
  return {
    title,
    header: [
      ...(showDivision ? ['Divisi'] : []),
      labelHeader,
      'Total',
      'Menunggu Approval',
      'Disetujui',
      'Dalam Proses',
      'Selesai',
      'Ditolak/Batal',
      'Lewat Deadline',
      'Rata-rata Selesai (hari)',
      'Tepat Waktu (%)',
    ],
    rows: rows.map(r => [
      ...(showDivision ? [r.division] : []),
      labelHeader === 'Prioritas' ? PRIORITY_CONFIG[r.label as keyof typeof PRIORITY_CONFIG]?.label ?? r.label : r.label,
      r.total,
      r.menungguApproval,
      r.disetujui,
      r.dalamProses,
      r.selesai,
      r.ditolakBatal,
      r.terlambat,
      r.rataHariSelesai ?? '',
      r.persenTepatWaktu ?? '',
    ]),
  }
}

// Baris tiket lengkap (Excel) atau ringkas (PDF, supaya muat di A4 landscape).
export function rowsExport(title: string, rows: LynReportRow[], compact = false): ExportTable {
  if (compact) {
    return {
      title,
      header: ['No Pengajuan', 'Antrian', 'Divisi', 'Jenis', 'Pemohon', 'Lokasi', 'Prioritas', 'Status', 'Dibuat', 'Deadline'],
      rows: rows.map(r => [
        r.requestNo,
        r.queueNo ?? '',
        r.divisionName,
        r.jenisName,
        r.requesterName,
        r.location,
        PRIORITY_CONFIG[r.priority]?.label ?? r.priority,
        (STATUS_CONFIG[r.status]?.label ?? r.status) + (r.terlambat ? ' (lewat deadline)' : ''),
        formatDateTime(r.createdAt),
        formatDate(r.neededDate),
      ]),
    }
  }

  return {
    title,
    header: [
      'No Pengajuan', 'Antrian', 'Divisi', 'Jenis Permintaan', 'Judul', 'Deskripsi', 'Lokasi', 'Prioritas', 'Status', 'Lewat Deadline',
      'Nama Pemohon', 'Jabatan', 'Departemen', 'Perusahaan', 'No HP', 'Dibuat', 'Deadline', 'Jadwal', 'Petugas', 'Selesai', 'Lama Selesai (hari)',
    ],
    rows: rows.map(r => [
      r.requestNo,
      r.queueNo ?? '',
      r.divisionName,
      r.jenisName,
      r.title,
      r.description,
      r.location,
      PRIORITY_CONFIG[r.priority]?.label ?? r.priority,
      STATUS_CONFIG[r.status]?.label ?? r.status,
      r.terlambat ? 'Ya' : '',
      r.requesterName,
      r.requesterPosition,
      r.requesterDepartment,
      r.requesterCompanies,
      r.requesterPhone,
      formatDateTime(r.createdAt),
      formatDate(r.neededDate),
      formatDateTime(r.scheduledAt),
      r.assignee,
      formatDateTime(r.closedAt),
      r.hariSelesai ?? '',
    ]),
  }
}

// Satu sheet per divisi (dipakai saat filter "Semua divisi").
export function rowsPerDivision(rows: LynReportRow[]): ExportTable[] {
  const names = Array.from(new Set(rows.map(r => r.divisionName))).sort()
  return names.map(name => rowsExport(`Data ${name}`, rows.filter(r => r.divisionName === name)))
}
