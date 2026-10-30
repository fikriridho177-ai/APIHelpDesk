import { useEffect, useMemo, useState } from 'react'

import { getSensusDetail, getSensusErrorMessage, runSensusAction } from '../api/sensusApi'
import { PERSIL_SUMBER, SNS_LOG_LABEL, formatLokasi, formatAngka, formatTanggal, formatWaktu, persen } from '../sensusConfig'
import type { SnsAction, SnsDetail } from '../types/sensus'
import { SnsStatusBadge, TingkatBadge } from './SensusParts'

type Props = {
  sensusId: number
  onClose: () => void
  onChanged?: () => void
}

const ACTIONS: { action: SnsAction; label: string; tone: string; noteRequired: boolean }[] = [
  { action: 'APPROVE', label: 'Setujui', tone: 'success', noteRequired: false },
  { action: 'REVISI', label: 'Minta Revisi', tone: 'neutral', noteRequired: true },
  { action: 'REJECT', label: 'Tolak', tone: 'danger', noteRequired: true },
]

export default function SensusDetailModal({ sensusId, onClose, onChanged }: Props) {
  const [detail, setDetail] = useState<SnsDetail | null>(null)
  const [error, setError] = useState('')
  const [note, setNote] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    getSensusDetail(sensusId)
      .then(setDetail)
      .catch(err => setError(getSensusErrorMessage(err, 'Gagal memuat detail sensus.')))
  }, [sensusId])

  // Nilai per pokok sampel ditampilkan sebagai tabel: baris = no pokok, kolom = parameter.
  const pokokTable = useMemo(() => {
    if (!detail) return null
    const perPokok = detail.details.filter(d => d.noPokok != null)
    if (perPokok.length === 0) return null

    const params = Array.from(new Map(perPokok.map(d => [d.parameterId, d.parameterName])).entries())
    const pokok = Array.from(new Set(perPokok.map(d => d.noPokok!))).sort((a, b) => a - b)
    const value = (no: number, paramId: number) => {
      const d = perPokok.find(x => x.noPokok === no && x.parameterId === paramId)
      return d ? d.nilaiTeks || formatAngka(d.nilaiAngka) : '-'
    }

    return { params, pokok, value }
  }, [detail])

  const blokValues = detail?.details.filter(d => d.noPokok == null) ?? []

  async function submit(action: SnsAction, noteRequired: boolean) {
    if (noteRequired && !note.trim()) {
      setError('Catatan wajib diisi untuk menolak / meminta revisi.')
      return
    }
    if (action !== 'APPROVE' && !confirm(`Yakin ${action === 'REJECT' ? 'menolak' : 'meminta revisi'} sensus ini?`)) return

    setSaving(true)
    setError('')
    try {
      setDetail(await runSensusAction(sensusId, action, note.trim() || undefined))
      setNote('')
      onChanged?.()
    } catch (err) {
      setError(getSensusErrorMessage(err, 'Gagal memproses sensus.'))
    } finally {
      setSaving(false)
    }
  }

  const s = detail?.summary

  return (
    <div className="lyn-modal-backdrop" onClick={onClose}>
      <div className="lyn-modal sns-modal" onClick={e => e.stopPropagation()}>
        {!detail || !s ? (
          <div className="lyn-empty">{error || 'Memuat detail...'}</div>
        ) : (
          <>
            <div className="lyn-modal-header">
              <div>
                <span className="lyn-request-no">{s.sensusNo}</span>
                <h2>{s.jenisName}</h2>
                <p>
                  {formatLokasi(s)} · {PERSIL_SUMBER[s.persilSumber]?.label ?? s.persilSumber} (Persil ID {s.persilId})
                </p>
              </div>
              <div className="lyn-modal-header-side">
                <SnsStatusBadge status={s.status} />
                <button type="button" className="lyn-icon-button" onClick={onClose} aria-label="Tutup">
                  ✕
                </button>
              </div>
            </div>

            <div className="lyn-modal-body">
              <div className="lyn-info-grid">
                <Info label="Tanggal sensus" value={formatTanggal(s.tanggalSensus)} />
                <Info label="Petugas" value={s.petugasName} />
                <Info label="Dikirim" value={formatWaktu(s.submittedAt)} />
                <Info label="Pokok sampel" value={s.jumlahPokokSampel} />
                <Info
                  label="Pokok terserang"
                  value={`${s.jumlahPokokTerserang} (${persen(s.jumlahPokokTerserang, s.jumlahPokokSampel)}%)`}
                />
                <Info label="Tingkat serangan" value={<TingkatBadge tingkat={s.tingkat} />} />
                <Info
                  label="Koordinat"
                  value={
                    detail.latitude != null && detail.longitude != null ? (
                      <a href={`https://www.google.com/maps?q=${detail.latitude},${detail.longitude}`} target="_blank" rel="noreferrer">
                        {detail.latitude}, {detail.longitude}
                      </a>
                    ) : (
                      '-'
                    )
                  }
                />
                <Info label="Perangkat" value={detail.deviceInfo || '-'} />
                {detail.approvedByName && <Info label="Diproses oleh" value={`${detail.approvedByName}, ${formatWaktu(s.approvedAt)}`} />}
              </div>

              {detail.catatan && (
                <div className="lyn-description">
                  <h4>Catatan petugas</h4>
                  <p>{detail.catatan}</p>
                </div>
              )}

              {detail.approvalNote && (
                <div className="lyn-description">
                  <h4>Catatan approval</h4>
                  <p>{detail.approvalNote}</p>
                </div>
              )}

              {/* RINGKASAN PARAMETER */}
              <h4 className="sns-subtitle">Ringkasan parameter</h4>
              <div className="table-container">
                <table className="sns-table compact">
                  <thead>
                    <tr>
                      <th>Parameter</th>
                      <th className="num">Rata-rata</th>
                      <th className="num">Total</th>
                      <th className="num">Jumlah data</th>
                      <th>Tingkat</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.ringkasan.map(r => (
                      <tr key={r.parameterId}>
                        <td>
                          {r.parameterName} {r.satuan && <small className="lyn-muted">({r.satuan})</small>}
                          {r.isIndikator && <span className="sns-tag">indikator</span>}
                        </td>
                        <td className="num">{formatAngka(r.rata2)}</td>
                        <td className="num">{formatAngka(r.total)}</td>
                        <td className="num">{r.jumlahData}</td>
                        <td>{r.isIndikator ? <TingkatBadge tingkat={r.tingkat} /> : '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {blokValues.length > 0 && (
                <>
                  <h4 className="sns-subtitle">Nilai per persil</h4>
                  <div className="lyn-info-grid">
                    {blokValues.map(d => (
                      <Info
                        key={d.id}
                        label={d.parameterName}
                        value={`${d.nilaiTeks || formatAngka(d.nilaiAngka)}${d.satuan && !d.nilaiTeks ? ` ${d.satuan}` : ''}`}
                      />
                    ))}
                  </div>
                </>
              )}

              {pokokTable && (
                <>
                  <h4 className="sns-subtitle">Data per pokok sampel ({pokokTable.pokok.length} pokok)</h4>
                  <div className="table-container sns-scroll">
                    <table className="sns-table compact">
                      <thead>
                        <tr>
                          <th>No pokok</th>
                          {pokokTable.params.map(([id, name]) => (
                            <th key={id}>{name}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {pokokTable.pokok.map(no => (
                          <tr key={no}>
                            <td>{no}</td>
                            {pokokTable.params.map(([id]) => (
                              <td key={id}>{pokokTable.value(no, id)}</td>
                            ))}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}

              {detail.fotos.length > 0 && (
                <>
                  <h4 className="sns-subtitle">Foto lapangan</h4>
                  <div className="sns-photos">
                    {detail.fotos.map(f => (
                      <a key={f.id} href={f.fileUrl} target="_blank" rel="noreferrer" title={f.keterangan ?? ''}>
                        <img src={f.fileUrl} alt={f.keterangan ?? 'Foto sensus'} loading="lazy" />
                      </a>
                    ))}
                  </div>
                </>
              )}

              {/* APPROVAL */}
              {detail.canApprove && (
                <div className="lyn-card sns-approval-box">
                  <label className="lyn-field">
                    <span>Catatan approval</span>
                    <textarea
                      rows={2}
                      value={note}
                      onChange={e => setNote(e.target.value)}
                      placeholder="Wajib diisi bila menolak / meminta revisi"
                    />
                  </label>
                  {error && <p className="error">{error}</p>}
                  <div className="lyn-form-actions">
                    {ACTIONS.map(a => (
                      <button
                        key={a.action}
                        type="button"
                        className={`lyn-button ${a.tone}`}
                        disabled={saving}
                        onClick={() => submit(a.action, a.noteRequired)}
                      >
                        {a.label}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* RIWAYAT */}
              <h4 className="sns-subtitle">Riwayat</h4>
              <ul className="sns-log">
                {detail.logs.map(log => (
                  <li key={log.id}>
                    <b>{SNS_LOG_LABEL[log.action] ?? log.action}</b> · {log.userName} · {formatWaktu(log.createdAt)}
                    {log.note && <p>{log.note}</p>}
                  </li>
                ))}
              </ul>
            </div>
          </>
        )}
      </div>
    </div>
  )
}

function Info({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="lyn-info-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}
