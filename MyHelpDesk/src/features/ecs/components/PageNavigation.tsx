import { Link } from 'react-router-dom'
import styles from './PageNavigation.module.css'

interface PageNavigationAction {
  to: string
  label: string
  icon?: string
}

interface PageNavigationProps {
  backTo: string
  backLabel: string
  actions?: PageNavigationAction[]
}

export default function PageNavigation({
  backTo,
  backLabel,
  actions = [],
}: PageNavigationProps) {
  return (
    <div className={styles.navBar}>
      <Link
        to={backTo}
        className={`${styles.button} ${styles.backButton}`}
      >
        <span className={styles.icon}>←</span>
        <span>{backLabel}</span>
      </Link>

      {actions.length > 0 && (
        <div className={styles.actions}>
          {actions.map(action => (
            <Link
              key={action.to}
              to={action.to}
              className={`${styles.button} ${styles.actionButton}`}
            >
              {action.icon && (
                <span className={styles.icon}>{action.icon}</span>
              )}

              <span>{action.label}</span>
              <span className={styles.arrow}>→</span>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}
