export const RAINFALL_CATEGORIES = [
  {
    id: 'no-data',
    label: 'Tidak ada data',
    range: 'NULL / tidak tersedia',
    legend: 'Tidak ada data',
    background: '#f1f5f4',
    color: '#80918b',
  },
  {
    id: 'below-threshold',
    label: 'Di bawah 0,5 mm',
    range: '0 ≤ x < 0,5 mm',
    legend: '< 0,5 mm',
    background: '#ffffff',
    color: '#315448',
  },
  {
    id: 'light',
    label: 'Hujan ringan',
    range: '0,5 ≤ x ≤ 20 mm',
    legend: '0,5–20 mm',
    background: '#10b981',
    color: '#052e23',
  },
  {
    id: 'moderate',
    label: 'Hujan sedang',
    range: '20 < x ≤ 50 mm',
    legend: '>20–50 mm',
    background: '#f59e0b',
    color: '#451a03',
  },
  {
    id: 'heavy',
    label: 'Hujan lebat',
    range: '50 < x ≤ 100 mm',
    legend: '>50–100 mm',
    background: '#f97316',
    color: '#431407',
  },
  {
    id: 'very-heavy',
    label: 'Hujan sangat lebat',
    range: '100 < x ≤ 150 mm',
    legend: '>100–150 mm',
    background: '#e11d48',
    color: '#ffffff',
  },
  {
    id: 'extreme',
    label: 'Hujan ekstrem',
    range: 'x > 150 mm',
    legend: '>150 mm',
    background: '#7c3aed',
    color: '#ffffff',
  },
] as const

export function classifyRainfall(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value) || value < 0) return RAINFALL_CATEGORIES[0]
  if (value < 0.5) return RAINFALL_CATEGORIES[1]
  if (value <= 20) return RAINFALL_CATEGORIES[2]
  if (value <= 50) return RAINFALL_CATEGORIES[3]
  if (value <= 100) return RAINFALL_CATEGORIES[4]
  if (value <= 150) return RAINFALL_CATEGORIES[5]
  return RAINFALL_CATEGORIES[6]
}

export function getRainfallCellStyle(value: number | null | undefined) {
  const category = classifyRainfall(value)
  return { background: category.background, color: category.color }
}
