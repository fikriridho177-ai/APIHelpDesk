export type LynStatus =
  | 'BARU'
  | 'DITERIMA'
  | 'DIJADWALKAN'
  | 'DIPROSES'
  | 'MENUNGGU_INFO'
  | 'SELESAI'
  | 'DITOLAK'
  | 'DIBATALKAN'

// Skala prioritas mengikuti form permintaan: Normal, Sedang, Tinggi, Urgent.
export type LynPriority = 'NORMAL' | 'SEDANG' | 'TINGGI' | 'URGENT'

export type LynAction = 'ACCEPT' | 'REJECT' | 'SCHEDULE' | 'PROCESS' | 'NEED_INFO' | 'FINISH' | 'CANCEL' | 'COMMENT'

export interface LynPermission {
  canView: boolean
  canCreate: boolean
  canRead: boolean
  canUpdate: boolean
  canDelete: boolean
  canApprove: boolean
  canExport: boolean
}

export interface LynService {
  id: number
  parentId?: number | null
  code: string
  name: string
  route?: string | null
  icon?: string | null
  fullName?: string | null
  description?: string | null
  color?: string | null
  menuType: 'GROUP' | 'MENU'
  sortOrder: number
  permissions: LynPermission
  // Jumlah jenis permintaan aktif (menu divisi).
  jenisCount: number
}

// LynService yang sudah disusun menjadi tree.
export interface LynServiceNode extends LynService {
  children: LynServiceNode[]
}

export interface LynRequestItem {
  id: number
  requestNo?: string | null
  menuId: number
  serviceCode: string
  serviceName: string
  divisionName?: string | null
  title: string
  priority: LynPriority
  status: LynStatus
  requesterUserId: number
  requesterName: string
  requesterDeptId?: string | null
  createdAt: string
  scheduledAt?: string | null
  updatedAt?: string | null
  jenisId?: number | null
  // Nomor antrian di divisinya (tiket Baru / Disetujui / Dijadwalkan).
  queueNo?: number | null
  allowedActions: LynAction[]
}

export interface LynRequestLog {
  id: number
  action: string
  fromStatus?: LynStatus | null
  toStatus?: LynStatus | null
  note?: string | null
  userName: string
  createdAt: string
}

export interface LynRequestDetail {
  summary: LynRequestItem
  description: string
  location?: string | null
  neededDate?: string | null
  assignee?: string | null
  closedAt?: string | null
  requesterUserLogin: string
  requesterCompanyId?: number | null
  requesterPosition?: string | null
  requesterDepartment?: string | null
  requesterCompanies?: string | null
  requesterPhone?: string | null
  requesterEmail?: string | null
  otherType?: string | null
  allowedActions: LynAction[]
  logs: LynRequestLog[]
}

export interface LynSummary {
  baru: number
  dalamProses: number
  menungguInfo: number
  selesai: number
  ditolak: number
  total: number
}

// Isian form pengajuan (mengikuti form permintaan GIS). Judul dibuat otomatis oleh server.
export interface CreateLynRequestPayload {
  // Jenis dari master; menuCode hanya untuk sub-layanan model lama.
  jenisId?: number
  menuCode?: string
  otherType?: string
  requesterName: string
  requesterPosition: string
  requesterDepartment: string
  companies: string[]
  otherCompany?: string
  requesterPhone: string
  requesterEmail: string
  description: string
  location: string
  neededDate: string
  priority: LynPriority
}

// Data akun login untuk mengisi awal bagian pemohon.
export interface LynRequesterProfile {
  name: string
  email: string
  phone?: string | null
  deptId?: string | null
  companyId?: number | null
}

export interface LynActionPayload {
  action: LynAction
  note?: string
  scheduledAt?: string
  assignee?: string
}

/* ---------- Master Jenis Permintaan ---------- */

export interface LynJenis {
  id: number
  divisionMenuId: number
  divisionCode: string
  divisionName: string
  code: string
  name: string
  description?: string | null
  // Jenis "Lainnya": pemohon menuliskan sendiri jenis permintaannya.
  isOther: boolean
  sortOrder: number
  active: boolean
  usedCount: number
}

export interface LynDivisionOption {
  id: number
  code: string
  name: string
  canManage: boolean
}

export interface LynJenisList {
  divisions: LynDivisionOption[]
  items: LynJenis[]
}

export interface SaveLynJenisPayload {
  divisionMenuId: number
  code?: string
  name: string
  description?: string
  isOther: boolean
  sortOrder: number
  active: boolean
}

/* ---------- Monitoring & Analisis ---------- */

export interface LynReportFilter {
  dateFrom?: string
  dateTo?: string
  divisionCode?: string
  jenisId?: number
  status?: string
}

export interface LynReportGroup {
  division: string
  label: string
  total: number
  menungguApproval: number
  disetujui: number
  dalamProses: number
  selesai: number
  ditolakBatal: number
  terlambat: number
  rataHariSelesai?: number | null
  persenTepatWaktu?: number | null
}

export interface LynReportRow {
  id: number
  requestNo?: string | null
  divisionName: string
  jenisName: string
  title: string
  description: string
  location?: string | null
  priority: LynPriority
  status: LynStatus
  requesterName: string
  requesterPosition?: string | null
  requesterDepartment?: string | null
  requesterCompanies?: string | null
  requesterPhone?: string | null
  createdAt: string
  neededDate?: string | null
  scheduledAt?: string | null
  closedAt?: string | null
  assignee?: string | null
  queueNo?: number | null
  terlambat: boolean
  hariSelesai?: number | null
}

export interface LynReport {
  dateFrom: string
  dateTo: string
  total: LynReportGroup
  perDivisi: LynReportGroup[]
  perJenis: LynReportGroup[]
  perPrioritas: LynReportGroup[]
  perBulan: { month: string; masuk: number; selesai: number }[]
  rows: LynReportRow[]
}
