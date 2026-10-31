import { useCallback, useEffect, useState } from 'react'

import { bulkApproveSensus, getSensusErrorMessage, getSensusList } from '../api/sensusApi'
import SensusDetailModal from '../components/SensusDetailModal'
import { NoAccess, SensusFilterBar, SensusHero, StatTiles } from '../components/SensusParts'
import SensusTable from '../components/SensusTable'
import { useMenuPermission, useSensusLookups } from '../hooks/useSensus'
import { SNS_MENU, todayIso } from '../sensusConfig'
import type { SnsFilter, SnsListItem } from '../types/sensus'
import '../../layanan/layanan.css'
import '../sensus.css'

// /sensus/approval : sensus yang dikirim Asisten Lapangan dari Android menunggu persetujuan.
export default function SensusApprovalPage() {
  const { permission, loaded } = useMenuPermission(SNS_MENU.approval)
  const { wilayah, jenis } = useSensusLookups()

  const [filter, setFilter] = useState<SnsFilter>({ status: 'SUBMITTED', dateFrom: todayIso(-90) })
  const [rows, setRows] = useState<SnsListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [openedId, setOpenedId] = useState<number | null>(null)
  const [saving, setSaving] = useState(false)

  const canRead = !!permission?.canRead
  const canApprove = !!permission?.canApprove

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

  // Jeda singkat agar mengetik di kolom cari tidak memanggil API setiap huruf.
  useEffect(() => {
    if (!canRead) return
    const timer = setTimeout(() => void load(), 300)
    return () => clearTimeout(timer)
  }, [canRead, load])

  async function approveSelected() {
    if (!confirm(`Setujui ${selected.size} data sensus terpilih?`)) return

    setSaving(true)
    try {
      const result = await bulkApproveSensus(Array.from(selected))
      alert(`${result.approved} sensus disetujui${result.skipped ? `, ${result.skipped} dilewati (status sudah berubah)` : ''}.`)
      await load()
    } catch (error) {
      alert(getSensusErrorMessage(error, 'Gagal menyetujui sensus.'))
    } finally {
      setSaving(false)
    }
  }

  if (!canRead) return <NoAccess loaded={loaded} />

  const count = (status: string) => rows.filter(r => r.status === status).length

  return (
    <section className="lyn-page">
      <SensusHero
        title="Approval Sensus"
        subtitle="Periksa data sensus dari Asisten Lapangan (Android), lalu setujui, minta revisi, atau tolak."
      />

      <StatTiles
        items={[
          { label: 'Data ditampilkan', value: rows.length, tone: 'indigo' },
          { label: 'Menunggu approval', value: count('SUBMITTED'), tone: 'blue' },
          { label: 'Berat (tingkat serangan)', value: rows.filter(r => r.tingkat === 'BERAT').length, tone: 'red' },
          { label: 'Perlu revisi', value: count('REVISI'), tone: 'orange' },
        ]}
      />

      <div className="lyn-card">
        <SensusFilterBar value={filter} onChange={setFilter} wilayah={wilayah} jenis={jenis} showStatus showSearch />

        <div className="sns-toolbar">
          <span className="lyn-muted">{rows.length} data · klik baris untuk melihat detail</span>
          {canApprove && (
            <button
              type="button"
              className="lyn-button success"
              disabled={selected.size === 0 || saving}
              onClick={approveSelected}
            >
              Setujui terpilih ({selected.size})
            </button>
          )}
        </div>

        <SensusTable
          rows={rows}
          loading={loading}
          onOpen={row => setOpenedId(row.id)}
          selected={canApprove ? selected : undefined}
          onSelectedChange={canApprove ? setSelected : undefined}
        />
      </div>

      {openedId !== null && (
        <SensusDetailModal sensusId={openedId} onClose={() => setOpenedId(null)} onChanged={() => void load()} />
      )}
    </section>
  )
}
