import type { ReactNode } from 'react'

import { SNS_STATUS, SNS_TINGKAT } from '../sensusConfig'
import type { SnsFilter, SnsJenis, SnsStatus, SnsTingkat } from '../types/sensus'

/* ---------- Badge ---------- */

export function SnsStatusBadge({ status }: { status: SnsStatus }) {
  const config = SNS_STATUS[status] ?? { label: status, tone: 'gray' }
  return <span className={`lyn-badge tone-${config.tone}`}>{config.label}</span>
}

export function TingkatBadge({ tingkat }: { tingkat: SnsTingkat }) {
  const config = SNS_TINGKAT[tingkat] ?? SNS_TINGKAT['-']
  return <span className={`lyn-priority tone-${config.tone}`}>{config.label}</span>
}

/* ---------- Header halaman ---------- */

export function SensusHero({ title, subtitle, children }: { title: string; subtitle: string; children?: ReactNode }) {
  return (
    <header className="lyn-hero compact">
      <div>
        {/* <span className="lyn-hero-eyebrow">Sensus RNA</span> */}
        <h1>{title}</h1>
        <p>{subtitle}</p>
      </div>
      {children}
    </header>
  )
}

export function NoAccess({ loaded }: { loaded: boolean }) {
  return (
    <div className="lyn-empty">
      {loaded ? 'Anda tidak memiliki akses ke halaman ini. Hubungi administrator untuk pengaturan Hak Akses.' : 'Memuat...'}
    </div>
  )
}

/* ---------- Kotak angka ---------- */

export function StatTiles({ items }: { items: { label: string; value: ReactNode; tone: string }[] }) {
  return (
    <div className="sns-stats">
      {items.map(item => (
        <div key={item.label} className={`sns-stat tone-${item.tone}`}>
          <strong>{item.value}</strong>
          <small>{item.label}</small>
        </div>
      ))}
    </div>
  )
}

/* ---------- Grafik batang sederhana (tanpa library) ---------- */

export function BarList({
  rows,
  unit = '',
  emptyText = 'Belum ada data.',
}: {
  rows: { label: string; value: number; tone?: string; hint?: string }[]
  unit?: string
  emptyText?: string
}) {
  if (rows.length === 0) return <p className="lyn-muted">{emptyText}</p>
  const max = Math.max(...rows.map(r => r.value), 1)

  return (
    <div className="sns-bars">
      {rows.map(row => (
        <div key={row.label} className="sns-bar-row" title={row.hint}>
          <span className="sns-bar-label">{row.label}</span>
          <span className="sns-bar-track">
            <span className={`sns-bar-fill tone-${row.tone ?? 'blue'}`} style={{ width: `${(row.value / max) * 100}%` }} />
          </span>
          <span className="sns-bar-value">
            {row.value.toLocaleString('id-ID')}
            {unit}
          </span>
        </div>
      ))}
    </div>
  )
}

/* ---------- Filter ---------- */

type FilterProps = {
  value: SnsFilter
  onChange: (value: SnsFilter) => void
  wilayah: string[]
  jenis: SnsJenis[]
  showStatus?: boolean
  showSearch?: boolean
  requireJenis?: boolean
  // Halaman sensus per jenis: jenis sudah tetap, pilihan jenis disembunyikan.
  hideJenis?: boolean
  children?: ReactNode
}

export function SensusFilterBar({ value, onChange, wilayah, jenis, showStatus, showSearch, requireJenis, hideJenis, children }: FilterProps) {
  const set = (patch: Partial<SnsFilter>) => onChange({ ...value, ...patch })
  const toNumber = (v: string) => (v ? Number(v) : undefined)

  return (
    <div className="sns-filter">
      {!hideJenis && (
      <label className="lyn-field">
        <span>Jenis sensus</span>
        <select value={value.jenisId ?? ''} onChange={e => set({ jenisId: toNumber(e.target.value) })}>
          {!requireJenis && <option value="">Semua jenis</option>}
          {jenis.map(j => (
            <option key={j.id} value={j.id}>
              {j.name}
            </option>
          ))}
        </select>
      </label>
      )}

      <label className="lyn-field">
        <span>Wilayah</span>
        <select value={value.wilayah ?? ''} onChange={e => set({ wilayah: e.target.value || undefined })}>
          <option value="">Semua wilayah</option>
          {wilayah.map(w => (
            <option key={w} value={w}>
              {w}
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
            {(Object.keys(SNS_STATUS) as SnsStatus[]).map(s => (
              <option key={s} value={s}>
                {SNS_STATUS[s].label}
              </option>
            ))}
          </select>
        </label>
      )}

      {showSearch && (
        <label className="lyn-field">
          <span>Cari</span>
          <input
            value={value.search ?? ''}
            onChange={e => set({ search: e.target.value || undefined })}
            placeholder="No sensus, persil, petugas"
          />
        </label>
      )}

      {children}
    </div>
  )
}
