import { useEffect, useMemo, useState } from 'react'

import { DashboardIcon, type IconName } from '../../dashboard/components/DashboardIcon'
import { getPersil, getPersilWilayah, getSensusErrorMessage } from '../api/sensusApi'
import { NoAccess, SensusHero } from '../components/SensusParts'
import { useMenuPermission } from '../hooks/useSensus'
import { SNS_MENU } from '../sensusConfig'
import type { Persil, PersilResult, PersilSumber } from '../types/sensus'
import '../../layanan/layanan.css'
import '../sensus.css'

/* =========================================================
   SUMBER DATA (hanya dibaca dari MyPAD & RSUPPKB)
========================================================= */

type SourceInfo = { key: PersilSumber; title: string; database: string; icon: IconName; tone: string }

const SOURCES: SourceInfo[] = [
  { key: 'KLP', title: 'Persil Kelapa', database: 'MyPAD', icon: 'leaf', tone: 'green' },
  { key: 'TNM_NNS', title: 'Tanaman Nanas', database: 'MyPAD', icon: 'grid', tone: 'orange' },
  { key: 'LHN_NNS', title: 'Lahan Nanas', database: 'MyPAD', icon: 'map', tone: 'purple' },
  { key: 'PNN', title: 'Persil Panen', database: 'RSUPPKB', icon: 'building', tone: 'teal' },
]

const PAGE_SIZE = 50

const hasCoordinate = (p: Persil) => p.latitude != null && p.longitude != null

const mapsUrl = (p: Persil) => `https://www.google.com/maps?q=${p.latitude},${p.longitude}`

const formatCoord = (value?: number | null) => (value == null ? '-' : value.toFixed(6))

// /sensus/master/lokasi : Master Kebun & Blok — tampilan data persil (tanpa tambah / ubah / hapus).
export default function MasterLokasiPage() {
  const { permission, loaded } = useMenuPermission(SNS_MENU.masterLokasi)
  const canRead = !!permission?.canRead

  const [sumber, setSumber] = useState<PersilSumber>('KLP')
  const [wilayahList, setWilayahList] = useState<string[]>([])
  const [wilayah, setWilayah] = useState('')
  const [search, setSearch] = useState('')
  const [result, setResult] = useState<PersilResult | null>(null)
  const [totals, setTotals] = useState<Partial<Record<PersilSumber, number>>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)

  const source = SOURCES.find(s => s.key === sumber)!

  // Ganti sumber: muat daftar wilayah & reset filter.
  useEffect(() => {
    if (!canRead) return
    setWilayah('')
    setSearch('')
    setWilayahList([])
    getPersilWilayah(sumber).then(setWilayahList).catch(() => setWilayahList([]))
  }, [canRead, sumber])

  // Muat data persil (jeda singkat saat mengetik pencarian).
  useEffect(() => {
    if (!canRead) return
    setLoading(true)
    setError('')

    const timer = setTimeout(() => {
      getPersil({ sumber, wilayah: wilayah || undefined, search: search.trim() || undefined })
        .then(data => {
          setResult(data)
          setPage(1)
          // Jumlah di kartu sumber = total tanpa filter.
          if (!wilayah && !search.trim()) setTotals(t => ({ ...t, [sumber]: data.total }))
        })
        .catch(err => {
          setResult(null)
          setError(getSensusErrorMessage(err, 'Gagal memuat data persil.'))
        })
        .finally(() => setLoading(false))
    }, 350)

    return () => clearTimeout(timer)
  }, [canRead, sumber, wilayah, search])

  const rows = result?.rows ?? []

  const withCoord = useMemo(() => rows.filter(hasCoordinate).length, [rows])

  // Kembali ke atas tabel setiap ganti halaman.
  useEffect(() => {
    document.querySelector('.persil-table-wrap')?.scrollTo({ top: 0 })
  }, [page])

  if (!canRead) return <NoAccess loaded={loaded} />

  const pageCount = Math.max(1, Math.ceil(rows.length / PAGE_SIZE))
  const pageRows = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  return (
    <section className="lyn-page">
      <SensusHero title="Master Persil & Sub Persil" subtitle="Data persil kelapa & nanas. Halaman ini hanya untuk melihat data." />

      {/* PILIH SUMBER */}
      <div className="persil-sources">
        {SOURCES.map(s => (
          <button
            key={s.key}
            type="button"
            className={`persil-source tone-${s.tone}${s.key === sumber ? ' active' : ''}`}
            onClick={() => setSumber(s.key)}
            aria-pressed={s.key === sumber}
          >
            <span className="persil-source-icon">
              <DashboardIcon name={s.icon} />
            </span>
            <span className="persil-source-text">
              <strong>{s.title}</strong>
              <span className="persil-db">{s.database}</span>
            </span>
            <b className="persil-source-total">
              {totals[s.key] != null ? totals[s.key]!.toLocaleString('id-ID') : '—'}
            </b>
          </button>
        ))}
      </div>

      {/* FILTER */}
      <div className="lyn-card persil-filter">
        <label className="lyn-field">
          <span>Wilayah</span>
          <select value={wilayah} onChange={e => setWilayah(e.target.value)}>
            <option value="">Semua wilayah</option>
            {wilayahList.map(w => (
              <option key={w} value={w}>
                {w}
              </option>
            ))}
          </select>
        </label>
        <label className="lyn-field persil-search">
          <span>Cari {source.key === 'KLP' ? 'kode sub persil' : 'kode'} / Persil ID</span>
          <span className="lyn-search">
            <DashboardIcon name="search" />
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Ketik untuk mencari..." />
          </span>
        </label>
        {(wilayah || search) && (
          <button type="button" className="lyn-button ghost" onClick={() => { setWilayah(''); setSearch('') }}>
            Reset filter
          </button>
        )}
      </div>

      {error ? (
        <div className="lyn-empty">
          <DashboardIcon name="info" />
          <p>{error}</p>
        </div>
      ) : (
        <div className="lyn-card">
          <div className="sns-toolbar">
            <div>
              <h3>Daftar persil</h3>
              <p className="lyn-muted">
                {source.title} 
                {!loading && rows.length > 0 && (
                  <>
                    {' · '}
                    {withCoord.toLocaleString('id-ID')} berkoordinat
                    {result?.terpotong ? ' · dibatasi 20.000 baris' : ''}
                  </>
                )}
              </p>
            </div>
            {!loading && rows.length > 0 && (
              <span className="lyn-muted">
                Menampilkan {((page - 1) * PAGE_SIZE + 1).toLocaleString('id-ID')}–
                {Math.min(page * PAGE_SIZE, rows.length).toLocaleString('id-ID')} dari {rows.length.toLocaleString('id-ID')}
              </span>
            )}
          </div>

          {loading ? (
            <div className="lyn-empty">Memuat data persil...</div>
          ) : rows.length === 0 ? (
            <div className="lyn-empty">
              <DashboardIcon name="inbox" />
              <p>Data tidak ditemukan.</p>
            </div>
          ) : (
            <>
              <div className="table-container persil-table-wrap">
                <table className="sns-table persil-table">
                  <thead>
                    <tr>
                      <th className="num">No</th>
                      <th>{source.key === 'KLP' ? 'Kode Sub Persil' : 'Kode'}</th>
                      {source.key === 'KLP' && <th>Afdeling</th>}
                      <th>Wilayah</th>
                      <th>Persil</th>
                      <th>Persil ID</th>
                      <th className="num">Latitude</th>
                      <th className="num">Longitude</th>
                      <th>Lokasi</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pageRows.map((p, i) => (
                      <tr key={`${p.kode}-${p.persilId}-${i}`}>
                        <td className="num lyn-muted">{(page - 1) * PAGE_SIZE + i + 1}</td>
                        <td className="sns-no">{p.kode || '-'}</td>
                        {source.key === 'KLP' && <td>{p.afdeling || '-'}</td>}
                        <td>{p.wilayah ? <span className="persil-chip">{p.wilayah}</span> : '-'}</td>
                        <td>{p.persil || '-'}</td>
                        <td>{p.persilId || '-'}</td>
                        <td className="num">{formatCoord(p.latitude)}</td>
                        <td className="num">{formatCoord(p.longitude)}</td>
                        <td>
                          {hasCoordinate(p) ? (
                            <a className="persil-map-link" href={mapsUrl(p)} target="_blank" rel="noreferrer">
                              <DashboardIcon name="pin" /> Peta
                            </a>
                          ) : (
                            <span className="lyn-badge tone-gray">Tanpa koordinat</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {pageCount > 1 && (
                <div className="persil-pager">
                  <button type="button" className="lyn-button ghost" disabled={page === 1} onClick={() => setPage(1)}>
                    «
                  </button>
                  <button type="button" className="lyn-button ghost" disabled={page === 1} onClick={() => setPage(p => p - 1)}>
                    ‹ Sebelumnya
                  </button>
                  <span>
                    Halaman <b>{page}</b> dari {pageCount.toLocaleString('id-ID')}
                  </span>
                  <button type="button" className="lyn-button ghost" disabled={page === pageCount} onClick={() => setPage(p => p + 1)}>
                    Berikutnya ›
                  </button>
                  <button type="button" className="lyn-button ghost" disabled={page === pageCount} onClick={() => setPage(pageCount)}>
                    »
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </section>
  )
}
