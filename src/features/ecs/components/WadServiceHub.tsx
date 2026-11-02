import { Link } from 'react-router-dom'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { WAD_SERVICE_ITEMS, WAD_TOTAL_MODULE_COUNT, type WadServiceItem } from '../wadServices'
import styles from './WadServiceHub.module.css'

function ServiceContent({ item }: { item: WadServiceItem }) {
  const isAvailable = item.available && !!item.route

  return (
    <>
      <span className={styles.serviceHeader}>
        <span className={styles.serviceIndex}>{String(item.order).padStart(2, '0')}</span>
        <span className={`${styles.serviceStatus} ${isAvailable ? styles.statusAvailable : styles.statusPlanned}`}>
          <span className={styles.statusDot} aria-hidden="true" />
          {isAvailable ? 'Aktif' : 'Segera tersedia'}
        </span>
      </span>
      <span className={styles.serviceIcon}>
        <DashboardIcon name={item.icon} />
      </span>
      <span className={styles.serviceCopy}>
        <strong>{item.label}</strong>
        <small>{item.description}</small>
      </span>
      <span className={styles.serviceFooter}>
        <span className={styles.serviceAction}>{isAvailable ? 'Monitoring aktif' : 'Modul disiapkan'}</span>
        <span className={styles.serviceArrow} aria-hidden="true">
          <DashboardIcon name="arrow" />
        </span>
      </span>
    </>
  )
}

export default function WadServiceHub() {
  const availableCount = WAD_SERVICE_ITEMS.filter(item => item.available && item.route).length

  return (
    <section className={styles.section} aria-labelledby="wad-service-hub-title">
      <div className={styles.heading}>
        <div className={styles.headingTitle}>
          <span className={styles.sectionIcon}>
            <DashboardIcon name="water" />
          </span>
          <div>
            <span className={styles.eyebrow}>MENU WAD</span>
            <h2 id="wad-service-hub-title">Layanan pemantauan</h2>
            <p>Fokus parameter sensor tersedia sebagai filter. Kartu berikut membuka workspace operasional WAD.</p>
          </div>
        </div>
        <div className={styles.headingAside}>
          <Link className={styles.allSensorsLink} to="/ecs/stasiun-monitoring?filter=all">
            Semua sensor <DashboardIcon name="arrow" />
          </Link>
          <div className={styles.serviceCount} aria-label={`${availableCount} layanan aktif dari ${WAD_TOTAL_MODULE_COUNT} modul`}>
            <strong>{availableCount}</strong>
            <span>layanan aktif</span>
            <span className={styles.countDivider} aria-hidden="true">/</span>
            <strong>{WAD_TOTAL_MODULE_COUNT}</strong>
            <span>modul</span>
          </div>
        </div>
      </div>

      <ul className={styles.serviceGrid}>
        {WAD_SERVICE_ITEMS.map(item => {
          const isAvailable = item.available && !!item.route

          return (
            <li key={item.code} className={styles.serviceItem}>
              {isAvailable ? (
                <Link
                  className={styles.serviceTile}
                  to={item.route!}
                  aria-label={`${item.label}: ${item.description}`}
                >
                  <ServiceContent item={item} />
                </Link>
              ) : (
                <div className={`${styles.serviceTile} ${styles.serviceTileDisabled}`} aria-disabled="true">
                  <ServiceContent item={item} />
                </div>
              )}
            </li>
          )
        })}
      </ul>

      <div className={styles.sectionNote}>
        <span className={styles.noteIcon}>
          <DashboardIcon name="info" />
        </span>
        <span>TMAT, TMAK, dan curah hujan dipilih melalui filter Monitoring Sensor; map, perangkat, prediksi, dan kalender memiliki halaman kerja sendiri.</span>
      </div>
    </section>
  )
}
