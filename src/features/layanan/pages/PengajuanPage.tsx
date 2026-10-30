import { useEffect, useState } from 'react'

import { getLayananRequests, getLayananSummary } from '../api/layananApi'
import RequestDetailModal from '../components/RequestDetailModal'
import RequestList from '../components/RequestList'
import SummaryTiles from '../components/SummaryTiles'
import { getCurrentUserId, useDepartmentNames, useLayananServices } from '../hooks/useLayanan'
import type { LynRequestItem, LynSummary } from '../types/layanan'
import '../layanan.css'

// /pengajuan : semua pengajuan yang boleh dilihat user
// (milik sendiri + pengajuan masuk sesuai Akses Menu & Akses Departemen).
export default function PengajuanPage() {
  const currentUserId = getCurrentUserId()
  const { services } = useLayananServices()
  const departmentNames = useDepartmentNames()

  const [summary, setSummary] = useState<LynSummary | null>(null)
  const [requests, setRequests] = useState<LynRequestItem[]>([])
  const [loading, setLoading] = useState(true)
  const [openedId, setOpenedId] = useState<number | null>(null)

  async function load() {
    const [summaryData, list] = await Promise.all([getLayananSummary(), getLayananRequests()])
    setSummary(summaryData)
    setRequests(list)
  }

  useEffect(() => {
    load()
      .catch(error => console.error('Gagal mengambil pengajuan:', error))
      .finally(() => setLoading(false))
  }, [])

  const canExport = services.some(service => service.permissions.canExport)

  return (
    <section className="lyn-page">
      <header className="lyn-hero compact">
        <div>
          <span className="lyn-hero-eyebrow">Layanan ASD</span>
          <h1>Daftar Pengajuan</h1>
          <p>Pantau semua pengajuan layanan: milik Anda dan pengajuan masuk yang menjadi tanggung jawab Anda.</p>
        </div>
      </header>

      <SummaryTiles summary={summary} />

      <RequestList
        requests={requests}
        loading={loading}
        currentUserId={currentUserId}
        showService
        canExport={canExport}
        onOpen={request => setOpenedId(request.id)}
      />

      {openedId !== null && (
        <RequestDetailModal
          requestId={openedId}
          departmentNames={departmentNames}
          onClose={() => setOpenedId(null)}
          onChanged={() => void load()}
        />
      )}
    </section>
  )
}
