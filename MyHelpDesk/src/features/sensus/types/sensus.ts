export type SnsStatus = 'SUBMITTED' | 'APPROVED' | 'REJECTED' | 'REVISI'
export type SnsTingkat = '-' | 'NIHIL' | 'RINGAN' | 'SEDANG' | 'BERAT'
export type SnsAction = 'APPROVE' | 'REJECT' | 'REVISI'
export type SnsTipeData = 'NUMBER' | 'TEXT' | 'BOOLEAN' | 'OPTION'

/* ---------- Master ---------- */

export interface SnsParameter {
  id: number
  jenisId: number
  code: string
  name: string
  satuan?: string | null
  tipeData: SnsTipeData
  opsi?: string | null
  wajib: boolean
  isIndikator: boolean
  ambangSedang?: number | null
  ambangBerat?: number | null
  active: boolean
}

export interface SnsJenis {
  id: number
  code: string
  name: string
  kategori: 'HPT' | 'TANAMAN'
  komoditas: 'KELAPA' | 'NANAS' | 'SEMUA'
  menuCode?: string | null
  description?: string | null
  active: boolean
  parameters: SnsParameter[]
}

/* ---------- Sensus ---------- */

export interface SnsListItem {
  id: number
  sensusNo?: string | null
  jenisId: number
  jenisName: string
  kategori: string
  persilSumber: PersilSumber
  persilId: string
  persilKode: string
  kodeWilayah?: string | null
  kodeAfdeling?: string | null
  komoditas: string
  tanggalSensus: string
  petugasUserId: number
  petugasName: string
  jumlahPokokSampel: number
  jumlahPokokTerserang: number
  status: SnsStatus
  tingkat: SnsTingkat
  submittedAt: string
  approvedAt?: string | null
}

export interface SnsDetailValue {
  id: number
  parameterId: number
  parameterName: string
  satuan?: string | null
  tipeData: SnsTipeData
  isIndikator: boolean
  noPokok?: number | null
  nilaiAngka?: number | null
  nilaiTeks?: string | null
}

export interface SnsParameterSummary {
  parameterId: number
  parameterName: string
  satuan?: string | null
  isIndikator: boolean
  rata2?: number | null
  total?: number | null
  jumlahData: number
  tingkat: SnsTingkat
}

export interface SnsDetail {
  summary: SnsListItem
  latitude?: number | null
  longitude?: number | null
  catatan?: string | null
  deviceInfo?: string | null
  approvedByName?: string | null
  approvalNote?: string | null
  canApprove: boolean
  ringkasan: SnsParameterSummary[]
  details: SnsDetailValue[]
  fotos: { id: number; fileUrl: string; keterangan?: string | null; latitude?: number | null; longitude?: number | null; takenAt?: string | null }[]
  logs: { id: number; action: string; fromStatus?: string | null; toStatus?: string | null; note?: string | null; userName: string; createdAt: string }[]
}

export interface SnsFilter {
  status?: string
  jenisId?: number
  wilayah?: string
  afdeling?: string
  persilSumber?: PersilSumber
  persilId?: string
  petugasUserId?: number
  dateFrom?: string
  dateTo?: string
  search?: string
}

export interface SnsCount {
  key: string
  label: string
  total: number
  submitted: number
  approved: number
  rejected: number
  revisi: number
}

export interface SnsPersilTerlambat {
  persilSumber: PersilSumber
  persilId: string
  persilKode: string
  kodeWilayah?: string | null
  kodeAfdeling?: string | null
  jenisId: number
  jenisName: string
  terakhirSensus?: string | null
  hariSejak?: number | null
}

export interface SnsMonitoring {
  total: number
  submitted: number
  approved: number
  rejected: number
  revisi: number
  perJenis: SnsCount[]
  perPetugas: SnsCount[]
  perWilayah: SnsCount[]
  perHari: SnsCount[]
  sumberTerlambat: PersilSumber
  persilTerlambat: SnsPersilTerlambat[]
  persilTerlambatError?: string | null
}

export interface SnsAnalisisPersil {
  persilSumber: PersilSumber
  persilId: string
  persilKode: string
  kodeWilayah?: string | null
  kodeAfdeling?: string | null
  jumlahSensus: number
  terakhirSensus: string
  totalPokokSampel: number
  totalPokokTerserang: number
  persenTerserang: number
  rataIndikator?: number | null
  tingkat: SnsTingkat
}

export interface SnsAnalisis {
  jenisId: number
  jenisName: string
  indikatorName?: string | null
  indikatorSatuan?: string | null
  ambangSedang?: number | null
  ambangBerat?: number | null
  jumlahSensus: number
  jumlahPersil: number
  persenTerserang: number
  sebaranTingkat: Record<SnsTingkat, number>
  perPersil: SnsAnalisisPersil[]
  tren: { periode: string; jumlahSensus: number; persenTerserang: number; rataIndikator?: number | null }[]
}

export interface SnsJenisPage {
  jenis: SnsJenis
  canRead: boolean
  canApprove: boolean
  canExport: boolean
}

/* ---------- Persil (MyPAD / RSUPPKB, hanya dibaca) ---------- */

export type PersilSumber = 'KLP' | 'TNM_NNS' | 'LHN_NNS' | 'PNN'

export interface Persil {
  kode: string
  afdeling?: string | null
  wilayah?: string | null
  latitude?: number | null
  longitude?: number | null
  persilId?: string | null
  // Kolom persil (KodePersil / Persil / NamaPersil / NoPersil, yang pertama ada di tabel).
  persil?: string | null
  // Semua kolom tabel sumber, kunci = nama kolom asli di database, mis. p.detail?.KodePersil
  detail?: Record<string, string | number | boolean | null>
}

export interface PersilResult {
  sumber: PersilSumber
  total: number
  terpotong: boolean
  rows: Persil[]
}
