import type { SensorStatus } from '../types'
import styles from './StatusBadge.module.css'

const LABEL: Record<SensorStatus, string> = {
  normal:  'Normal',
  warning: 'Waspada',
  danger:  'Kritis',
}

export default function StatusBadge({ status }: { status: SensorStatus }) {
  return (
    <span className={`${styles.badge} ${styles[status]}`}>
      {LABEL[status]}
    </span>
  )
}
