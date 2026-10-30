import styles from './StatCard.module.css'

interface StatCardProps {
  icon: string
  value: string | number
  label: string
  sub?: string
  accent?: 'green' | 'blue' | 'purple' | 'orange'
  loading?: boolean
}

const ACCENT_MAP = {
  green:  styles.accentGreen,
  blue:   styles.accentBlue,
  purple: styles.accentPurple,
  orange: styles.accentOrange,
}

export default function StatCard({
  icon,
  value,
  label,
  sub,
  accent = 'green',
  loading,
}: StatCardProps) {
  return (
    <div className={styles.card}>
      <div className={`${styles.iconWrap} ${ACCENT_MAP[accent]}`}>
        <span className={styles.icon}>{icon}</span>
      </div>
      <div className={styles.body}>
        {loading ? (
          <div className={styles.skeleton} />
        ) : (
          <div className={styles.value}>{value}</div>
        )}
        <div className={styles.label}>{label}</div>
        {sub && <div className={styles.sub}>{sub}</div>}
      </div>
    </div>
  )
}
