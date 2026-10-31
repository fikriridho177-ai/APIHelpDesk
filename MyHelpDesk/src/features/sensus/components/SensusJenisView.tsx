import { useCallback, useEffect, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { bulkApproveSensus, getSensusErrorMessage, getSensusList, getSensusWilayah } from '../api/sensusApi'
import { PERSIL_SUMBER, SNS_STATUS, downloadCsv, formatAngka, todayIso } from '../sensusConfig'
import type { SnsFilter, SnsJenisPage, SnsListItem } from '../types/sensus'
import AnalisisView from './AnalisisView'
import SensusDetailModal from './SensusDetailModal'
import { SensusFilterBar, StatTiles } from './SensusParts'
import SensusTable from './SensusTable'
import '../sensus.css'

type Tab = 'data' | 'analisis' | 'parameter'

const TIPE_LABEL: Record<string, string> = { NUMBER: 'Angka', TEXT: 'Teks', BOOLEAN: 'Ya / Tidak', OPTION: 'Pilihan' }

// Isi halaman sensus per jenis (Sensus Plasispa, Ulat Api, Kumbang Tanduk, Ganoderma, Penyakit Lainnya, Kelapa, Nanas).
// Data diinput Asisten Lapangan lewat Android; di web: lihat data, approval, analisis, dan daftar parameter.
export default function SensusJenisView({ page }: { page: SnsJenisPage }) {
  const { jenis, canRead, canApprove, canExport } = page

  const [tab, setTab] = useState<Tab>('data')
  const [wilayah, setWilayah] = useState<string[]>([])
  const [filter, setFilter] = useState<SnsFilter>({ jenisId: jenis.id, dateFrom: todayIso(-90), dateTo: todayIso() })
  const [rows, setRows] = useState<SnsListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [openedId, setOpenedId] = useState<number | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    getSensusWilayah().then(setWilayah).catch(() => setWilayah([]))
  }, [])

  // Ganti halaman (jenis lain) -> reset filter.
  useEffect(() => {
    setFilter(f => ({ ...f, jenisId: jenis.id }))
    setTab('data')
  }, [jenis.id])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setRows(await getSensusList(filter))
      setSelected(new Set())
    } catch (error) {
      console.error('Gagal mengambil data sensus:', error)
    } finally {
      setLoading(false)
    }
  }, [filter])

  useEffect(() => {
    if (!canRead || tab !== 'data') return
    const timer = setTimeout(() => void load(), 300)
    return () => clearTimeout(timer)
  }, [canRead, tab, load])

  async function approveSelected() {
    if (!confirm(`Setujui ${selected.size} data sensus terpilih?`)) return
    setSaving(true)
    try {
      const result = await bulkApproveSensus(Array.from(selected))
      alert(`${result.approved} sensus disetujui${result.skipped ? `, ${result.skipped} dilewati` : ''}.`)
      await load()
    } catch (error) {
      alert(getSensusErrorMessage(error, 'Gagal menyetujui sensus.'))
    } finally {
      setSaving(false)
    }
  }

  function exportData() {
    downloadCsv(
      `sensus-${jenis.code.toLowerCase()}`,
      ['No Sensus', 'Tanggal', 'Sumber Persil', 'Wilayah', 'Afdeling', 'Kode Persil', 'Persil ID', 'Petugas', 'Sampel', 'Terserang', 'Tingkat', 'Status'],
      rows.map(r => [
        r.sensusNo, r.tanggalSensus, PERSIL_SUMBER[r.persilSumber]?.label ?? r.persilSumber, r.kodeWilayah ?? '', r.kodeAfdeling ?? '', r.persilKode, r.persilId, r.petugasName,
        r.jumlahPokokSampel, r.jumlahPokokTerserang, r.tingkat, SNS_STATUS[r.status]?.label ?? r.status,
      ]),
    )
  }

  if (!canRead) {
    return (
      <div className="lyn-card lyn-info-card">
        <DashboardIcon name="info" />
        <p>
          Sensus {jenis.name} diinput oleh Asisten Lapangan melalui aplikasi Android. Anda belum memiliki akses Read untuk
          melihat datanya — hubungi administrator (Hak Akses &gt; Akses Menu).
        </p>
      </div>
    )
  }

  const count = (status: string) => rows.filter(r => r.status === status).length

  return (
    <>
      <div className="lyn-card lyn-info-card">
        <DashboardIcon name="info" />
        <p>
          Data sensus diinput oleh Asisten Lapangan melalui aplikasi Android.
          {canApprove ? ' Di halaman ini Anda dapat memeriksa, menyetujui, dan menganalisis hasilnya.' : ' Di halaman ini Anda dapat memantau dan menganalisis hasilnya.'}
        </p>
      </div>

      <div className="sns-tabs">
        {(
          [
            ['data', 'Data Sensus'],
            ['analisis', 'Analisis'],
            ['parameter', `Parameter (${jenis.parameters.length})`],
          ] as [Tab, string][]
        ).map(([key, label]) => (
          <button key={key} type="button" className={tab === key ? 'lyn-chip active' : 'lyn-chip'} onClick={() => setTab(key)}>
            {label}
          </button>
        ))}
      </div>

      {tab !== 'parameter' && (
        <div className="lyn-card">
          <SensusFilterBar
            value={filter}
            onChange={value => setFilter({ ...value, jenisId: jenis.id })}
            wilayah={wilayah}
            jenis={[jenis]}
            hideJenis
            showStatus={tab === 'data'}
            showSearch={tab === 'data'}
          />
        </div>
      )}

      {tab === 'data' && (
        <>
          <StatTiles
            items={[
              { label: 'Data ditampilkan', value: rows.length, tone: 'indigo' },
              { label: 'Menunggu approval', value: count('SUBMITTED'), tone: 'blue' },
              { label: 'Disetujui', value: count('APPROVED'), tone: 'green' },
              { label: 'Tingkat berat', value: rows.filter(r => r.tingkat === 'BERAT').length, tone: 'red' },
            ]}
          />

          <div className="lyn-card">
            <div className="sns-toolbar">
              <span className="lyn-muted">{rows.length} data · klik baris untuk melihat detail</span>
              <div className="sns-toolbar-right">
                {canExport && rows.length > 0 && (
                  <button type="button" className="lyn-button ghost" onClick={exportData}>
                    <DashboardIcon name="download" /> Export
                  </button>
                )}
                {canApprove && (
                  <button type="button" className="lyn-button success" disabled={selected.size === 0 || saving} onClick={approveSelected}>
                    Setujui terpilih ({selected.size})
                  </button>
                )}
              </div>
            </div>

            <SensusTable
              rows={rows}
              loading={loading}
              onOpen={row => setOpenedId(row.id)}
              selected={canApprove ? selected : undefined}
              onSelectedChange={canApprove ? setSelected : undefined}
            />
          </div>
        </>
      )}

      {tab === 'analisis' && <AnalisisView filter={filter} canExport={canExport} />}

      {tab === 'parameter' && (
        <div className="lyn-card">
          <h3>Parameter yang diisi di Android</h3>
          {jenis.description && <p className="lyn-muted">{jenis.description}</p>}
          <div className="table-container">
            <table className="sns-table">
              <thead>
                <tr>
                  <th>Parameter</th>
                  <th>Tipe</th>
                  <th>Wajib</th>
                  <th>Indikator</th>
                  <th className="num">Ambang sedang</th>
                  <th className="num">Ambang berat</th>
                </tr>
              </thead>
              <tbody>
                {jenis.parameters.length === 0 && (
                  <tr>
                    <td colSpan={6} className="lyn-muted">
                      Belum ada parameter. Atur di Master Jenis &amp; Parameter.
                    </td>
                  </tr>
                )}
                {jenis.parameters.map(p => (
                  <tr key={p.id}>
                    <td>
                      {p.name} {p.satuan && <small className="lyn-muted">({p.satuan})</small>}
                      {p.tipeData === 'OPTION' && p.opsi && <small className="lyn-muted"> · {p.opsi}</small>}
                    </td>
                    <td>{TIPE_LABEL[p.tipeData] ?? p.tipeData}</td>
                    <td>{p.wajib ? 'Ya' : '-'}</td>
                    <td>{p.isIndikator ? <span className="sns-tag">indikator</span> : '-'}</td>
                    <td className="num">{formatAngka(p.ambangSedang)}</td>
                    <td className="num">{formatAngka(p.ambangBerat)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {openedId !== null && <SensusDetailModal sensusId={openedId} onClose={() => setOpenedId(null)} onChanged={() => void load()} />}
    </>
  )
}
