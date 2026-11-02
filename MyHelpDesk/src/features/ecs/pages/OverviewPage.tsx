import { useNavigate } from 'react-router-dom'

import EnvironmentalMonitoringMenu from '../components/EnvironmentalMonitoringMenu'
import styles from './OverviewPage.module.css'

export default function OverviewPage() {
  const navigate = useNavigate()

  return (
    <div className={styles.page}>
      <section className={styles.hero}>
        <div className={styles.heroContent}>
          <div className={styles.heroMark}>🌿</div>

          <div className={styles.eyebrow}>
            RSUP PERKEBUNAN
          </div>

          <h1 className={styles.heroTitle}>
            Environmental <span>Monitoring</span>
          </h1>

          <p className={styles.heroText}>
            Sistem pemantauan lingkungan berbasis sensor IoT untuk Curah Hujan,
            Tinggi Muka Air Kanal, dan Kelembaban Tanah.
          </p>

          <div className={styles.heroActions}>
            <button
              type="button"
              className={styles.primaryButton}
              onClick={() => navigate('/ecs/stasiun-monitoring')}
            >
              Lihat Stasiun
            </button>
          </div>

          <EnvironmentalMonitoringMenu />
        </div>
      </section>
    </div>
  )
}
