// SiTMAT, Kementerian Lingkungan Hidup: https://sitmat.kemenlh.go.id/
// Apply to signed TMAT in cm relative to the ground surface, without rounding.
export const TMAT_CATEGORIES = [
  { id: 'flooded', label: 'Tergenang', range: 'TMAT > 0 cm', background: '#2563eb', color: '#ffffff' },
  { id: 'normal', label: 'Normal', range: '−40 cm < TMAT ≤ 0 cm', background: '#10b981', color: '#052e23' },
  { id: 'vulnerable', label: 'Rawan', range: '−80 cm < TMAT ≤ −40 cm', background: '#f59e0b', color: '#451a03' },
  { id: 'very-vulnerable', label: 'Sangat Rawan', range: 'TMAT ≤ −80 cm', background: '#e11d48', color: '#ffffff' },
] as const

export function classifyTmat(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return null
  if (value > 0) return TMAT_CATEGORIES[0]
  if (value > -40) return TMAT_CATEGORIES[1]
  if (value > -80) return TMAT_CATEGORIES[2]
  return TMAT_CATEGORIES[3]
}

export function getTmatCellStyle(value: number | null | undefined) {
  const category = classifyTmat(value)
  return category
    ? { background: category.background, color: category.color }
    : { background: '#f1f5f4', color: '#80918b' }
}
