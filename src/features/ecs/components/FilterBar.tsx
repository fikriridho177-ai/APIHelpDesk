import type { DateRange } from '../types'
import styles from './FilterBar.module.css'

interface FilterBarProps {
  range: DateRange
  onChange: (range: DateRange) => void
  onApply: () => void
  loading?: boolean
}

export default function FilterBar({ range, onChange, onApply, loading }: FilterBarProps) {
  return (
    <div className={styles.bar}>
      <div className={styles.field}>
        <label htmlFor="date-from">Dari Tanggal</label>
        <input
          id="date-from"
          type="date"
          value={range.from}
          max={range.to}
          onChange={e => onChange({ ...range, from: e.target.value })}
        />
      </div>
      <div className={styles.field}>
        <label htmlFor="date-to">Sampai Tanggal</label>
        <input
          id="date-to"
          type="date"
          value={range.to}
          min={range.from}
          onChange={e => onChange({ ...range, to: e.target.value })}
        />
      </div>
      <button className={styles.btn} onClick={onApply} disabled={loading}>
        {loading ? <span className={styles.spinner} /> : '🔄'}
        {loading ? 'Memuat...' : 'Perbarui Data'}
      </button>
    </div>
  )
}
