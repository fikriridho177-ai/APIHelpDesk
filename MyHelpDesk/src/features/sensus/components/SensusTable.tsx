import { PERSIL_SUMBER, formatTanggal, persen } from '../sensusConfig'
import type { SnsListItem } from '../types/sensus'
import { SnsStatusBadge, TingkatBadge } from './SensusParts'

type Props = {
  rows: SnsListItem[]
  loading?: boolean
  onOpen: (row: SnsListItem) => void
  // Bila diisi, tampil kolom checkbox (untuk approve sekaligus).
  selected?: Set<number>
  onSelectedChange?: (selected: Set<number>) => void
}

export default function SensusTable({ rows, loading, onOpen, selected, onSelectedChange }: Props) {
  const selectable = !!selected && !!onSelectedChange
  const selectableRows = rows.filter(r => r.status === 'SUBMITTED')
  const allChecked = selectable && selectableRows.length > 0 && selectableRows.every(r => selected!.has(r.id))

  const toggle = (id: number) => {
    const next = new Set(selected)
    if (next.has(id)) next.delete(id)
    else next.add(id)
    onSelectedChange!(next)
  }

  const toggleAll = () => onSelectedChange!(allChecked ? new Set() : new Set(selectableRows.map(r => r.id)))

  if (loading) return <div className="lyn-empty">Memuat data sensus...</div>
  if (rows.length === 0) return <div className="lyn-empty">Tidak ada data sensus untuk filter ini.</div>

  return (
    <div className="table-container">
      <table className="sns-table">
        <thead>
          <tr>
            {selectable && (
              <th className="sns-check">
                <input type="checkbox" checked={allChecked} onChange={toggleAll} aria-label="Pilih semua" />
              </th>
            )}
            <th>No Sensus</th>
            <th>Tanggal</th>
            <th>Jenis</th>
            <th>Wilayah / Afdeling / Persil</th>
            <th>Petugas</th>
            <th className="num">Sampel</th>
            <th className="num">Terserang</th>
            <th>Tingkat</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.id} className="sns-row" onClick={() => onOpen(r)}>
              {selectable && (
                <td className="sns-check" onClick={e => e.stopPropagation()}>
                  <input
                    type="checkbox"
                    disabled={r.status !== 'SUBMITTED'}
                    checked={selected!.has(r.id)}
                    onChange={() => toggle(r.id)}
                    aria-label={`Pilih ${r.sensusNo}`}
                  />
                </td>
              )}
              <td className="sns-no">{r.sensusNo ?? `#${r.id}`}</td>
              <td>{formatTanggal(r.tanggalSensus)}</td>
              <td>{r.jenisName}</td>
              <td>
                {[r.kodeWilayah, r.kodeAfdeling].filter(Boolean).join(' / ')}
                {(r.kodeWilayah || r.kodeAfdeling) && ' / '}
                <b>{r.persilKode || r.persilId}</b>
                <small className="lyn-muted"> · {PERSIL_SUMBER[r.persilSumber]?.label ?? r.persilSumber}</small>
              </td>
              <td>{r.petugasName}</td>
              <td className="num">{r.jumlahPokokSampel}</td>
              <td className="num">
                {r.jumlahPokokTerserang} <small className="lyn-muted">({persen(r.jumlahPokokTerserang, r.jumlahPokokSampel)}%)</small>
              </td>
              <td>
                <TingkatBadge tingkat={r.tingkat} />
              </td>
              <td>
                <SnsStatusBadge status={r.status} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
