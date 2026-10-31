import type { MapLayer } from './types'

const OWNER_PHRASE = 'riau sakti united'
const PLANTATION_TERMS = ['plantation', 'perkebunan']
const DIVISION_TERMS = ['divisi', 'division']

function normalize(value: string | null | undefined) {
  return (value ?? '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
}

function hasTerm(value: string, terms: readonly string[]) {
  return terms.some(term => value.includes(term))
}

function layerMatchScore(layer: MapLayer) {
  const fields = [
    { value: normalize(layer.name), weight: 3 },
    { value: normalize(layer.groupName), weight: 2 },
    { value: normalize(layer.description), weight: 1 },
  ]
  const combined = fields.map(field => field.value).filter(Boolean).join(' ')
  const hasOwner = combined.includes(OWNER_PHRASE) || /(^| )rsup( |$)/.test(combined)
  const hasPlantation = hasTerm(combined, PLANTATION_TERMS)
  const hasDivision = hasTerm(combined, DIVISION_TERMS)
  if (!hasOwner || (!hasPlantation && !hasDivision)) return 0

  let score = 100
  for (const field of fields) {
    if (field.value.includes(OWNER_PHRASE) || /(^| )rsup( |$)/.test(field.value)) score += field.weight * 8
    if (hasTerm(field.value, PLANTATION_TERMS)) score += field.weight * 4
    if (hasTerm(field.value, DIVISION_TERMS)) score += field.weight * 3
  }
  return score
}

export function findPreferredGisLayer(layers: readonly MapLayer[]): MapLayer | null {
  let preferred: MapLayer | null = null
  let preferredScore = 0
  for (const layer of layers) {
    const score = layerMatchScore(layer)
    if (score > preferredScore) {
      preferred = layer
      preferredScore = score
    }
  }
  return preferred
}
