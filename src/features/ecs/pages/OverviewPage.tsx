import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import WadServiceHub from '../components/WadServiceHub'
import styles from './OverviewPage.module.css'

export default function OverviewPage() {
  return (
    <div className={styles.page}>
      <section className={styles.hero}>
        <div className={styles.heroContent}>
          <div className={styles.heroTop}>
            <div className={styles.heroIntro}>
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
            </div>

            <aside className={styles.heroPanel} aria-label="Status layanan WAD">
              <div className={styles.panelLabel}>WAD / OPERATION CENTER</div>
              <div className={styles.panelHeading}>
                <span className={styles.panelPulse} aria-hidden="true" />
                Layanan siap digunakan
              </div>
              <p className={styles.panelText}>Akses pemantauan air dari satu ruang kerja.</p>
              <div className={styles.panelMeta}>
                <span><strong>03</strong> aktif</span>
                <span><strong>06</strong> layanan</span>
              </div>
            </aside>
          </div>

          <WadServiceHub />
        </div>
      </section>
    </div>
  )
}
