import { http } from '../../api/http'

export interface RnaJenisRingkasan {
  id: number
  code: string
  name: string
  kategori: string // HPT | TANAMAN
  komoditas: string
  description?: string | null
  sensusPeriode: number
  menungguApproval: number
  revisi: number
  disetujui: number
  persilDinilai: number
  berat: number
  sedang: number
  ringan: number
  nihil: number
  sensusTerakhir?: string | null
}

export interface RnaRingkasan {
  dateFrom: string
  dateTo: string
  jenis: RnaJenisRingkasan[]
}

export async function getRnaRingkasan(hari = 90): Promise<RnaRingkasan> {
  return (await http.get<RnaRingkasan>('/divisi/rna/ringkasan', { params: { hari } })).data
}
