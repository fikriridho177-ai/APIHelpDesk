import { useEffect, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { getSensusAnalisis, getSensusErrorMessage } from '../api/sensusApi'
import { PERSIL_SUMBER, SNS_TINGKAT, downloadCsv, formatAngka, formatTanggal } from '../sensusConfig'
import type { SnsAnalisis, SnsFilter, SnsTingkat } from '../types/sensus'
import { BarList, StatTiles, TingkatBadge } from './SensusParts'

const TINGKAT_ORDER: SnsTingkat[] = ['BERAT', 'SEDANG', 'RINGAN', 'NIHIL', '-']

type Props = {
  filter: SnsFilter
  canExport: boolean
}

// Isi analisis (dipakai halaman Analisis Sensus & halaman sensus per jenis). Hanya data APPROVED.
export default function AnalisisView({ filter, canExport }: Props) {
  const [data, setData] = useState<SnsAnalisis | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [tingkatFilter, setTingkatFilter] = useState<SnsTingkat | ''>('')

  useEffect(() => {
    if (!filter.jenisId) return
    setLoading(true)
    setError('')

    getSensusAnalisis({ ...filter, jenisId: filter.jenisId })
      .then(setData)
      .catch(err => setError(getSensusErrorMessage(err, 'Gagal memuat analisis.')))
      .finally(() => setLoading(false))
  }, [filter])

  if (!filter.jenisId) return <div className="lyn-empty">Pilih jenis sensus.</div>
  if (error) return <div className="lyn-empty">{error}</div>

  const perPersil = data?.perPersil.filter(b => !tingkatFilter || b.tingkat === tingkatFilter) ?? []
  const satuan = data?.indikatorSatuan ? ` ${data.indikatorSatuan}` : ''

  function exportData() {
    if (!data) return
    downloadCsv(
      `analisis-${data.jenisName.toLowerCase().replace(/\s+/g, '-')}`,
      ['Sumber', 'Wilayah', 'Afdeling', 'Kode Persil', 'Persil ID', 'Jumlah Sensus', 'Terakhir', 'Pokok Sampel', 'Pokok Terserang', '% Terserang', `Rata-rata ${data.indikatorName ?? 'Indikator'}`, 'Tingkat'],
      perPersil.map(b => [
        PERSIL_SUMBER[b.persilSumber]?.label ?? b.persilSumber, b.kodeWilayah ?? '', b.kodeAfdeling ?? '', b.persilKode, b.persilId, b.jumlahSensus, b.terakhirSensus, b.totalPokokSampel,
        b.totalPokokTerserang, b.persenTerserang, b.rataIndikator ?? '', SNS_TINGKAT[b.tingkat].label,
      ]),
    )
  }

  if (loading || !data) return <div className="lyn-empty">Memuat analisis...</div>

  return (
    <>
      <div className="lyn-card">
        {data.indikatorName ? (
          <p className="lyn-muted">
            Indikator: <b>{data.indikatorName}</b>
            {satuan} · Sedang ≥ {formatAngka(data.ambangSedang)}
            {satuan} · Berat ≥ {formatAngka(data.ambangBerat)}
            {satuan}. Ambang batas dapat diubah di Master Jenis &amp; Parameter.
          </p>
        ) : (
          <p className="lyn-muted">Jenis sensus ini belum punya parameter indikator. Atur di Master Jenis &amp; Parameter.</p>
        )}
      </div>

      <StatTiles
        items={[
          { label: 'Sensus disetujui', value: data.jumlahSensus, tone: 'indigo' },
          { label: 'Persil tersensus', value: data.jumlahPersil, tone: 'blue' },
          { label: 'Rata-rata pokok terserang', value: `${formatAngka(data.persenTerserang)}%`, tone: 'orange' },
          { label: 'Persil tingkat berat', value: data.perPersil.filter(b => b.tingkat === 'BERAT').length, tone: 'red' },
        ]}
      />

      <div className="sns-grid-2">
        <div className="lyn-card">
          <h3>Sebaran tingkat serangan (per sensus)</h3>
          <BarList
            rows={TINGKAT_ORDER.filter(t => t !== '-' || data.sebaranTingkat['-'] > 0).map(t => ({
              label: SNS_TINGKAT[t].label === '-' ? 'Tanpa indikator' : SNS_TINGKAT[t].label,
              value: data.sebaranTingkat[t] ?? 0,
              tone: SNS_TINGKAT[t].tone,
            }))}
          />
        </div>

        <div className="lyn-card">
          <h3>Tren bulanan</h3>
          {data.tren.length === 0 ? (
            <p className="lyn-muted">Belum ada data.</p>
          ) : (
            <div className="table-container">
              <table className="sns-table compact">
                <thead>
                  <tr>
                    <th>Bulan</th>
                    <th className="num">Sensus</th>
                    <th className="num">% Terserang</th>
                    <th className="num">Rata-rata indikator</th>
                  </tr>
                </thead>
                <tbody>
                  {data.tren.map(t => (
                    <tr key={t.periode}>
                      <td>{formatBulan(t.periode)}</td>
                      <td className="num">{t.jumlahSensus}</td>
                      <td className="num">{formatAngka(t.persenTerserang)}%</td>
                      <td className="num">
                        {formatAngka(t.rataIndikator)}
                        {t.rataIndikator != null && satuan}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <div className="lyn-card">
        <div className="sns-toolbar">
          <h3>Per persil ({perPersil.length})</h3>
          <div className="sns-toolbar-right">
            <select value={tingkatFilter} onChange={e => setTingkatFilter(e.target.value as SnsTingkat | '')}>
              <option value="">Semua tingkat</option>
              {TINGKAT_ORDER.filter(t => t !== '-').map(t => (
                <option key={t} value={t}>
                  {SNS_TINGKAT[t].label}
                </option>
              ))}
            </select>
            {canExport && perPersil.length > 0 && (
              <button type="button" className="lyn-button ghost" onClick={exportData}>
                <DashboardIcon name="download" /> Export
              </button>
            )}
          </div>
        </div>

        {perPersil.length === 0 ? (
          <div className="lyn-empty">Tidak ada data sensus yang disetujui untuk filter ini.</div>
        ) : (
          <div className="table-container">
            <table className="sns-table">
              <thead>
                <tr>
                  <th>Wilayah / Afdeling</th>
                  <th>Persil</th>
                  <th className="num">Sensus</th>
                  <th>Terakhir</th>
                  <th className="num">Pokok sampel</th>
                  <th className="num">Terserang</th>
                  <th className="num">% Terserang</th>
                  <th className="num">Rata-rata {data.indikatorName ?? 'indikator'}</th>
                  <th>Tingkat</th>
                </tr>
              </thead>
              <tbody>
                {perPersil.map(b => (
                  <tr key={`${b.persilSumber}-${b.persilId}`}>
                    <td>{[b.kodeWilayah, b.kodeAfdeling].filter(Boolean).join(' / ') || '-'}</td>
                    <td>
                      <b>{b.persilKode || b.persilId}</b>
                      <small className="lyn-muted"> · {PERSIL_SUMBER[b.persilSumber]?.label ?? b.persilSumber}</small>
                    </td>
                    <td className="num">{b.jumlahSensus}</td>
                    <td>{formatTanggal(b.terakhirSensus)}</td>
                    <td className="num">{b.totalPokokSampel}</td>
                    <td className="num">{b.totalPokokTerserang}</td>
                    <td className="num">
                      <span className="sns-percent">
                        <span style={{ width: `${Math.min(100, b.persenTerserang)}%` }} />
                      </span>
                      {formatAngka(b.persenTerserang)}%
                    </td>
                    <td className="num">{formatAngka(b.rataIndikator)}</td>
                    <td>
                      <TingkatBadge tingkat={b.tingkat} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  )
}

function formatBulan(periode: string) {
  const [y, m] = periode.split('-').map(Number)
  return new Date(y, m - 1, 1).toLocaleDateString('id-ID', { month: 'long', year: 'numeric' })
}
