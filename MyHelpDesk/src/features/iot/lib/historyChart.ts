export type HistoryTooltipValue = number | string | null | undefined

export function getHistoryTooltipValue({
  rawValue,
  sourceValue,
  directional = false,
}: {
  rawValue: unknown
  sourceValue: HistoryTooltipValue
  directional?: boolean
}): HistoryTooltipValue {
  if (directional) return sourceValue
  if (typeof rawValue === 'number' || typeof rawValue === 'string') return rawValue
  if (Array.isArray(rawValue)) {
    const value = rawValue.find(item => typeof item === 'number' || typeof item === 'string')
    return value ?? sourceValue
  }
  return sourceValue
}
