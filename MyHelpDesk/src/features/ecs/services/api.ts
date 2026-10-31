import { http } from '../../../api/http'
import type { CurahHujanItem, TmakItem, KelembapanItem, CurahHujanMonitorItem } from '../types'

// Data sensor ECS dibaca lewat backend kita (api/ecs/...) langsung dari database MyKTTATest.
// HANYA GET. User ECS untuk TMAK diatur di backend (appsettings: Ecs:UserId).

export async function getCurahHujan(date1: string, date2: string): Promise<CurahHujanItem[]> {
  const { data } = await http.get<CurahHujanItem[]>(`/ecs/curah-hujan/${date1}/${date2}`)
  if (!Array.isArray(data)) return []
  return data.filter(
    (r): r is CurahHujanItem => r != null && typeof r === 'object' && 'Tanggal' in r,
  )
}

export async function getTmak(date1: string, date2: string): Promise<TmakItem[]> {
  const { data } = await http.get<TmakItem[]>(`/ecs/tmak/${date1}/${date2}`)
  if (!Array.isArray(data)) return []
  return data.filter((r): r is TmakItem => r != null && typeof r === 'object' && 'Tanggal' in r)
}

export async function getKelembapan(date1: string, date2: string): Promise<KelembapanItem[]> {
  const { data } = await http.get<KelembapanItem[]>(`/ecs/kelembapan/${date1}/${date2}`)
  if (!Array.isArray(data)) return []
  return data.filter((r): r is KelembapanItem => r != null && typeof r === 'object' && 'Tanggal' in r)
}

export async function getMonitorCurahHujan(date1: string, date2: string): Promise<CurahHujanMonitorItem[]> {
  const { data } = await http.get<CurahHujanMonitorItem[]>(`/ecs/curah-hujan/${date1}/${date2}`)
  if (!Array.isArray(data)) return []
  return data.filter(
    (r): r is CurahHujanMonitorItem =>
      r != null &&
      typeof r === 'object' &&
      'LoggerID' in r &&
      'SensorCode' in r &&
      'ProcessedAt' in r &&
      'NilaiHasil' in r,
  )
}
