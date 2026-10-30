import type { IoTMonitoringDevice, IoTMonitoringResponse } from '../types/iot'

export interface IoTMonitoringSummary {
  total: number
  online: number
  offline: number
  issues: number
  mapped: number
}

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
