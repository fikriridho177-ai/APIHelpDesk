import { useEffect, useState } from 'react'

import { getLayananRequests, getLayananSummary } from '../api/layananApi'
import RequestDetailModal from '../components/RequestDetailModal'
import RequestList from '../components/RequestList'
import { DivisionCards } from '../components/ServiceCards'
import SummaryTiles from '../components/SummaryTiles'
import { getCurrentUserId, useDepartmentNames, useLayananServices } from '../hooks/useLayanan'
import type { LynRequestItem, LynSummary } from '../types/layanan'
import '../layanan.css'

// /layanan : beranda layanan -> "Pilih Layanan" pada flowchart.
export default function LayananHomePage() {
  const currentUserId = getCurrentUserId()
  const { divisions, loading } = useLayananServices()
  const departmentNames = useDepartmentNames()

  const [summary, setSummary] = useState<LynSummary | null>(null)
  const [myRequests, setMyRequests] = useState<LynRequestItem[]>([])
  const [requestsLoading, setRequestsLoading] = useState(true)
  const [openedId, setOpenedId] = useState<number | null>(null)

  async function loadRequests() {
    const [summaryData, requests] = await Promise.all([getLayananSummary(true), getLayananRequests({ mine: true })])
    setSummary(summaryData)
    setMyRequests(requests)
  }

  useEffect(() => {
    loadRequests()
      .catch(error => console.error('Gagal mengambil pengajuan:', error))
      .finally(() => setRequestsLoading(false))
  }, [])

  return (
    <section className="lyn-page">
      <header className="lyn-hero">
        <div>
          <span className="lyn-hero-eyebrow">Layanan ASD</span>
          <h1>Pilih Layanan</h1>
          <p>Ajukan kebutuhan ke divisi RNA, GIS, WAD, QC, atau NAT dan pantau statusnya dari pengajuan hingga selesai.</p>
        </div>
        <ol className="lyn-hero-flow" aria-label="Alur layanan">
          {['Isi Formulir', 'Antrian', 'Approve', 'Penjadwalan', 'Proses', 'Selesai'].map((step, index) => (
            <li key={step}>
              <span>{index + 1}</span>
              {step}
            </li>
          ))}
        </ol>
      </header>

      <div className="lyn-section-title">
        <h2>Divisi</h2>
        <span>{divisions.length} divisi tersedia</span>
      </div>

      {loading ? (
        <div className="lyn-empty">Memuat layanan...</div>
      ) : divisions.length === 0 ? (
        <div className="lyn-empty">Belum ada layanan yang dapat Anda akses. Hubungi administrator untuk pengaturan hak akses.</div>
      ) : (
        <DivisionCards divisions={divisions} />
      )}

      <div className="lyn-section-title">
        <h2>Pengajuan Saya</h2>
      </div>

      <SummaryTiles summary={summary} />

      <RequestList
        title="Riwayat Pengajuan Saya"
        requests={myRequests}
        loading={requestsLoading}
        currentUserId={currentUserId}
        showService
        onOpen={request => setOpenedId(request.id)}
      />

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
