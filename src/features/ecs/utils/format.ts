export function formatDate(str: string | null | undefined): string {
  if (!str) return '-'
  const d = new Date(str)
  if (isNaN(d.getTime())) return str
  return d.toLocaleDateString('id-ID', { day: '2-digit', month: 'short', year: 'numeric' })
}

export function formatDateShort(str: string | null | undefined): string {
  if (!str) return '-'
  const d = new Date(str)
  if (isNaN(d.getTime())) return str
  return d.toLocaleDateString('id-ID', { day: '2-digit', month: 'short' })
}

export function todayStr(): string { return new Date().toISOString().slice(0, 10) }
export function daysAgoStr(n: number): string { return new Date(Date.now() - n * 86_400_000).toISOString().slice(0, 10) }

export function groupByDateAvg(data: { tanggal: string; value: number }[]): { label: string; value: number }[] {
  const map = new Map<string, number[]>()
  for (const d of data) {
    const label = formatDateShort(d.tanggal)
    if (!map.has(label)) map.set(label, [])
    map.get(label)!.push(d.value)
  }
  return Array.from(map.entries()).map(([label, vals]) => ({
    label,
    value: parseFloat((vals.reduce((s, v) => s + v, 0) / vals.length).toFixed(2)),
  }))
}
