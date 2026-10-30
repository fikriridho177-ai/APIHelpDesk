import type { SensorValue } from '../types/iot'

const TARGET_LOGGER_CODE = 'AWL-C001'
const TMAK_PARAMETER_CODES = new Set(['TMAK', 'TMAK_DISTANCE'])

export function adjustTmakValue(
  loggerCode: string | null | undefined,
  parameterCode: string,
  value: SensorValue | undefined,
): SensorValue | undefined {
  const isTargetLogger = loggerCode?.trim().toUpperCase() === TARGET_LOGGER_CODE
  const isTmakParameter = TMAK_PARAMETER_CODES.has(parameterCode.trim().toUpperCase())

  if (!isTargetLogger || !isTmakParameter || value === null || value === undefined) return value

  if (typeof value === 'string' && value.trim() === '') return value

  const numericValue = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(numericValue) ? numericValue + 10 : value
}
