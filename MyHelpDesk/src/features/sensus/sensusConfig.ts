import type { PersilSumber, SnsStatus, SnsTingkat } from './types/sensus'

export const SNS_MENU = {
  approval: 'SNS_APPROVAL',
  monitoring: 'SNS_MONITORING',
  analisis: 'SNS_ANALISIS',
  masterLokasi: 'SNS_MASTER_LOKASI',
  masterJenis: 'SNS_MASTER_JENIS',
} as const

export const SNS_STATUS: Record<SnsStatus, { label: string; tone: string }> = {
  SUBMITTED: { label: 'Menunggu Approval', tone: 'blue' },
  APPROVED: { label: 'Disetujui', tone: 'green' },
  REVISI: { label: 'Perlu Revisi', tone: 'orange' },
  REJECTED: { label: 'Ditolak', tone: 'red' },
}

export const SNS_TINGKAT: Record<SnsTingkat, { label: string; tone: string }> = {
  '-': { label: '-', tone: 'gray' },
  NIHIL: { label: 'Nihil', tone: 'green' },
  RINGAN: { label: 'Ringan', tone: 'teal' },
  SEDANG: { label: 'Sedang', tone: 'amber' },
  BERAT: { label: 'Berat', tone: 'red' },
}

export const SNS_LOG_LABEL: Record<string, string> = {
  SUBMIT: 'Dikirim dari Android',
  RESUBMIT: 'Dikirim ulang',
  APPROVE: 'Disetujui',
  REJECT: 'Ditolak',
  REVISI: 'Diminta revisi',
}

/* ---------- Format ---------- */

export function todayIso(offsetDays = 0) {
  const date = new Date()
  date.setDate(date.getDate() + offsetDays)
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

export function formatTanggal(value?: string | null) {
  if (!value) return '-'
  // DateOnly dari API ("2026-10-13") dibaca sebagai tanggal lokal agar tidak bergeser zona waktu.
  const [y, m, d] = value.slice(0, 10).split('-').map(Number)
  return new Date(y, m - 1, d).toLocaleDateString('id-ID', { day: 'numeric', month: 'short', year: 'numeric' })
}

export function formatWaktu(value?: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleString('id-ID', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export function formatAngka(value?: number | null, digits = 2) {
  if (value === null || value === undefined) return '-'
  return value.toLocaleString('id-ID', { maximumFractionDigits: digits })
}

export function persen(part: number, total: number) {
  return total === 0 ? 0 : Math.round((part * 10000) / total) / 100
}

// Unduh CSV (permission Export).
export function downloadCsv(fileName: string, header: string[], rows: unknown[][]) {
  const escape = (value: unknown) => `"${String(value ?? '').replace(/"/g, '""')}"`
  const lines = [header, ...rows].map(row => row.map(escape).join(','))
  const blob = new Blob(['﻿' + lines.join('\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${fileName}-${todayIso()}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

/* ---------- Sumber persil (MyPAD / RSUPPKB) ---------- */

export const PERSIL_SUMBER: Record<PersilSumber, { label: string; database: string; komoditas: 'KELAPA' | 'NANAS' }> = {
  KLP: { label: 'Persil Kelapa', database: 'MyPAD', komoditas: 'KELAPA' },
  TNM_NNS: { label: 'Tanaman Nanas', database: 'MyPAD', komoditas: 'NANAS' },
  LHN_NNS: { label: 'Lahan Nanas', database: 'MyPAD', komoditas: 'NANAS' },
  PNN: { label: 'Persil Kelapa RSUPPKB', database: 'RSUPPKB', komoditas: 'KELAPA' },
}

// Teks lokasi singkat: "W01 · Afd 02 · KS-1234"
export function formatLokasi(x: { kodeWilayah?: string | null; kodeAfdeling?: string | null; persilKode: string }) {
  return [x.kodeWilayah, x.kodeAfdeling ? `Afd ${x.kodeAfdeling}` : null, x.persilKode].filter(Boolean).join(' · ')
}
