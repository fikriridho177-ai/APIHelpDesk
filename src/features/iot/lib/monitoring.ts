import type { IoTMonitoringDevice, IoTMonitoringResponse, IoTParameter } from '../types/iot'

export interface IoTMonitoringSummary {
  total: number
  online: number
  offline: number
  issues: number
  mapped: number
}

export interface MonitoringReading {
  key: string
  code: string
  value: number | null
}

const priorityParameterCodes = ['TMAK', 'TMAT']

export function hasValidCoordinates(
  latitude: number | null | undefined,
  longitude: number | null | undefined,
): boolean {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return false
  const lat = latitude as number
  const lon = longitude as number
  return lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180
}

export function isMappableDevice(device: IoTMonitoringDevice) {
  return hasValidCoordinates(device.latitude, device.longitude)
}

export function getMonitoringSummary(response: IoTMonitoringResponse): IoTMonitoringSummary {
  const devices = response.devices ?? []
  const online = devices.filter(device => device.status === 'ONLINE').length
  const issues = devices.filter(device => ['NO_RECENT_DATA', 'INACTIVE', 'UNKNOWN'].includes(device.status)).length

  return {
    total: response.total ?? devices.length,
    online,
    offline: Math.max(0, devices.length - online),
    issues,
    mapped: devices.filter(isMappableDevice).length,
  }
}

export function monitoringStatusLabel(status: string | null | undefined) {
  switch (status) {
    case 'ONLINE': return 'Online'
    case 'STALE': return 'Stale'
    case 'NO_RECENT_DATA': return 'Tidak ada data'
    case 'INACTIVE': return 'Nonaktif'
    default: return 'Tidak diketahui'
  }
}

export function monitoringStatusTone(status: string | null | undefined) {
  switch (status) {
    case 'ONLINE': return 'online'
    case 'STALE': return 'stale'
    case 'NO_RECENT_DATA': return 'noRecentData'
    case 'INACTIVE': return 'inactive'
    default: return 'unknown'
  }
}

export function formatMonitoringDate(value: string | null | undefined, withTime = true) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('id-ID', {
    dateStyle: 'medium',
    ...(withTime ? { timeStyle: 'short' as const } : {}),
  }).format(date)
}

export function formatMonitoringNumber(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return '-'
  return new Intl.NumberFormat('id-ID', { maximumFractionDigits: 3 }).format(value)
}

export function getMonitoringParameterCode(key: string) {
  return key.split('#', 1)[0]
}

export function getOrderedMonitoringReadings(data: Record<string, number | null>): MonitoringReading[] {
  return Object.entries(data)
    .map(([key, value], index) => ({
      key,
      code: getMonitoringParameterCode(key).trim(),
      value,
      index,
    }))
    .sort((left, right) => {
      const leftPriority = priorityParameterCodes.indexOf(left.code.toUpperCase())
      const rightPriority = priorityParameterCodes.indexOf(right.code.toUpperCase())
      const leftRank = leftPriority === -1 ? priorityParameterCodes.length : leftPriority
      const rightRank = rightPriority === -1 ? priorityParameterCodes.length : rightPriority
      return leftRank - rightRank || left.index - right.index
    })
    .map(({ index: _index, ...reading }) => reading)
}

export function getMonitoringParameterName(parameter: IoTParameter | undefined, code: string) {
  return parameter?.namaParameter?.trim() || parameter?.parameterCode?.trim() || code
}

export function getMonitoringParameterUnit(parameter: IoTParameter | undefined) {
  return parameter?.satuan?.trim() || 'nilai'
}
