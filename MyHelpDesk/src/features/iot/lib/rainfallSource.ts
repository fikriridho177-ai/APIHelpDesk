import type { IoTMonitoringDevice, IoTMonitoringResponse } from '../types/iot'

function normalize(value: string | null | undefined) {
  return value?.trim().toUpperCase().replace(/[^A-Z0-9]/g, '') ?? ''
}

const AWL_IDENTIFIERS = new Set(['AWLC001', 'AWLCPKB001'])
const RAIN_PARAMETER_ALIASES = new Set(['RAIN', 'RAIN_TIP', 'RAINFALL', 'CURAH_HUJAN', 'CURAHHUJAN'].map(normalize))

function isAwlLogger(device: IoTMonitoringDevice) {
  return [device.loggerCode, device.namaLogger].some(identifier => AWL_IDENTIFIERS.has(normalize(identifier)))
}

function isAwsLogger(device: IoTMonitoringDevice) {
  if (device.sourceType !== 'VENDOR') return false

  return [device.loggerCode, device.namaLogger].some(identifier => {
    const normalized = normalize(identifier)
    return normalized === 'AWS' || normalized.includes('AWS')
  })
}

function isRainParameter(key: string) {
  const normalized = normalize(key.split('#', 1)[0])
  return RAIN_PARAMETER_ALIASES.has(normalized)
}

function findRainReading(device: IoTMonitoringDevice) {
  return Object.entries(device.data ?? {}).find(([key, value]) => isRainParameter(key) && value !== null && value !== undefined)
}

/**
 * Uses the AWS rainfall snapshot for AWL-C001's rainfall display only.
 * The original AWL payload is copied before changing the rainfall key so
 * unrelated readings such as TMAK remain untouched.
 */
export function copyAwsRainfallToAwl(response: IoTMonitoringResponse): IoTMonitoringResponse {
  const awsRainReading = response.devices
    .filter(isAwsLogger)
    .map(findRainReading)
    .find(reading => reading !== undefined)

  if (!awsRainReading) return response

  const awlDevice = response.devices.find(isAwlLogger)
  if (!awlDevice) return response

  const targetRainKey = Object.keys(awlDevice.data ?? {}).find(isRainParameter) ?? 'RAIN_TIP'

  return {
    ...response,
    devices: response.devices.map(device => {
      if (!isAwlLogger(device)) return device

      const mappedDevice = {
        ...device,
        data: {
          ...device.data,
          [targetRainKey]: awsRainReading[1],
        },
      }

      if (device.errors) {
        const remainingErrors = { ...device.errors }
        delete remainingErrors[targetRainKey]

        if (Object.keys(remainingErrors).length > 0) {
          mappedDevice.errors = remainingErrors
        } else {
          delete mappedDevice.errors
        }
      }

      return mappedDevice
    }),
  }
}
