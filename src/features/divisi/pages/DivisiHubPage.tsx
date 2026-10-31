import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'

import { DashboardIcon, pickIconName } from '../../dashboard/components/DashboardIcon'
import { getJenisList, getLayananReport } from '../../layanan/api/layananApi'
import { useLayananServices } from '../../layanan/hooks/useLayanan'
import { formatDate } from '../../layanan/layananConfig'
import type { LynJenis, LynReport } from '../../layanan/types/layanan'
import { getNavigationMenus } from '../../menus/api/menuApi'
import type { NavigationMenuItem } from '../../menus/types/menu'
import { getRnaRingkasan, type RnaJenisRingkasan, type RnaRingkasan } from '../divisiApi'
import { DIVISI_CONFIG, type DivisiConfig } from '../divisiConfig'
import '../../layanan/layanan.css'
import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import MapWorkspace from '../../webgis/components/MapWorkspace'
import '../divisi.css'

const PERIODE_HARI = 90

type MenuLookup = Map<string, NavigationMenuItem>
type DivisiTab = 'ringkasan' | 'map'

// /divisi/:slug : halaman divisi (dibuka dari kartu Katalog di dashboard).
// Isi: masalah / pekerjaan yang ditangani divisi + menu Master, Transaksi, Monitoring sesuai hak akses.
export default function DivisiHubPage() {
  const { slug } = useParams()
  const config = DIVISI_CONFIG.find(c => c.slug === slug)
  const { divisions, loading } = useLayananServices()
  const [menus, setMenus] = useState<MenuLookup | null>(null)

  useEffect(() => {
    getNavigationMenus()
      .then(list => setMenus(new Map(list.map(m => [m.code, m]))))
      .catch(() => setMenus(new Map()))
  }, [])

  const division = divisions.find(d => d.code === config?.divisionCode)

  // Tab aktif disimpan di URL (?tab=map) supaya bisa di-refresh / dibagikan.
  const [searchParams, setSearchParams] = useSearchParams()
  const tab: DivisiTab = searchParams.get('tab') === 'map' ? 'map' : 'ringkasan'
  const setTab = (next: DivisiTab) =>
    setSearchParams(prev => {
      const params = new URLSearchParams(prev)
      if (next === 'map') params.set('tab', 'map')
      else params.delete('tab')
      return params
    })
  // Tab Map Workspace hanya muncul bila menu peta ada di navigasi user (sudah difilter hak akses).
  const showMap = !!config?.mapWorkspaceMenu && !!menus?.has(config.mapWorkspaceMenu)

  if (!config) return <div className="lyn-empty">Halaman divisi tidak ditemukan.</div>
  if (loading || menus === null) return <div className="lyn-empty">Memuat halaman divisi...</div>
  if (!division) {
    return <div className="lyn-empty">Anda tidak memiliki akses ke divisi ini. Hubungi administrator untuk pengaturan Hak Akses.</div>
  }

  return (
    <section className="lyn-page dvs-page">
      <header className="lyn-hero compact">
        <div>
          <span className="lyn-hero-eyebrow">Divisi</span>
          <h1>
            {division.name}
            {division.fullName && <small className="dvs-fullname"> · {division.fullName}</small>}
          </h1>
          {division.description && <p>{division.description}</p>}
        </div>
      </header>

      {showMap && (
        <div className="dvs-tabs" role="tablist">
          <button type="button" role="tab" aria-selected={tab === 'ringkasan'} className={tab === 'ringkasan' ? 'active' : ''} onClick={() => setTab('ringkasan')}>
            <DashboardIcon name="grid" /> Ringkasan
          </button>
          <button type="button" role="tab" aria-selected={tab === 'map'} className={tab === 'map' ? 'active' : ''} onClick={() => setTab('map')}>
            <DashboardIcon name="map" /> Map Workspace
          </button>
        </div>
      )}

      {showMap && tab === 'map' ? (
        <MapWorkspaceTab menuCode={config.mapWorkspaceMenu!} />
      ) : (
        <>
          <div className="lyn-card">
            <div className="dvs-card-head">
              <h2>{config.problemTitle}</h2>
              <span className="lyn-muted">{PERIODE_HARI} hari terakhir</span>
            </div>
            {config.slug === 'rna' ? <RnaProblems menus={menus} /> : <GisProblems config={config} menus={menus} />}
          </div>

          <Sections config={config} menus={menus} />
        </>
      )}
    </section>
  )
}

/* ---------- Map Workspace (viewer WebGIS) ---------- */

function MapWorkspaceTab({ menuCode }: { menuCode: string }) {
  const { permission, loaded } = useMenuPermission(menuCode)
  if (!permission?.canRead) return <NoAccess loaded={loaded} />
  return <MapWorkspace />
}

/* ---------- RNA: sensus per jenis (HPT & tanaman) ---------- */

function RnaProblems({ menus }: { menus: MenuLookup }) {
  const navigate = useNavigate()
  const [data, setData] = useState<RnaRingkasan | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    getRnaRingkasan(PERIODE_HARI)
      .then(setData)
      .catch(() => setError('Ringkasan sensus tidak dapat dimuat.'))
  }, [])

  if (error) return <p className="error">{error}</p>
  if (!data) return <p className="lyn-muted">Memuat ringkasan sensus...</p>
  if (data.jenis.length === 0) return <p className="lyn-muted">Belum ada jenis sensus aktif di Master Jenis & Parameter.</p>

  // Kartu dibuka ke Analisis Sensus (jenis terpilih); bila tidak ada hak, ke Monitoring Sensus.
  const target = menus.get('SNS_ANALISIS')?.route ?? menus.get('SNS_MONITORING')?.route
  const open = (j: RnaJenisRingkasan) => target && navigate(`${target}?jenisId=${j.id}`)

  const groups = [
    { title: 'Sensus HPT', hint: 'Hama & penyakit tanaman', items: data.jenis.filter(j => j.kategori === 'HPT') },
    { title: 'Sensus Tanaman', hint: 'Populasi & kondisi tanaman', items: data.jenis.filter(j => j.kategori !== 'HPT') },
  ].filter(g => g.items.length > 0)

  return (
    <div className="dvs-groups">
      {groups.map(group => (
        <div key={group.title} className="dvs-group">
          <div className="dvs-group-title">
            <strong>{group.title}</strong>
            <span>{group.hint}</span>
          </div>
          <div className="dvs-problem-grid">
            {group.items.map(j => (
              <button key={j.id} type="button" className="dvs-problem" onClick={() => open(j)} disabled={!target}>
                <span className="dvs-problem-name">{j.name}</span>
                {j.description && <small className="dvs-problem-desc">{j.description}</small>}
                <span className="dvs-chips">
                  <Chip tone="red" label="Berat" value={j.berat} />
                  <Chip tone="amber" label="Sedang" value={j.sedang} />
                  <Chip tone="blue" label="Menunggu approval" value={j.menungguApproval} />
                </span>
                <span className="dvs-problem-foot">
                  {j.sensusPeriode} sensus · {j.persilDinilai} persil dinilai
                  {j.sensusTerakhir ? ` · terakhir ${formatDate(j.sensusTerakhir)}` : ''}
                </span>
              </button>
            ))}
          </div>
        </div>
      ))}
      <p className="lyn-muted dvs-note">
        Berat / sedang dihitung dari sensus terakhir yang sudah disetujui per persil, memakai ambang indikator di Master Jenis &
        Parameter.
      </p>
    </div>
  )
}

/* ---------- GIS: tiket per jenis pekerjaan ---------- */

function GisProblems({ config, menus }: { config: DivisiConfig; menus: MenuLookup }) {
  const navigate = useNavigate()
  const [jenis, setJenis] = useState<LynJenis[] | null>(null)
  const [report, setReport] = useState<LynReport | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    Promise.all([
      getJenisList({ divisionCode: config.divisionCode }),
      getLayananReport({ divisionCode: config.divisionCode }),
    ])
      .then(([list, rep]) => {
        setJenis(list.items.filter(j => j.active))
        setReport(rep)
      })
      .catch(() => setError('Ringkasan pekerjaan tidak dapat dimuat.'))
  }, [config.divisionCode])

  const stats = useMemo(() => new Map((report?.perJenis ?? []).map(g => [g.label, g])), [report])

  if (error) return <p className="error">{error}</p>
  if (!jenis) return <p className="lyn-muted">Memuat ringkasan pekerjaan...</p>
  if (jenis.length === 0) return <p className="lyn-muted">Belum ada jenis pekerjaan aktif di Master Jenis Permintaan.</p>

  // Kartu dibuka ke Monitoring Permintaan (divisi & jenis terpilih); bila tidak ada hak, ke form divisi.
  const monitoring = menus.get('PMT_MONITORING')?.route
  const fallback = menus.get(config.divisionCode)?.route
  const open = (j: LynJenis) =>
    monitoring
      ? navigate(`${monitoring}?division=${config.divisionCode}&jenisId=${j.id}`)
      : fallback && navigate(fallback)

  return (
    <div className="dvs-groups">
      <div className="dvs-problem-grid">
        {jenis.map(j => {
          const s = stats.get(j.name)
          return (
            <button key={j.id} type="button" className="dvs-problem" onClick={() => open(j)} disabled={!monitoring && !fallback}>
              <span className="dvs-problem-name">{j.name}</span>
              {j.description && <small className="dvs-problem-desc">{j.description}</small>}
              <span className="dvs-chips">
                <Chip tone="blue" label="Menunggu approval" value={s?.menungguApproval ?? 0} />
                <Chip tone="amber" label="Dalam proses" value={(s?.disetujui ?? 0) + (s?.dalamProses ?? 0)} />
                <Chip tone="red" label="Terlambat" value={s?.terlambat ?? 0} />
              </span>
              <span className="dvs-problem-foot">
                {s?.total ?? 0} tiket · {s?.selesai ?? 0} selesai
              </span>
            </button>
          )
        })}
      </div>
      <p className="lyn-muted dvs-note">Jumlah tiket mengikuti tiket yang boleh Anda lihat (Hak Akses & Akses Departemen).</p>
    </div>
  )
}

function Chip({ tone, label, value }: { tone: string; label: string; value: number }) {
  return (
    <span className={`dvs-chip tone-${value > 0 ? tone : 'gray'}`}>
      <b>{value}</b> {label}
    </span>
  )
}

/* ---------- Master / Transaksi / Monitoring ---------- */

function Sections({ config, menus }: { config: DivisiConfig; menus: MenuLookup }) {
  const navigate = useNavigate()

  const sections = config.sections
    .map(section => ({
      ...section,
      items: section.menuCodes
        .map(code => menus.get(code))
        .filter((m): m is NavigationMenuItem => !!m?.route),
    }))
    .filter(section => section.items.length > 0)

  if (sections.length === 0) return null

  return (
    <div className="dvs-sections">
      {sections.map(section => (
        <div key={section.key} className={`lyn-card dvs-section dvs-${section.key}`}>
          <div>
            <h2>{section.title}</h2>
            <span className="lyn-muted">{section.hint}</span>
          </div>
          <div className="dvs-links">
            {section.items.map(menu => (
              <button key={menu.code} type="button" className="dvs-link" onClick={() => navigate(menu.route!)}>
                <span className="dvs-link-icon">
                  <DashboardIcon name={pickIconName(menu)} />
                </span>
                <span className="dvs-link-text">
                  <strong>{section.labels?.[menu.code] ?? menu.name}</strong>
                  {menu.description && <small>{menu.description}</small>}
                </span>
                <DashboardIcon name="arrow" />
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}
