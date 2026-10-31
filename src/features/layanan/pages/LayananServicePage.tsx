import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'

import { DashboardIcon, pickIconName } from '../../dashboard/components/DashboardIcon'
import { getJenisList, getLayananRequests } from '../api/layananApi'
import RequestDetailModal from '../components/RequestDetailModal'
import RequestForm from '../components/RequestForm'
import RequestList from '../components/RequestList'
import { ServiceCards } from '../components/ServiceCards'
import { getCurrentUserId, normalizeRoute, useDepartmentNames, useLayananServices } from '../hooks/useLayanan'
import { getDivisionInfo } from '../layananConfig'
import { getJenisByMenu } from '../../sensus/api/sensusApi'
import SensusJenisView from '../../sensus/components/SensusJenisView'
import type { SnsJenisPage } from '../../sensus/types/sensus'
import type { LynJenis, LynRequestItem, LynServiceNode } from '../types/layanan'
import '../layanan.css'

// /layanan/... : halaman divisi, grup (mis. Sensus HPT), atau sub-layanan.
// Halaman dicocokkan dari route menu, jadi sub-layanan baru di Manajemen Menu langsung bisa dipakai.
export default function LayananServicePage() {
  const location = useLocation()
  const currentUserId = getCurrentUserId()
  const { byRoute, byId, loading } = useLayananServices()
  const departmentNames = useDepartmentNames()

  const node = byRoute.get(normalizeRoute(location.pathname))

  const [requests, setRequests] = useState<LynRequestItem[]>([])
  const [requestsLoading, setRequestsLoading] = useState(true)
  const [openedId, setOpenedId] = useState<number | null>(null)

  // Sub-layanan yang terhubung ke jenis sensus (Master Jenis > Kode menu) tampil sebagai halaman sensus,
  // karena sensus diinput dari Android, bukan lewat form pengajuan.
  const [sensusPage, setSensusPage] = useState<SnsJenisPage | null>(null)
  const [sensusChecked, setSensusChecked] = useState(false)

  // Jenis permintaan aktif divisi ini (Master Jenis Permintaan) -> pilihan di form pengajuan.
  const [jenisList, setJenisList] = useState<LynJenis[]>([])

  useEffect(() => {
    setJenisList([])
    if (!node || node.menuType !== 'GROUP' || node.jenisCount === 0) return
    getJenisList({ divisionCode: node.code })
      .then(data => setJenisList(data.items))
      .catch(error => console.error('Gagal mengambil jenis permintaan:', error))
  }, [node?.code, node?.menuType, node?.jenisCount])

  useEffect(() => {
    setSensusPage(null)
    setSensusChecked(false)
    if (!node || node.menuType !== 'MENU') {
      setSensusChecked(true)
      return
    }
    getJenisByMenu(node.code)
      .then(setSensusPage)
      .catch(() => setSensusPage(null))
      .finally(() => setSensusChecked(true))
  }, [node?.code, node?.menuType])

  // Rantai parent untuk breadcrumb & warna divisi.
  const ancestors = useMemo(() => {
    const chain: LynServiceNode[] = []
    let current = node?.parentId != null ? byId.get(node.parentId) : undefined
    while (current && current.code !== 'LAYANAN') {
      chain.unshift(current)
      current = current.parentId != null ? byId.get(current.parentId) : undefined
    }
    // Menu dipindah ke parent di luar Layanan (Manajemen Menu): divisi diambil dari kode, mis. LYN_QC_KIRIM_NANAS -> LYN_QC.
    if (chain.length === 0 && node && /^LYN_[^_]+_/.test(node.code)) {
      const divisionCode = node.code.split('_').slice(0, 2).join('_')
      const divisionNode = Array.from(byId.values()).find(n => n.code === divisionCode)
      if (divisionNode) chain.push(divisionNode)
    }
    return chain
  }, [node, byId])

  const division = ancestors[0] ?? node
  const divisionIndex = division ? Array.from(byId.values()).filter(n => n.menuType === 'GROUP').indexOf(division) : 0
  const info = getDivisionInfo(division, divisionIndex)

  const loadRequests = useCallback(async () => {
    if (!node) return
    setRequests(await getLayananRequests({ menuCode: node.code }))
  }, [node?.code])

  useEffect(() => {
    if (!node) return
    setRequestsLoading(true)
    loadRequests()
      .catch(error => console.error('Gagal mengambil pengajuan:', error))
      .finally(() => setRequestsLoading(false))
  }, [loadRequests])

  if (loading || (node && !sensusChecked)) return <div className="lyn-empty">Memuat layanan...</div>

  if (!node) {
    return (
      <div className="lyn-empty">
        Layanan tidak ditemukan atau Anda tidak memiliki akses. <Link to="/layanan">Kembali ke daftar layanan</Link>
      </div>
    )
  }

  const isGroup = node.menuType === 'GROUP'
  const canExport = node.permissions.canExport

  return (
    <section className="lyn-page">
      {/* HEADER */}
      <header className={`lyn-service-hero tone-${info.tone}`}>
        <span className="lyn-service-hero-icon">
          <DashboardIcon name={node === division ? info.icon : pickIconName(node)} />
        </span>
        <div>
          <nav className="lyn-crumbs">
            <Link to="/layanan">Layanan</Link>
            {ancestors.map(item => (
              <span key={item.id}>
                <span>/</span>
                <Link to={item.route ?? '/layanan'}>{item.name}</Link>
              </span>
            ))}
          </nav>
          <h1>{node.name}</h1>
          <p>{sensusPage ? `Sensus divisi ${division?.name ?? ''} — data dari aplikasi Android Asisten Lapangan` : node === division ? `${info.fullName ? info.fullName + ' — ' : ''}${info.description}` : `Layanan divisi ${division?.name ?? ''}`}</p>
        </div>
      </header>

      {sensusPage ? (
        <SensusJenisView page={sensusPage} />
      ) : isGroup && node.jenisCount > 0 ? (
        // Divisi dengan Master Jenis Permintaan: form langsung, jenis dipilih di dalam form.
        <>
          {node.children.length > 0 && (
            <>
              <div className="lyn-section-title">
                <h2>Menu Lain</h2>
                <span>{node.children.length} pilihan</span>
              </div>
              <ServiceCards nodes={node.children} tone={info.tone} />
            </>
          )}

          <div className="lyn-service-layout">
            {node.permissions.canCreate ? (
              <RequestForm
                service={node}
                jenisOptions={jenisList}
                onCreated={created => {
                  void loadRequests()
                  setOpenedId(created.summary.id)
                }}
              />
            ) : (
              <div className="lyn-card lyn-info-card">
                <DashboardIcon name="info" />
                <p>Anda dapat melihat layanan ini, tetapi tidak memiliki akses untuk membuat pengajuan.</p>
              </div>
            )}

            <RequestList
              title={`Pengajuan ${node.name}`}
              requests={requests}
              loading={requestsLoading}
              currentUserId={currentUserId}
              showService
              canExport={canExport}
              onOpen={request => setOpenedId(request.id)}
            />
          </div>
        </>
      ) : isGroup ? (
        <>
          <div className="lyn-section-title">
            <h2>Pilih Layanan</h2>
            <span>{node.children.length} pilihan</span>
          </div>
          {node.children.length === 0 ? (
            <div className="lyn-empty">Belum ada sub-layanan yang dapat Anda akses.</div>
          ) : (
            <ServiceCards nodes={node.children} tone={info.tone} />
          )}

          <RequestList
            title={`Pengajuan ${node.name}`}
            requests={requests}
            loading={requestsLoading}
            currentUserId={currentUserId}
            showService
            onOpen={request => setOpenedId(request.id)}
          />
        </>
      ) : (
        <div className="lyn-service-layout">
          {node.permissions.canCreate ? (
            <RequestForm
              service={node}
              onCreated={created => {
                void loadRequests()
                setOpenedId(created.summary.id)
              }}
            />
          ) : (
            <div className="lyn-card lyn-info-card">
              <DashboardIcon name="info" />
              <p>Anda dapat melihat layanan ini, tetapi tidak memiliki akses untuk membuat pengajuan.</p>
            </div>
          )}

          <RequestList
            requests={requests}
            loading={requestsLoading}
            currentUserId={currentUserId}
            canExport={canExport}
            onOpen={request => setOpenedId(request.id)}
          />
        </div>
      )}

      {openedId !== null && (
        <RequestDetailModal
          requestId={openedId}
          departmentNames={departmentNames}
          onClose={() => setOpenedId(null)}
          onChanged={() => void loadRequests()}
        />
      )}
    </section>
  )
}
