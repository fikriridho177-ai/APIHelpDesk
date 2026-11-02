import { Link } from 'react-router-dom'

import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { IOT_NAVIGATION_ITEMS } from '../../iot/iotNavigation'
import styles from './EnvironmentalMonitoringMenu.module.css'

const IOT_PERMISSION_CODE = 'DASHBOARD_IOT'

export default function EnvironmentalMonitoringMenu() {
  const { permission, loaded } = useMenuPermission(IOT_PERMISSION_CODE)

  // The ECS landing page remains useful for users who only have WAD access.
  // IoT shortcuts appear only after their existing permission is known.
  if (!loaded || !permission?.canRead) return null

  return (
    <section className={styles.section} aria-labelledby="environmental-monitoring-menu-title">
      <div className={styles.heading}>
        <div>
          <span className={styles.eyebrow}>IOT MONITORING</span>
          <h2 id="environmental-monitoring-menu-title">Pusat monitoring sensor</h2>
          <p>Akses semua pekerjaan IoT dari satu halaman Environmental Monitoring.</p>
        </div>
        <span className={styles.permissionBadge}>AKSES AKTIF</span>
      </div>

      <div className={styles.grid}>
        {IOT_NAVIGATION_ITEMS.map(item => (
          <Link key={item.code} className={styles.card} to={item.route}>
            <span className={styles.marker}>{item.marker}</span>
            <span className={styles.copy}>
              <strong>{item.label}</strong>
              <small>{item.description}</small>
            </span>
            <span className={styles.arrow} aria-hidden="true">↗</span>
          </Link>
        ))}
      </div>
    </section>
  )
}
