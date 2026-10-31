import { useEffect, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { getSensusList, getSensusMonitoring } from '../api/sensusApi'
import SensusDetailModal from '../components/SensusDetailModal'
import { BarList, NoAccess, SensusFilterBar, SensusHero, StatTiles } from '../components/SensusParts'
import SensusTable from '../components/SensusTable'
import { useMenuPermission, useSensusLookups } from '../hooks/useSensus'
import { PERSIL_SUMBER, SNS_MENU, SNS_STATUS, downloadCsv, formatTanggal, persen, todayIso } from '../sensusConfig'
import type { PersilSumber, SnsCount, SnsFilter, SnsListItem, SnsMonitoring } from '../types/sensus'
import '../../layanan/layanan.css'
import '../sensus.css'

type Tab = 'ringkasan' | 'terlambat' | 'data'

// /sensus/monitoring : progres sensus per jenis / petugas / wilayah, persil yang terlambat disensus, dan daftar data.
export default function SensusMonitoringPage() {
  const { permission, loaded } = useMenuPermission(SNS_MENU.monitoring)
  const { wilayah, jenis } = useSensusLookups()

  const [filter, setFilter] = useState<SnsFilter>({ dateFrom: todayIso(-29), dateTo: todayIso() })
  const [hariTerlambat, setHariTerlambat] = useState(30)
  // Sumber persil (MyPAD / RSUPPKB) untuk menghitung persil yang belum disensus.
  const [sumber, setSumber] = useState<PersilSumber>('KLP')
  const [data, setData] = useState<SnsMonitoring | null>(null)
  const [rows, setRows] = useState<SnsListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [tab, setTab] = useState<Tab>('ringkasan')
  const [openedId, setOpenedId] = useState<number | null>(null)

  const canRead = !!permission?.canRead
  const canExport = !!permission?.canExport

  useEffect(() => {
    if (!canRead) return
    setLoading(true)

    const timer = setTimeout(() => {
      Promise.all([getSensusMonitoring({ ...filter, hariTerlambat, sumber }), getSensusList(filter)])
        .then(([monitoring, list]) => {
          setData(monitoring)
          setRows(list)
        })
        .catch(error => console.error('Gagal mengambil monitoring sensus:', error))
        .finally(() => setLoading(false))
    }, 300)

    return () => clearTimeout(timer)
  }, [canRead, filter, hariTerlambat, sumber])

  if (!canRead) return <NoAccess loaded={loaded} />

  function exportData() {
    if (tab === 'terlambat' && data) {
      downloadCsv(
        'sensus-persil-terlambat',
        ['Sumber', 'Wilayah', 'Afdeling', 'Kode Persil', 'Persil ID', 'Jenis Sensus', 'Terakhir Sensus', 'Hari Sejak'],
        data.persilTerlambat.map(b => [PERSIL_SUMBER[b.persilSumber]?.label ?? b.persilSumber, b.kodeWilayah ?? '', b.kodeAfdeling ?? '', b.persilKode, b.persilId, b.jenisName, b.terakhirSensus ?? 'Belum pernah', b.hariSejak ?? '']),
      )
      return
    }

    downloadCsv(
      'sensus-monitoring',
      ['No Sensus', 'Tanggal', 'Jenis', 'Sumber Persil', 'Wilayah', 'Afdeling', 'Kode Persil', 'Persil ID', 'Petugas', 'Sampel', 'Terserang', 'Tingkat', 'Status'],
      rows.map(r => [
        r.sensusNo, r.tanggalSensus, r.jenisName, PERSIL_SUMBER[r.persilSumber]?.label ?? r.persilSumber, r.kodeWilayah ?? '', r.kodeAfdeling ?? '', r.persilKode, r.persilId, r.petugasName,
        r.jumlahPokokSampel, r.jumlahPokokTerserang, r.tingkat, SNS_STATUS[r.status]?.label ?? r.status,
      ]),
    )
  }

  return (
    <section className="lyn-page">
      <SensusHero
        title="Monitoring Sensus"
        subtitle="Pantau progres sensus lapangan: jumlah data masuk, status approval, kinerja petugas, dan persil yang belum disensus."
      />

      <div className="lyn-card">
        <SensusFilterBar value={filter} onChange={setFilter} wilayah={wilayah} jenis={jenis}>
          <label className="lyn-field">
            <span>Sumber persil (terlambat)</span>
            <select value={sumber} onChange={e => setSumber(e.target.value as PersilSumber)}>
              {(Object.keys(PERSIL_SUMBER) as PersilSumber[]).map(k => (
                <option key={k} value={k}>
                  {PERSIL_SUMBER[k].label}
                </option>
              ))}
            </select>
          </label>
          <label className="lyn-field">
            <span>Batas terlambat (hari)</span>
            <input
              type="number"
              min={1}
              value={hariTerlambat}
              onChange={e => setHariTerlambat(Math.max(1, Number(e.target.value) || 1))}
            />
          </label>
        </SensusFilterBar>
      </div>

      <StatTiles
        items={[
          { label: 'Total sensus', value: data?.total ?? '-', tone: 'indigo' },
          { label: 'Menunggu approval', value: data?.submitted ?? '-', tone: 'blue' },
          { label: 'Disetujui', value: data?.approved ?? '-', tone: 'green' },
          { label: 'Revisi / ditolak', value: data ? data.revisi + data.rejected : '-', tone: 'orange' },
          { label: `Persil > ${hariTerlambat} hari belum disensus`, value: data ? (data.persilTerlambat.length >= 300 ? '300+' : data.persilTerlambat.length) : '-', tone: 'red' },
        ]}
      />

      <div className="sns-tabs">
        {(
          [
            ['ringkasan', 'Ringkasan'],
            ['terlambat', 'Persil Terlambat'],
            ['data', 'Data Sensus'],
          ] as [Tab, string][]
        ).map(([key, label]) => (
          <button key={key} type="button" className={tab === key ? 'lyn-chip active' : 'lyn-chip'} onClick={() => setTab(key)}>
            {label}
          </button>
        ))}

        {canExport && tab !== 'ringkasan' && (
          <button type="button" className="lyn-button ghost sns-tabs-action" onClick={exportData}>
            <DashboardIcon name="download" /> Export
          </button>
        )}
      </div>

      {loading || !data ? (
        <div className="lyn-empty">Memuat monitoring...</div>
      ) : tab === 'ringkasan' ? (
        <>
          <div className="lyn-card">
            <h3>Sensus masuk per hari</h3>
            <BarList rows={data.perHari.map(x => ({ label: x.label, value: x.total, tone: 'blue' }))} />
          </div>

          <div className="sns-grid-2">
            <CountCard title="Per jenis sensus" rows={data.perJenis} />
            <CountCard title="Per wilayah" rows={data.perWilayah} />
          </div>

          <CountCard title="Per petugas (Asisten Lapangan)" rows={data.perPetugas} />
        </>
      ) : tab === 'terlambat' ? (
        <div className="lyn-card">
          <h3>
            {PERSIL_SUMBER[data.sumberTerlambat]?.label} belum disensus lebih dari {hariTerlambat} hari
          </h3>
          <p className="lyn-muted">
            Daftar persil aktif dari database {PERSIL_SUMBER[data.sumberTerlambat]?.database}, dibandingkan dengan sensus terakhir
            berstatus Menunggu Approval / Disetujui, per jenis sensus sesuai komoditas. Maksimal 300 baris, yang belum pernah
            disensus ditampilkan lebih dulu.
          </p>
          {data.persilTerlambatError ? (
            <div className="lyn-empty">{data.persilTerlambatError}</div>
          ) : data.persilTerlambat.length === 0 ? (
            <div className="lyn-empty">Semua persil sudah disensus tepat waktu.</div>
          ) : (
            <div className="table-container">
              <table className="sns-table">
                <thead>
                  <tr>
                    <th>Wilayah</th>
                    <th>Afdeling</th>
                    <th>Kode persil</th>
                    <th>Persil ID</th>
                    <th>Jenis sensus</th>
                    <th>Terakhir sensus</th>
                    <th className="num">Hari sejak</th>
                  </tr>
                </thead>
                <tbody>
                  {data.persilTerlambat.map(b => (
                    <tr key={`${b.persilId}-${b.jenisId}`}>
                      <td>{b.kodeWilayah || '-'}</td>
                      <td>{b.kodeAfdeling || '-'}</td>
                      <td>
                        <b>{b.persilKode || '-'}</b>
                      </td>
                      <td>{b.persilId}</td>
                      <td>{b.jenisName}</td>
                      <td>{b.terakhirSensus ? formatTanggal(b.terakhirSensus) : <span className="lyn-badge tone-red">Belum pernah</span>}</td>
                      <td className="num">{b.hariSejak ?? '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ) : (
        <div className="lyn-card">
          <SensusTable rows={rows} onOpen={row => setOpenedId(row.id)} />
        </div>
      )}

      {openedId !== null && <SensusDetailModal sensusId={openedId} onClose={() => setOpenedId(null)} />}
    </section>
  )
}

function CountCard({ title, rows }: { title: string; rows: SnsCount[] }) {
  return (
    <div className="lyn-card">
      <h3>{title}</h3>
      {rows.length === 0 ? (
        <p className="lyn-muted">Belum ada data.</p>
      ) : (
        <div className="table-container">
          <table className="sns-table compact">
            <thead>
              <tr>
                <th>Nama</th>
                <th className="num">Total</th>
                <th className="num">Menunggu</th>
                <th className="num">Disetujui</th>
                <th className="num">Revisi</th>
                <th className="num">Ditolak</th>
                <th className="num">% Disetujui</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(r => (
                <tr key={r.key}>
                  <td>{r.label}</td>
                  <td className="num">
                    <b>{r.total}</b>
                  </td>
                  <td className="num">{r.submitted}</td>
                  <td className="num">{r.approved}</td>
                  <td className="num">{r.revisi}</td>
                  <td className="num">{r.rejected}</td>
                  <td className="num">{persen(r.approved, r.total)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
