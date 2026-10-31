import { http } from '../../../api/http'

import type {
  CreateLynRequestPayload,
  LynActionPayload,
  LynJenis,
  LynJenisList,
  LynReport,
  LynReportFilter,
  LynRequestDetail,
  LynRequestItem,
  LynRequesterProfile,
  LynService,
  LynServiceNode,
  LynSummary,
  SaveLynJenisPayload,
} from '../types/layanan'

export async function getLayananServices(): Promise<LynService[]> {
  return (await http.get<LynService[]>('/layanan/services')).data
}

export async function getLayananRequests(params: {
  menuCode?: string
  status?: string
  search?: string
  mine?: boolean
} = {}): Promise<LynRequestItem[]> {
  return (await http.get<LynRequestItem[]>('/layanan/requests', { params })).data
}

export async function getLayananRequest(id: number): Promise<LynRequestDetail> {
  return (await http.get<LynRequestDetail>(`/layanan/requests/${id}`)).data
}

export async function createLayananRequest(payload: CreateLynRequestPayload): Promise<LynRequestDetail> {
  return (await http.post<LynRequestDetail>('/layanan/requests', payload)).data
}

export async function getRequesterProfile(): Promise<LynRequesterProfile> {
  return (await http.get<LynRequesterProfile>('/layanan/requester-profile')).data
}

export async function runLayananAction(id: number, payload: LynActionPayload): Promise<LynRequestDetail> {
  return (await http.post<LynRequestDetail>(`/layanan/requests/${id}/actions`, payload)).data
}

export async function getLayananSummary(mine = false): Promise<LynSummary> {
  return (await http.get<LynSummary>('/layanan/summary', { params: { mine } })).data
}

/* ---------- Helper ---------- */

export function getLayananErrorMessage(error: any, fallback: string) {
  return error?.response?.data?.message || fallback
}

// Susun daftar flat dari API menjadi tree (urut sesuai SortOrder di Manajemen Menu).
export function buildServiceTree(services: LynService[]): LynServiceNode[] {
  const nodes = new Map<number, LynServiceNode>(services.map(s => [s.id, { ...s, children: [] }]))
  const roots: LynServiceNode[] = []

  nodes.forEach(node => {
    const parent = node.parentId != null ? nodes.get(node.parentId) : undefined
    if (parent) parent.children.push(node)
    else roots.push(node)
  })

  const sort = (list: LynServiceNode[]) => {
    list.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
    list.forEach(item => sort(item.children))
  }
  sort(roots)

  return roots
}

// Semua sub-layanan (MENU) di bawah sebuah node, termasuk yang bertingkat.
export function flattenServices(node: LynServiceNode): LynServiceNode[] {
  if (node.menuType === 'MENU') return [node]
  return node.children.flatMap(flattenServices)
}

// Divisi = anak langsung dari menu induk LAYANAN yang bertipe GROUP.
export function getDivisions(tree: LynServiceNode[]): LynServiceNode[] {
  const root = tree.find(node => node.code === 'LAYANAN')
  const candidates = root ? root.children : tree
  return candidates.filter(node => node.menuType === 'GROUP' && (node.jenisCount > 0 || flattenServices(node).length > 0))
}

/* ---------- Master Jenis Permintaan ---------- */

// all=false: jenis aktif untuk form; all=true: semua jenis (halaman master).
export async function getJenisList(params: { divisionCode?: string; all?: boolean } = {}): Promise<LynJenisList> {
  return (await http.get<LynJenisList>('/layanan/jenis', { params })).data
}

export async function saveJenis(id: number | null, payload: SaveLynJenisPayload): Promise<LynJenis> {
  return id == null
    ? (await http.post<LynJenis>('/layanan/jenis', payload)).data
    : (await http.put<LynJenis>(`/layanan/jenis/${id}`, payload)).data
}

export async function deleteJenis(id: number): Promise<{ deactivated: boolean; message: string }> {
  return (await http.delete(`/layanan/jenis/${id}`)).data
}

/* ---------- Monitoring & Analisis ---------- */

export async function getLayananReport(filter: LynReportFilter): Promise<LynReport> {
  return (await http.get<LynReport>('/layanan/report', { params: filter })).data
}
