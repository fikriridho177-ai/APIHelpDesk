import * as XLSX from 'xlsx'
import type { CurahHujanMonitorItem } from '../types'

const FAKTOR_KOREKSI = 0.7

export type DownloadInterval =
  | '5m'
  | 'hour'
  | 'day'
  | 'month'
  | 'year'

export type DownloadFormat =
  | 'csv'
  | 'xlsx'

export interface StationOption {
  value: string
  label: string
}

export interface DownloadRow {
  Periode: string
  Stasiun: string
  JumlahData: number
  CurahHujan: number
  IntensitasHujan: number
  KelembapanTanah: number
  TMAK: number
}

interface NormalizedRow {
  date: Date

  stationKey: string
  stationLabel: string

  curahHujan: number
  intensitasHujan: number

  kelembapanTanah: number
  tmak: number

  nDataJam: number
}

function parseTanggal(
  value: string,
): Date | null {
  if (!value) {
    return null
  }

  const normalized =
    value.includes('T')
      ? value
      : value.replace(' ', 'T')

  const date =
    new Date(normalized)

  if (
    Number.isNaN(
      date.getTime(),
    )
  ) {
    return null
  }

  return date
}

function pad(
  value: number,
): string {
  return String(value)
    .padStart(2, '0')
}

function fullDateTime(
  date: Date,
): string {
  return (
    `${date.getFullYear()}-` +
    `${pad(date.getMonth() + 1)}-` +
    `${pad(date.getDate())} ` +
    `${pad(date.getHours())}:` +
    `${pad(date.getMinutes())}`
  )
}

function stationIdentity(
  row: CurahHujanMonitorItem,
): {
  key: string
  label: string
} | null {

  const rawKey =
    row.Lokasi ??
    row.IdSensor

  if (
    rawKey === null ||
    rawKey === undefined
  ) {
    return null
  }

  const key =
    String(rawKey)

  const titik =
    String(
      row.TitikIpal ?? '',
    ).trim()

  const wilayah =
    String(
      row.KodeWilayah ?? '',
    ).trim()

  let label = titik

  if (!label) {
    label =
      `Stasiun ${key}`
  }

  if (
    wilayah &&
    wilayah !== label
  ) {
    label +=
      ` - ${wilayah}`
  }

  return {
    key,
    label,
  }
}

/**
 * Isi dropdown Stasiun.
 */
export function buildStationOptions(
  rows: CurahHujanMonitorItem[],
): StationOption[] {

  const map =
    new Map<
      string,
      string
    >()

  for (const row of rows) {
    const station =
      stationIdentity(row)

    if (!station) {
      continue
    }

    map.set(
      station.key,
      station.label,
    )
  }

  return Array.from(
    map.entries(),
  )
    .map(
      ([value, label]) => ({
        value,
        label,
      }),
    )
    .sort(
      (a, b) =>
        a.label.localeCompare(
          b.label,
        ),
    )
}

/**
 * Normalisasi data terlebih dahulu.
 *
 * Curah =
 * NilaiCurahHujan × 0.7
 *
 * Intensitas per data =
 * Curah data / jumlah data
 * pada jam kalender tersebut.
 *
 * TMAK =
 * 30 - NilaiTinggiAir
 */
function normalizeRows(
  rows: CurahHujanMonitorItem[],
): NormalizedRow[] {

  const base = rows
    .map(row => {
      const date =
        parseTanggal(
          row.Tanggal ?? '',
        )

      const station =
        stationIdentity(row)

      if (
        !date ||
        !station
      ) {
        return null
      }

      const rawCH =
        Number(
          row.NilaiCurahHujan ??
            0,
        ) || 0

      const curahHujan =
        rawCH *
        FAKTOR_KOREKSI

      const kelembapanTanah =
        Number(
          row.NilaiKelembapanTanah ??
            0,
        ) || 0

      const tinggiRaw =
        Number(
          row.NilaiTinggiAir ??
            0,
        ) || 0

      const tmak =
        30 -
        tinggiRaw

      const hourKey =
        `${station.key}|` +
        `${date.getFullYear()}-` +
        `${pad(
          date.getMonth() + 1,
        )}-` +
        `${pad(
          date.getDate(),
        )}-` +
        `${pad(
          date.getHours(),
        )}`

      return {
        date,
        hourKey,

        stationKey:
          station.key,

        stationLabel:
          station.label,

        curahHujan,

        kelembapanTanah,

        tmak,
      }
    })
    .filter(
      (
        item,
      ): item is NonNullable<
        typeof item
      > =>
        item !== null,
    )

  /*
   * Hitung n per jam kalender
   * untuk masing-masing stasiun.
   */
  const hourlyCount =
    new Map<
      string,
      number
    >()

  for (const row of base) {
    hourlyCount.set(
      row.hourKey,

      (
        hourlyCount.get(
          row.hourKey,
        ) ?? 0
      ) + 1,
    )
  }

  return base
    .map(row => {
      const nDataJam =
        hourlyCount.get(
          row.hourKey,
        ) ?? 1

      return {
        date:
          row.date,

        stationKey:
          row.stationKey,

        stationLabel:
          row.stationLabel,

        curahHujan:
          row.curahHujan,

        /*
         * Intensitas per-record:
         *
         * (NilaiCH × 0.7)
         * ----------------
         * n data dalam jam
         */
        intensitasHujan:
          row.curahHujan /
          nDataJam,

        kelembapanTanah:
          row.kelembapanTanah,

        tmak:
          row.tmak,

        nDataJam,
      }
    })
    .sort(
      (a, b) =>
        a.date.getTime() -
        b.date.getTime(),
    )
}

function groupKey(
  row: NormalizedRow,
  interval: DownloadInterval,
): {
  key: string
  label: string
} {
  const d =
    row.date

  const year =
    d.getFullYear()

  const month =
    pad(
      d.getMonth() + 1,
    )

  const day =
    pad(
      d.getDate(),
    )

  const hour =
    pad(
      d.getHours(),
    )

  switch (interval) {
    case 'hour':
      return {
        key:
          `${row.stationKey}|${year}-${month}-${day}-${hour}`,

        label:
          `${year}-${month}-${day} ${hour}:00`,
      }

    case 'day':
      return {
        key:
          `${row.stationKey}|${year}-${month}-${day}`,

        label:
          `${year}-${month}-${day}`,
      }

    case 'month':
      return {
        key:
          `${row.stationKey}|${year}-${month}`,

        label:
          `${year}-${month}`,
      }

    case 'year':
      return {
        key:
          `${row.stationKey}|${year}`,

        label:
          `${year}`,
      }

    default:
      return {
        key:
          `${row.stationKey}|${d.getTime()}`,

        label:
          fullDateTime(d),
      }
  }
}

function round(
  value: number,
  digits = 4,
): number {
  return Number(
    value.toFixed(
      digits,
    ),
  )
}

/**
 * Menghasilkan data sesuai interval.
 *
 * Per 5 menit:
 * - data aktual per record
 *
 * Per Jam/Harian/Bulanan/Tahunan:
 * - Curah Hujan = SUM
 * - Intensitas = AVG
 * - Kelembaban = AVG
 * - TMAK = AVG
 */
export function prepareDownloadRows(
  rows: CurahHujanMonitorItem[],
  interval: DownloadInterval,
  stationKey: string,
): DownloadRow[] {

  const normalized =
    normalizeRows(rows)
      .filter(
        row =>
          row.stationKey ===
          stationKey,
      )

  /*
   * PER 5 MENIT
   */
  if (
    interval === '5m'
  ) {
    return normalized.map(
      row => ({
        Periode:
          fullDateTime(
            row.date,
          ),

        Stasiun:
          row.stationLabel,

        /*
         * n yang digunakan
         * untuk menghitung
         * intensitas.
         */
        JumlahData:
          row.nDataJam,

        CurahHujan:
          round(
            row.curahHujan,
          ),

        IntensitasHujan:
          round(
            row.intensitasHujan,
          ),

        KelembapanTanah:
          round(
            row.kelembapanTanah,
            2,
          ),

        TMAK:
          round(
            row.tmak,
            2,
          ),
      }),
    )
  }

  /*
   * AGREGASI
   */
  const groups =
    new Map<
      string,
      {
        label: string
        station: string

        count: number

        curah: number
        intensitas: number
        kelembapan: number
        tmak: number
      }
    >()

  for (
    const row of normalized
  ) {
    const bucket =
      groupKey(
        row,
        interval,
      )

    const current =
      groups.get(
        bucket.key,
      ) ?? {
        label:
          bucket.label,

        station:
          row.stationLabel,

        count: 0,

        curah: 0,
        intensitas: 0,
        kelembapan: 0,
        tmak: 0,
      }

    current.count += 1

    /*
     * Curah = SUM
     */
    current.curah +=
      row.curahHujan

    /*
     * Yang lain nanti
     * dibagi count = AVG
     */
    current.intensitas +=
      row.intensitasHujan

    current.kelembapan +=
      row.kelembapanTanah

    current.tmak +=
      row.tmak

    groups.set(
      bucket.key,
      current,
    )
  }

  return Array.from(
    groups.values(),
  )
    .map(group => ({
      Periode:
        group.label,

      Stasiun:
        group.station,

      JumlahData:
        group.count,

      /*
       * SUM
       */
      CurahHujan:
        round(
          group.curah,
        ),

      /*
       * AVG
       */
      IntensitasHujan:
        round(
          group.intensitas /
            group.count,
        ),

      KelembapanTanah:
        round(
          group.kelembapan /
            group.count,
          2,
        ),

      TMAK:
        round(
          group.tmak /
            group.count,
          2,
        ),
    }))
}

function escapeCsv(
  value: unknown,
): string {
  const text =
    String(
      value ?? '',
    )

  if (
    text.includes(';') ||
    text.includes('"') ||
    text.includes('\n')
  ) {
    return (
      '"' +
      text.replace(
        /"/g,
        '""',
      ) +
      '"'
    )
  }

  return text
}

function downloadBlob(
  blob: Blob,
  filename: string,
) {
  const url =
    URL.createObjectURL(
      blob,
    )

  const link =
    document.createElement(
      'a',
    )

  link.href = url
  link.download =
    filename

  document.body.appendChild(
    link,
  )

  link.click()
  link.remove()

  URL.revokeObjectURL(
    url,
  )
}

export function exportMonitoringCsv(
  rows: DownloadRow[],
  filename: string,
) {
  const headers: Array<
    keyof DownloadRow
  > = [
    'Periode',
    'Stasiun',
    'JumlahData',
    'CurahHujan',
    'IntensitasHujan',
    'KelembapanTanah',
    'TMAK',
  ]

  const content = [
    headers.join(';'),

    ...rows.map(row =>
      headers
        .map(header =>
          escapeCsv(
            row[header],
          ),
        )
        .join(';'),
    ),
  ].join('\r\n')

  /*
   * BOM agar Excel Windows
   * membaca UTF-8 dengan baik.
   */
  const blob =
    new Blob(
      [
        '\uFEFF',
        content,
      ],
      {
        type:
          'text/csv;charset=utf-8;',
      },
    )

  downloadBlob(
    blob,
    `${filename}.csv`,
  )
}

export function exportMonitoringXlsx(
  rows: DownloadRow[],
  filename: string,
) {
  const worksheet =
    XLSX.utils.json_to_sheet(
      rows,
    )

  /*
   * Lebar kolom.
   */
  worksheet['!cols'] = [
    { wch: 20 },
    { wch: 25 },
    { wch: 12 },
    { wch: 15 },
    { wch: 18 },
    { wch: 20 },
    { wch: 15 },
  ]

  const workbook =
    XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(
    workbook,
    worksheet,
    'Data Sensor',
  )

  XLSX.writeFile(
    workbook,
    `${filename}.xlsx`,
  )
}