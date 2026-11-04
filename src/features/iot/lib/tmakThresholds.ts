// Operational KLHK reference for peatland water-level management: more than
// 0.4 m below the peat surface is beyond the reference limit (PP 57/2016).
// TMAK values in this application are signed centimetres relative to the
// surface, so the boundary is -40 cm and the exact boundary remains in range.
export const KLHK_TMAK_THRESHOLD_CM = -40

export const TMAK_CATEGORIES = [
  {
    id: 'within',
    label: 'Dalam ambang KLHK',
    range: 'TMAK ≥ −40 cm',
    background: '#10b981',
    color: '#052e23',
  },
  {
    id: 'below',
    label: 'Melewati ambang KLHK',
    range: 'TMAK < −40 cm',
    background: '#e11d48',
    color: '#ffffff',
  },
] as const

export function classifyTmak(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return null
  return value >= KLHK_TMAK_THRESHOLD_CM ? TMAK_CATEGORIES[0] : TMAK_CATEGORIES[1]
}

export function getTmakCellStyle(value: number | null | undefined) {
  const category = classifyTmak(value)
  return category
    ? { background: category.background, color: category.color }
    : { background: '#f1f5f4', color: '#80918b' }
}
