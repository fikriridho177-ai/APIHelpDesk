import type { CurahHujanMonitorItem } from '../types'
import { getRainfallCellStyle } from '../../iot/lib/rainThresholds'

export type HistoryInterval = '5m' | 'hour' | 'day' | 'month' | 'year'

export interface HistoryPoint {
  ts: number
  label: string
  curahHujan: number | null
  intensitasHujan: number | null
  kelembapanTanah: number | null
  tmak: number | null
}

// const FAKTOR_KOREKSI = 0.07
const GAP_5M_MS = 5 * 60 * 1000

function pad(n: number) {
  return String(n).padStart(2, '0')
}

function parseTanggal(value: string): Date | null {
  if (!value) return null
  const d = new Date(value.includes('T') ? value : value.replace(' ', 'T'))
  return Number.isNaN(d.getTime()) ? null : d
}

function normalizeRows(rows: CurahHujanMonitorItem[]) {
  const base = rows
    .map(row => {
      const date = parseTanggal(row.Tanggal ?? '')
      if (!date) return null

      // PENTING: station selalu berdasarkan IdSensor lebih dahulu.
      const stationKey = String(row.IdSensor ?? row.Lokasi ?? 'UNKNOWN').trim()
      // const curahHujan = (Number(row.NilaiCurahHujan ?? 0) || 0) * FAKTOR_KOREKSI
      const curahHujan = Number(row.NilaiTerkoreksi ?? 0) || 0
      const kelembapanTanah = Number(row.NilaiKelembapanTanah ?? 0) || 0
      const tmak = 30 - (Number(row.NilaiTinggiAir ?? 0) || 0)
      const hourKey = `${stationKey}|${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}-${pad(date.getHours())}`

      return {
        ts: date.getTime(), date, stationKey, hourKey,
        curahHujan, kelembapanTanah, tmak, intensitasHujan: 0,
      }
    })
    .filter((x): x is NonNullable<typeof x> => x !== null)

  const counts = new Map<string, number>()
  for (const row of base) {
    counts.set(row.hourKey, (counts.get(row.hourKey) ?? 0) + 1)
  }

  return base.map(row => {
    const n = counts.get(row.hourKey) ?? 1
    return {
      ...row,
      intensitasHujan: Number((row.curahHujan / n).toFixed(4)),
    }
  }).sort((a, b) => a.ts - b.ts)
}

function labelFor(date: Date, interval: HistoryInterval) {
  if (interval === '5m') return `${pad(date.getDate())}/${pad(date.getMonth() + 1)} ${pad(date.getHours())}:${pad(date.getMinutes())}`
  if (interval === 'hour') return `${pad(date.getDate())}/${pad(date.getMonth() + 1)} ${pad(date.getHours())}:00`
  if (interval === 'day') return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}`
  if (interval === 'month') return `${pad(date.getMonth() + 1)}/${date.getFullYear()}`
  return String(date.getFullYear())
}

function bucketKey(row: ReturnType<typeof normalizeRows>[number], interval: HistoryInterval) {
  const d = row.date
  if (interval === 'hour') return `${row.stationKey}|${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}-${pad(d.getHours())}`
  if (interval === 'day') return `${row.stationKey}|${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
  if (interval === 'month') return `${row.stationKey}|${d.getFullYear()}-${pad(d.getMonth() + 1)}`
  return `${row.stationKey}|${d.getFullYear()}`
}

export function buildHistoryPoints(
  rows: CurahHujanMonitorItem[],
  interval: HistoryInterval,
  stationId?: string,
): HistoryPoint[] {
  let normalized = normalizeRows(rows)
  if (stationId) normalized = normalized.filter(row => row.stationKey === stationId)

  if (interval === '5m') {
    const points: HistoryPoint[] = normalized.map(row => ({
      ts: row.ts,
      label: labelFor(row.date, interval),
      curahHujan: row.curahHujan,
      intensitasHujan: row.intensitasHujan,
      kelembapanTanah: row.kelembapanTanah,
      tmak: row.tmak,
    }))

    const result: HistoryPoint[] = []
    for (let i = 0; i < points.length; i += 1) {
      if (i > 0 && points[i].ts - points[i - 1].ts > GAP_5M_MS) {
        result.push({
          ts: points[i - 1].ts + GAP_5M_MS,
          label: '',
          curahHujan: null,
          intensitasHujan: null,
          kelembapanTanah: null,
          tmak: null,
        })
      }
      result.push(points[i])
    }
    return result
  }

  const groups = new Map<string, {
    ts: number; label: string; count: number;
    curah: number; intensitas: number; kelembapan: number; tmak: number;
  }>()

  for (const row of normalized) {
    const key = bucketKey(row, interval)
    const g = groups.get(key) ?? {
      ts: row.ts,
      label: labelFor(row.date, interval),
      count: 0, curah: 0, intensitas: 0, kelembapan: 0, tmak: 0,
    }
    g.count += 1
    g.curah += row.curahHujan
    g.intensitas += row.intensitasHujan
    g.kelembapan += row.kelembapanTanah
    g.tmak += row.tmak
    groups.set(key, g)
  }

  return Array.from(groups.values()).map(g => ({
    ts: g.ts,
    label: g.label,
    curahHujan: Number(g.curah.toFixed(2)),
    intensitasHujan: Number((g.intensitas / g.count).toFixed(4)),
    kelembapanTanah: Number((g.kelembapan / g.count).toFixed(2)),
    tmak: Number((g.tmak / g.count).toFixed(2)),
  })).sort((a, b) => a.ts - b.ts)
}

export function buildCalendarData(
  rows: CurahHujanMonitorItem[],
  year: number,
  stationId?: string,
): Map<string, number> {
  const result = new Map<string, number>()
  const normalized = normalizeRows(rows).filter(row =>
    row.date.getFullYear() === year && (!stationId || row.stationKey === stationId),
  )

  for (const row of normalized) {
    const key = `${year}-${pad(row.date.getMonth() + 1)}-${pad(row.date.getDate())}`
    result.set(key, Number(((result.get(key) ?? 0) + row.curahHujan).toFixed(2)))
  }
  return result
}

export function getCalendarCellStyle(total: number | undefined) {
  return getRainfallCellStyle(total)
}
