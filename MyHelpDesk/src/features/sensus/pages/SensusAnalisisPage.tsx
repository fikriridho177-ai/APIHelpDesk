import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'

import AnalisisView from '../components/AnalisisView'
import { NoAccess, SensusFilterBar, SensusHero } from '../components/SensusParts'
import { useMenuPermission, useSensusLookups } from '../hooks/useSensus'
import { SNS_MENU, todayIso } from '../sensusConfig'
import type { SnsFilter } from '../types/sensus'
import '../../layanan/layanan.css'
import '../sensus.css'

// /sensus/analisis : analisis tingkat serangan & tren per jenis sensus (hanya data yang sudah disetujui).
export default function SensusAnalisisPage() {
  const { permission, loaded } = useMenuPermission(SNS_MENU.analisis)
  const { wilayah, jenis } = useSensusLookups()

  // ?jenisId= dari kartu masalah di halaman divisi RNA.
  const [params] = useSearchParams()
  const [filter, setFilter] = useState<SnsFilter>({
    dateFrom: todayIso(-180),
    dateTo: todayIso(),
    jenisId: Number(params.get('jenisId')) || undefined,
  })

  // Pilih jenis sensus pertama secara otomatis.
  useEffect(() => {
    if (!filter.jenisId && jenis.length > 0) setFilter(f => ({ ...f, jenisId: jenis[0].id }))
  }, [jenis, filter.jenisId])

  if (!permission?.canRead) return <NoAccess loaded={loaded} />

  return (
    <section className="lyn-page">
      <SensusHero
        title="Analisis Sensus"
        subtitle="Tingkat serangan hama & penyakit per persil, sebaran tingkat, dan tren bulanan dari data sensus yang sudah disetujui."
      />

      <div className="lyn-card">
        <SensusFilterBar value={filter} onChange={setFilter} wilayah={wilayah} jenis={jenis} requireJenis />
      </div>

      {jenis.length === 0 ? (
        <div className="lyn-empty">Belum ada jenis sensus aktif.</div>
      ) : (
        <AnalisisView filter={filter} canExport={!!permission.canExport} />
      )}
    </section>
  )
}
