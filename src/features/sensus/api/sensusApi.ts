import { http } from '../../../api/http'

import type {
  SnsAction,
  SnsAnalisis,
  SnsDetail,
  SnsFilter,
  SnsJenis,
  SnsJenisPage,
  SnsListItem,
  SnsMonitoring,
  SnsParameter,
  PersilResult,
  PersilSumber,
} from '../types/sensus'

// Buang filter kosong agar tidak terkirim sebagai "?jenisId=".
const clean = (params: object) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''))

/* ---------- Sensus ---------- */

export async function getSensusList(filter: SnsFilter = {}): Promise<SnsListItem[]> {
  return (await http.get<SnsListItem[]>('/sensus', { params: clean(filter) })).data
}

export async function getSensusDetail(id: number): Promise<SnsDetail> {
  return (await http.get<SnsDetail>(`/sensus/${id}`)).data
}

export async function runSensusAction(id: number, action: SnsAction, note?: string): Promise<SnsDetail> {
  return (await http.post<SnsDetail>(`/sensus/${id}/actions`, { action, note })).data
}

export async function bulkApproveSensus(ids: number[], note?: string): Promise<{ approved: number; skipped: number }> {
  return (await http.post('/sensus/bulk-approve', { ids, note })).data
}

export async function getSensusMonitoring(filter: SnsFilter & { hariTerlambat?: number; sumber?: PersilSumber }): Promise<SnsMonitoring> {
  return (await http.get<SnsMonitoring>('/sensus/monitoring', { params: clean(filter) })).data
}

export async function getSensusAnalisis(filter: SnsFilter & { jenisId: number }): Promise<SnsAnalisis> {
  return (await http.get<SnsAnalisis>('/sensus/analisis', { params: clean(filter) })).data
}

/* ---------- Master ---------- */

const master = '/sensus/master'

// Wilayah yang ada di data sensus (untuk filter).
export const getSensusWilayah = async () => (await http.get<string[]>('/sensus/wilayah')).data
export const getJenis = async () => (await http.get<SnsJenis[]>(`${master}/jenis`)).data

export type MasterKind = 'jenis'

// Simpan master: id kosong = tambah, ada id = ubah.
export async function saveMaster(kind: MasterKind, id: number | null, body: object) {
  return id ? http.put(`${master}/${kind}/${id}`, body) : http.post(`${master}/${kind}`, body)
}

export const deactivateMaster = (kind: MasterKind, id: number) => http.delete(`${master}/${kind}/${id}`)

export async function saveParameter(jenisId: number, id: number | null, body: Omit<SnsParameter, 'id' | 'jenisId'>) {
  return id ? http.put(`${master}/parameter/${id}`, body) : http.post(`${master}/jenis/${jenisId}/parameter`, body)
}

export const deleteParameter = (id: number) => http.delete(`${master}/parameter/${id}`)

/* ---------- Helper ---------- */

export function getSensusErrorMessage(error: any, fallback: string) {
  if (error?.response?.status === 403) return error?.response?.data?.message || 'Anda tidak memiliki akses.'
  return error?.response?.data?.message || fallback
}

// Jenis sensus untuk halaman sub-layanan RNA (null = menu ini bukan halaman sensus).
export async function getJenisByMenu(menuCode: string): Promise<SnsJenisPage | null> {
  return (await http.get<SnsJenisPage | ''>(`/sensus/jenis-by-menu/${encodeURIComponent(menuCode)}`)).data || null
}

/* ---------- Persil (Master Kebun & Blok, dari MyPAD / RSUPPKB) ---------- */

export async function getPersil(params: { sumber: PersilSumber; wilayah?: string; search?: string }): Promise<PersilResult> {
  return (await http.get<PersilResult>('/sensus/persil', { params: clean(params) })).data
}

export async function getPersilWilayah(sumber: PersilSumber): Promise<string[]> {
  return (await http.get<string[]>('/sensus/persil/wilayah', { params: { sumber } })).data
}
