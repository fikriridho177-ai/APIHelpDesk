import type { IoTMonitoringDevice } from '../types/iot'

type RainfallSource = Pick<IoTMonitoringDevice, 'loggerCode' | 'sourceType'>

export function toCalendarRainfallMm(
  device: RainfallSource | null,
  parameterCode: string,
  value: number,
) {
  const isVerifiedSource = device?.sourceType?.trim().toUpperCase() === 'VENDOR'
    && device.loggerCode?.trim().toUpperCase() === 'RSUP-AWS-01'
  const isRainRate = parameterCode.split('#', 1)[0].trim().toUpperCase() === 'RAIN'
  if (!isVerifiedSource || !isRainRate) return value

  // SinauBumi's five-minute RAIN samples are rates in mm/hour. The daily
  // API sum becomes a depth in mm using the same 5/60 factor as the vendor.
  // Use the verified sampling interval, not elapsed gaps or logger settings.
  return Number((value * 5 / 60).toFixed(6))
}

export function summarizeCalendarRainfall(values: Iterable<number>) {
  const readings = [...values]
  return {
    rainyDays: readings.filter(value => value >= 0.5).length,
    total: Number(readings.reduce((sum, value) => sum + value, 0).toFixed(6)),
    maximum: readings.length ? Math.max(...readings) : null,
  }
}
