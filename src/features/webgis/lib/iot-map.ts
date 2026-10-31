import { isMappableDevice } from '../../iot/lib/monitoring.ts'
import type { IoTMonitoringDevice } from '../../iot/types/iot'
import type { Bounds } from './gis/types'

const MINIMUM_EXTENT_DEGREES = 0.01

function roundCoordinate(value: number): number {
  return Number(value.toFixed(12))
}

function paddedExtent(minimum: number, maximum: number, lowerLimit: number, upperLimit: number): [number, number] {
  if (maximum - minimum >= MINIMUM_EXTENT_DEGREES) return [minimum, maximum]

  const center = (minimum + maximum) / 2
  const halfExtent = MINIMUM_EXTENT_DEGREES / 2
  let paddedMinimum = center - halfExtent
  let paddedMaximum = center + halfExtent

  if (paddedMinimum < lowerLimit) {
    const shift = lowerLimit - paddedMinimum
    paddedMinimum += shift
    paddedMaximum += shift
  }
  if (paddedMaximum > upperLimit) {
    const shift = paddedMaximum - upperLimit
    paddedMinimum -= shift
    paddedMaximum -= shift
  }

  return [paddedMinimum, paddedMaximum]
}

export function getIoTMapBounds(devices: readonly IoTMonitoringDevice[]): Bounds | null {
  const mappableDevices = devices.filter(isMappableDevice)
  if (mappableDevices.length === 0) return null

  const longitudes = mappableDevices.map(device => device.longitude as number)
  const latitudes = mappableDevices.map(device => device.latitude as number)
  const [minimumLongitude, maximumLongitude] = paddedExtent(
    Math.min(...longitudes),
    Math.max(...longitudes),
    -180,
    180,
  )
  const [minimumLatitude, maximumLatitude] = paddedExtent(
    Math.min(...latitudes),
    Math.max(...latitudes),
    -90,
    90,
  )

  return [
    roundCoordinate(minimumLongitude),
    roundCoordinate(minimumLatitude),
    roundCoordinate(maximumLongitude),
    roundCoordinate(maximumLatitude),
  ]
}
