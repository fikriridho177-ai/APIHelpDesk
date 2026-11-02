import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import WadServiceHub from '../components/WadServiceHub'
import styles from './OverviewPage.module.css'

export default function OverviewPage() {
  return (
    <div className={styles.page}>
      <section className={styles.hero}>
        <div className={styles.heroContent}>
          <div className={styles.heroMark}>
            <DashboardIcon name="water" />
          </div>

          <div className={styles.eyebrow}>
            PILIH LAYANAN / WAD
          </div>

          <h1 className={styles.heroTitle}>
            Water <span>Management</span>
          </h1>

          <p className={styles.heroText}>
            Pusat layanan pemantauan dan pemeriksaan kondisi air,
            kanal, pintu air, serta area gambut.
          </p>

          <WadServiceHub />
        </div>
      </section>
    </div>
  )
}
