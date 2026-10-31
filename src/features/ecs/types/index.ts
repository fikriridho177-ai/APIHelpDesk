export interface User {
  UserID: string
  NamaLengkap?: string
  KodeWilayah?: string
  Jabatan?: string
  lanjut?: string
  status?: string
}

export interface DateRange {
  from: string
  to: string
}

// export interface CurahHujanItem {
//   Tanggal: string
//   KodePersil: string
//   Pagi: number | null
//   Sore: number | null
//   KoordinatX: string | null
//   KoordinatY: string | null
//   response?: string
// }
export interface CurahHujanItem {
  Tanggal: string
  Lokasi: number | string | null
  IdSensor: number | string | null

  NilaiCurahHujan: number | null
  NilaiTerkoreksi: number | null

  NilaiKelembapanTanah: number | null
  NilaiTinggiAir: number | null
  PeriodePerJam: number | null

  // Format lama (dipakai halaman Curah Hujan).
  KodePersil?: string | null
  Pagi?: number | null
  Sore?: number | null
}

export interface TmakItem {
  Tanggal: string
  KodeBlok: string
  Nilai: number | null
  KoordinatX: string | null
  KoordinatY: string | null
  ket: string | null
  response?: string
}

export interface KelembapanItem {
  Tanggal: string
  Lokasi: string | null
  NilaiKelembapan: number | null
  IdSensor: string | null
}

export type ApiResponse<T> = T[] | [{ response: 'failed' }]

export interface Threshold {
  warning: number
  danger: number
}

export type SensorStatus = 'normal' | 'warning' | 'danger'

export function getStatus(value: number, threshold: Threshold): SensorStatus {
  if (value >= threshold.danger) return 'danger'
  if (value >= threshold.warning) return 'warning'
  return 'normal'
}

export const THRESHOLDS = {
  curahHujan: { warning: 50,  danger: 100 } as Threshold,
  tmak:       { warning: 150, danger: 200 } as Threshold,
  kelembapan: { warning: 30,  danger: 80  } as Threshold,
}
//CH
// export interface CurahHujanMonitorItem {
//   Tanggal: string

//   Lokasi: number | string | null
//   IdSensor: number | string | null

//   NilaiCurahHujan: number | string | null
//   NilaiTerkoreksi?: number | string | null

//   NilaiKelembapanTanah: number | string | null
//   NilaiTinggiAir: number | string | null

//   PeriodePerJam: number | string | null

//   KodeWilayah?: string | null

//   // TAMBAHKAN
//   TitikIpal?: string | null
//   NamaSensor?: string | null

//   Latitude?: string | number | null
//   Longitude?: string | number | null
// }
export interface CurahHujanMonitorItem {
  HasilID?: number | null
  RawDtlID?: number | null
  KalibrasiID?: number | null

  NilaiHasil?: number | string | null
  Satuan?: string | null

  VersiProses?: number | null
  IsCurrent?: boolean | number | null
  StatusHasil?: string | null

  ProcessedAt?: string | null

  RawID?: number | null
  SensorParameterID?: number | null

  RawValue?: number | string | null
  RawText?: string | null

  StatusData?: string | null
  Versi?: number | null
  TipeKalibrasi?: string | null

  BerlakuMulai?: string | null
  BerlakuSampai?: string | null

  Aktif?: boolean | number | null
  Keterangan?: string | null

  RawUnit?: string | null
  OutputUnit?: string | null

  NamaLogger?: string | null
  LoggerID?: number | string | null

  SensorCode?: string | null
  NamaSensor?: string | null

  // Field format lama - masih dibaca Data Historis, Kalender & unduhan.
  Tanggal?: string | null
  Lokasi?: number | string | null
  IdSensor?: number | string | null
  NilaiCurahHujan?: number | string | null
  NilaiTerkoreksi?: number | string | null
  NilaiKelembapanTanah?: number | string | null
  NilaiTinggiAir?: number | string | null
  PeriodePerJam?: number | string | null
  KodeWilayah?: string | null
  TitikIpal?: string | null
  Latitude?: string | number | null
  Longitude?: string | number | null
}

export interface IntensitasItem {
  jam: string              // label jam (misal "08:00")
  intensitas: number       // mm/jam = akumulasi data dalam 1 jam / 60 menit * 10
  jumlahData: number
  akumulasi: number
}
