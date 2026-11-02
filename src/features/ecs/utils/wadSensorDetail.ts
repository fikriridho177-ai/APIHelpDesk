export interface LiveParameter {
  code: string
  label: string
  unit: string
  value: number | null
  categoryName: string
  categoryOrder: number
}

export interface LiveParameterGroup {
  key: string
  name: string
  order: number
  parameters: LiveParameter[]
}

export function groupLiveParameters(parameters: LiveParameter[]): LiveParameterGroup[] {
  const groups = new Map<string, LiveParameterGroup>()

  parameters.forEach(parameter => {
    const name = parameter.categoryName.trim() || 'Data lainnya'
    const key = name.toLocaleUpperCase('id-ID')
    const existing = groups.get(key)

    if (existing) {
      existing.parameters.push(parameter)
      existing.order = Math.min(existing.order, parameter.categoryOrder)
      return
    }

    groups.set(key, {
      key,
      name,
      order: parameter.categoryOrder,
      parameters: [parameter],
    })
  })

  return [...groups.values()].sort((left, right) =>
    left.order - right.order || left.name.localeCompare(right.name, 'id-ID'),
  )
}
