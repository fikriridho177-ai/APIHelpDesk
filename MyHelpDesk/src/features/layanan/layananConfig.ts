import { pickIconName, type IconName } from '../dashboard/components/DashboardIcon'
import type { LynAction, LynPriority, LynService, LynStatus } from './types/layanan'

/* ---------- Status ---------- */

export const STATUS_CONFIG: Record<LynStatus, { label: string; tone: string }> = {
  BARU: { label: 'Baru', tone: 'blue' },
  DITERIMA: { label: 'Disetujui', tone: 'indigo' },
  DIJADWALKAN: { label: 'Dijadwalkan', tone: 'purple' },
  DIPROSES: { label: 'Diproses', tone: 'amber' },
  MENUNGGU_INFO: { label: 'Menunggu Informasi', tone: 'orange' },
  SELESAI: { label: 'Selesai', tone: 'green' },
  DITOLAK: { label: 'Ditolak', tone: 'red' },
  DIBATALKAN: { label: 'Dibatalkan', tone: 'gray' },
}

// Tahapan utama sesuai flowchart, untuk progress bar.
export const STATUS_STEPS: { status: LynStatus; label: string }[] = [
  { status: 'BARU', label: 'Isi Formulir' },
  { status: 'DITERIMA', label: 'Disetujui' },
  { status: 'DIJADWALKAN', label: 'Penjadwalan' },
  { status: 'DIPROSES', label: 'Proses Layanan' },
  { status: 'SELESAI', label: 'Selesai' },
]

export function getStepIndex(status: LynStatus) {
  if (status === 'MENUNGGU_INFO') return 3 // posisi setara "Proses Layanan"
  return STATUS_STEPS.findIndex(step => step.status === status)
}

/* ---------- Prioritas ---------- */

export const PRIORITY_CONFIG: Record<LynPriority, { label: string; tone: string }> = {
  NORMAL: { label: 'Normal', tone: 'gray' },
  SEDANG: { label: 'Sedang', tone: 'blue' },
  TINGGI: { label: 'Tinggi', tone: 'amber' },
  URGENT: { label: 'Urgent', tone: 'red' },
}

/* ---------- Form pengajuan ---------- */

// Pilihan "Perusahaan" sesuai form permintaan (boleh lebih dari satu + "Yang lain").
export const COMPANY_OPTIONS = [
  'PT RSUP - Industri',
  'PT RSUP - PKB',
  'PT RSTM',
  'PT GHS',
  'PT Pulau Sambu',
  'PT STI',
  'Yayasan Bahtera Dwipa Abadi',
  'Tay Juhana Foundation',
]

// Sub-layanan "Lainnya" (kode ..._LAINNYA) meminta isian jenis permintaan sendiri.
export function isOtherService(code: string) {
  return code.toUpperCase().endsWith('_LAINNYA')
}

/* ---------- Aksi ---------- */

export const ACTION_CONFIG: Record<
  LynAction,
  { label: string; tone: 'primary' | 'success' | 'danger' | 'neutral'; noteRequired?: boolean; schedule?: boolean; hint: string }
> = {
  ACCEPT: { label: 'Approve', tone: 'success', hint: 'Pengajuan disetujui dan siap dijadwalkan.' },
  SCHEDULE: { label: 'Jadwalkan', tone: 'primary', schedule: true, hint: 'Tentukan tanggal pelaksanaan dan petugas.' },
  PROCESS: { label: 'Mulai Proses', tone: 'primary', hint: 'Layanan sedang dikerjakan.' },
  NEED_INFO: { label: 'Minta Informasi', tone: 'neutral', noteRequired: true, hint: 'Minta data tambahan ke pemohon.' },
  FINISH: { label: 'Selesaikan', tone: 'success', hint: 'Tandai layanan sudah selesai.' },
  REJECT: { label: 'Tolak', tone: 'danger', noteRequired: true, hint: 'Tuliskan alasan penolakan.' },
  CANCEL: { label: 'Batalkan', tone: 'danger', hint: 'Pengajuan dibatalkan.' },
  COMMENT: { label: 'Kirim Catatan', tone: 'neutral', noteRequired: true, hint: 'Catatan tanpa mengubah status.' },
}

export const LOG_LABEL: Record<string, string> = {
  CREATE: 'Pengajuan dibuat',
  ACCEPT: 'Disetujui (approve)',
  REJECT: 'Ditolak',
  SCHEDULE: 'Dijadwalkan',
  PROCESS: 'Mulai diproses',
  NEED_INFO: 'Meminta informasi',
  FINISH: 'Selesai',
  CANCEL: 'Dibatalkan',
  COMMENT: 'Catatan',
}

/* ---------- Divisi (keterangan & warna kartu) ---------- */

// Nama lengkap, deskripsi, ikon & warna diambil dari tabel UtlMenu (diatur di Manajemen Menu).
type DivisionInfo = { fullName: string; description: string; icon: IconName; tone: string }

// Pilihan warna kartu (harus sesuai class .tone-* di layanan.css).
export const CARD_COLORS: { value: string; label: string }[] = [
  { value: 'blue', label: 'Biru' },
  { value: 'indigo', label: 'Indigo' },
  { value: 'purple', label: 'Ungu' },
  { value: 'green', label: 'Hijau' },
  { value: 'teal', label: 'Hijau Tosca' },
  { value: 'cyan', label: 'Biru Muda' },
  { value: 'amber', label: 'Kuning' },
  { value: 'orange', label: 'Oranye' },
  { value: 'red', label: 'Merah' },
  { value: 'gray', label: 'Abu-abu' },
]

const FALLBACK_TONES = ['blue', 'green', 'purple', 'orange', 'teal', 'cyan']

export function getDivisionInfo(division: LynService | undefined, index = 0): DivisionInfo {
  const color = division?.color?.trim().toLowerCase()

  return {
    fullName: division?.fullName?.trim() ?? '',
    description: division?.description?.trim() || 'Pilih layanan untuk membuat pengajuan.',
    icon: division ? pickIconName(division) : 'grid',
    tone: color && CARD_COLORS.some(c => c.value === color) ? color : FALLBACK_TONES[index % FALLBACK_TONES.length],
  }
}

/* ---------- Format ---------- */

export function formatDateTime(value?: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleString('id-ID', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export function formatDate(value?: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleDateString('id-ID', { day: 'numeric', month: 'long', year: 'numeric' })
}
