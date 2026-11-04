import type { SensorValue } from '../types/iot'

const TARGET_LOGGER_IDENTIFIERS = new Set(['AWL-C001', 'AWLC-PKB-001'])
const TMAK_PARAMETER_CODES = new Set(['TMAK', 'TMAK_DISTANCE', 'TMAK_CM'])

// UI-only correction for the AWL-C001 display. Raw database/API values remain unchanged.
export const TMAK_CORRECTION_THRESHOLD_CM = -60
export const TMAK_DISPLAY_OFFSET_CM = 10

export function adjustTmakValue(
  loggerCode: string | null | undefined,
  parameterCode: string,
  value: SensorValue | undefined,
  loggerName?: string | null,
): SensorValue | undefined {
  const isTargetLogger = [loggerCode, loggerName].some(identifier => {
    const normalizedIdentifier = identifier?.trim().toUpperCase()
    return normalizedIdentifier !== undefined && TARGET_LOGGER_IDENTIFIERS.has(normalizedIdentifier)
  })
  const normalizedParameterCode = parameterCode.split('#', 1)[0].trim().toUpperCase()
  const isTmakParameter = TMAK_PARAMETER_CODES.has(normalizedParameterCode)

  if (!isTargetLogger || !isTmakParameter || value === null || value === undefined) return value

  if (typeof value === 'string' && value.trim() === '') return value

  const numericValue = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(numericValue) && numericValue < TMAK_CORRECTION_THRESHOLD_CM
    ? numericValue + TMAK_DISPLAY_OFFSET_CM
    : value
}
