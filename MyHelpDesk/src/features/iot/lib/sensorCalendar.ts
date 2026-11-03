import type { IoTHistoryQuery, IoTHistoryResponse, IoTMonitoringDevice, SensorValue } from '../types/iot'
import { toCalendarRainfallMm } from './rainCalendar.ts'
import { adjustTmakValue } from './tmakOffset.ts'

export type CalendarKind = 'rain' | 'tmak' | 'tmat'

export const CALENDAR_KINDS: Record<CalendarKind, { label: string; unit: string; aliases: string[] }> = {
  rain: {
    label: 'Curah Hujan', unit: 'mm',
    aliases: ['RAIN_TIP', 'RAIN', 'RAINFALL', 'CURAH_HUJAN', 'HUJAN', 'LPA_RAIN_HOUR', 'LPA_RAIN_NOW'],
  },
  tmak: { label: 'TMAK', unit: 'cm', aliases: ['TMAK', 'TMAK_DISTANCE', 'TMAK_CM'] },
  tmat: { label: 'TMAT', unit: 'cm', aliases: ['TMAT', 'TMAT_DISTANCE', 'WATER_TABLE', 'LPA_TMAT'] },
}

function normalizeParameter(key: string) {
  return key.split('#', 1)[0].trim().toUpperCase().replace(/[^A-Z0-9]/g, '')
}

function matchesParameter(key: string, kind: CalendarKind) {
  return CALENDAR_KINDS[kind].aliases.some(alias => normalizeParameter(alias) === normalizeParameter(key))
}

export function getCalendarKind(value: string | null): CalendarKind {
  return value === 'tmak' || value === 'tmat' ? value : 'rain'
}

export function getCalendarSelection(devices: IoTMonitoringDevice[], kind: CalendarKind, loggerId: number) {
  const compatible = devices.filter(device => Object.keys(device.data ?? {}).some(key => matchesParameter(key, kind)))
  return {
    devices: compatible,
    selectedDevice: compatible.find(device => device.loggerId === loggerId) ?? compatible[0] ?? null,
  }
}

export function getCalendarHistoryQuery(kind: CalendarKind, year: number): IoTHistoryQuery {
  return {
    from: `${year}-01-01T00:00`,
    to: `${year + 1}-01-01T00:00`,
    aggregationMode: kind === 'rain' ? 'sum' : 'average',
  }
}

function numericValue(value: SensorValue | undefined) {
  if (value == null || (typeof value === 'string' && value.trim() === '')) return null
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

export function getSensorCalendar(
  history: IoTHistoryResponse | null,
  device: IoTMonitoringDevice | null,
  kind: CalendarKind,
  year: number,
) {
  const calendar = new Map<string, number>()
  if (!history || !device || history.loggerId !== device.loggerId
    || history.aggregationMode !== getCalendarHistoryQuery(kind, year).aggregationMode
    || history.from?.slice(0, 4) !== String(year)) return calendar

  for (const reading of history.data) {
    const match = Object.entries(reading.data).find(([key, value]) => matchesParameter(key, kind) && numericValue(value) !== null)
    if (!match || !reading.ts) continue
    const date = reading.ts.slice(0, 10)
    if (!date.startsWith(`${year}-`) || Number.isNaN(new Date(date).getTime())) continue
    const [key, value] = match
    const displayed = kind === 'tmak'
      ? adjustTmakValue(device.loggerCode, key.split('#', 1)[0], value, device.namaLogger)
      : value
    const numeric = numericValue(displayed)
    if (numeric === null) continue
    calendar.set(date, kind === 'rain' ? toCalendarRainfallMm(device, key, numeric) : numeric)
  }
  return calendar
}

export function getCalendarStatistics(values: Iterable<number>) {
  const readings = [...values]
  return {
    count: readings.length,
    minimum: readings.length ? Math.min(...readings) : null,
    maximum: readings.length ? Math.max(...readings) : null,
    average: readings.length ? readings.reduce((sum, value) => sum + value, 0) / readings.length : null,
  }
}

export function getLevelCellStyle(value: number | undefined, minimum: number | null, maximum: number | null) {
  if (value === undefined) return { background: '#f1f5f4', color: '#80918b' }
  const ratio = minimum !== null && maximum !== null && maximum > minimum ? (value - minimum) / (maximum - minimum) : 0.5
  const colors = ['#e8f3fb', '#c6e2f5', '#91c5e8', '#529fce', '#246a9a']
  const index = Math.min(colors.length - 1, Math.max(0, Math.floor(ratio * colors.length)))
  return { background: colors[index], color: index >= 3 ? '#ffffff' : '#143e58' }
}
