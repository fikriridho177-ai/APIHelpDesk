import type { ReactNode } from 'react'
import styles from './ChartCard.module.css'

interface ChartCardProps {
  title: string
  dot?: 'blue' | 'purple' | 'orange' | 'green'
  unit?: string
  children: ReactNode
  loading?: boolean
  footer?: ReactNode
}

const DOT_MAP = {
  blue:   styles.dotBlue,
  purple: styles.dotPurple,
  orange: styles.dotOrange,
  green:  styles.dotGreen,
}

export default function ChartCard({
  title,
  dot,
  unit,
  children,
  loading,
  footer,
}: ChartCardProps) {
  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <div className={styles.titleWrap}>
          {dot && <span className={`${styles.dot} ${DOT_MAP[dot]}`} />}
          <span className={styles.title}>{title}</span>
        </div>
        {unit && <span className={styles.unit}>{unit}</span>}
      </div>

      <div className={styles.body}>
        {loading ? (
          <div className={styles.loadingWrap}>
            <span className={styles.spinner} />
          </div>
        ) : (
          children
        )}
      </div>

      {footer && <div className={styles.footer}>{footer}</div>}
    </div>
  )
}
