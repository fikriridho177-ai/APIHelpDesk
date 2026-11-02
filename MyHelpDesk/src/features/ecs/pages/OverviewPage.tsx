import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import WadServiceHub from '../components/WadServiceHub'
import styles from './OverviewPage.module.css'

export default function OverviewPage() {
  return (
    <div className={styles.page}>
      <section className={styles.hero}>
        <div className={styles.heroCopy}>
          <span className={styles.eyebrow}>WAD / WATER MANAGEMENT</span>
          <h1>Monitoring air <span>berbasis IoT</span></h1>
          <p>
            Pilih layanan untuk memantau tinggi muka air, curah hujan, dan kondisi
            pengelolaan air dari data logger terbaru.
          </p>
          <div className={styles.heroMeta} aria-label="Ringkasan WAD">
            <span><i className={styles.liveDot} aria-hidden="true" /> Data monitoring terbaru</span>
            <span><strong>03</strong> layanan aktif</span>
            <span><strong>06</strong> layanan WAD</span>
          </div>
        </div>

        <aside className={styles.heroStatus} aria-label="Status monitoring IoT">
          <div className={styles.statusIcon}><DashboardIcon name="water" /></div>
          <div>
            <span>WAD / LIVE SENSOR</span>
            <strong>Operation center</strong>
            <p>Gunakan satu pintu layanan untuk membaca data sensor.</p>
          </div>
        </aside>
      </section>

      <WadServiceHub />
    </div>
  )
}
