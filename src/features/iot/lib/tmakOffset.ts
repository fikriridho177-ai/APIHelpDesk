import type { SensorValue } from '../types/iot'

const TARGET_LOGGER_IDENTIFIERS = new Set(['AWL-C001', 'AWLC-PKB-001'])
const TMAK_PARAMETER_CODES = new Set(['TMAK', 'TMAK_DISTANCE'])

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
  const isTmakParameter = TMAK_PARAMETER_CODES.has(parameterCode.trim().toUpperCase())

  if (!isTargetLogger || !isTmakParameter || value === null || value === undefined) return value

  if (typeof value === 'string' && value.trim() === '') return value

  const numericValue = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(numericValue) ? numericValue + 10 : value
}
