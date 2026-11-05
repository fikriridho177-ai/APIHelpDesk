// TMAK uses the same signed-centimetre convention and visual reference as
// TMAT. The dashboard bands follow the SiTMAT/KLH thresholds supplied by the
// operator: > 0, (-40, 0], (-80, -40], and <= -80 cm.
//
// PP 57/2016 separately defines more than 0.4 m below the peat surface as a
// peatland-damage criterion for groundwater level (TMAT). Keep that reference
// at -40 cm, but do not present the SiTMAT display bands as extra legal limits.
export const KLHK_TMAK_THRESHOLD_CM = -40
export const KLHK_TMAK_VULNERABLE_THRESHOLD_CM = -80

export const TMAK_CATEGORIES = [
  {
    id: 'high',
    label: 'Siaga Banjir',
    range: 'TMAK > 0 cm',
    background: '#2563eb',
    color: '#ffffff',
  },
  {
    id: 'normal',
    label: 'Normal',
    range: '−40 cm < TMAK ≤ 0 cm',
    background: '#10b981',
    color: '#052e23',
  },
  {
    id: 'vulnerable',
    label: 'Waspada',
    range: '−80 cm < TMAK ≤ −40 cm',
    background: '#f59e0b',
    color: '#451a03',
  },
  {
    id: 'very-vulnerable',
    label: 'Kritis',
    range: 'TMAK ≤ −80 cm',
    background: '#e11d48',
    color: '#ffffff',
  },
] as const

export function classifyTmak(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return null
  if (value > 0) return TMAK_CATEGORIES[0]
  if (value > KLHK_TMAK_THRESHOLD_CM) return TMAK_CATEGORIES[1]
  if (value > KLHK_TMAK_VULNERABLE_THRESHOLD_CM) return TMAK_CATEGORIES[2]
  return TMAK_CATEGORIES[3]
}

export function formatTmakCategoryLabel(value: number | null | undefined) {
  return classifyTmak(value)?.label ?? 'Tidak ada data'
}

export function getTmakCellStyle(value: number | null | undefined) {
  const category = classifyTmak(value)
  return category
    ? { background: category.background, color: category.color }
    : { background: '#f1f5f4', color: '#80918b' }
}
