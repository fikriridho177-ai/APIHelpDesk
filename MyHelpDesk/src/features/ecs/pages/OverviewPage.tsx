import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import WadServiceHub from '../components/WadServiceHub'
import { WAD_SERVICE_ITEMS } from '../wadServices'
import styles from './OverviewPage.module.css'

export default function OverviewPage() {
  const sensorFocusCount = WAD_SERVICE_ITEMS.filter(item => ['WAD_TMAT', 'WAD_TMAK', 'WAD_CURAH_HUJAN'].includes(item.code)).length

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
            <span><strong>{String(sensorFocusCount).padStart(2, '0')}</strong> fokus sensor</span>
            <span><strong>{String(WAD_SERVICE_ITEMS.length).padStart(2, '0')}</strong> modul WAD</span>
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
