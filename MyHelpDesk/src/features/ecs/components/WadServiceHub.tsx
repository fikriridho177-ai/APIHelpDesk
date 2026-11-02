import { Link } from 'react-router-dom'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { WAD_SERVICE_ITEMS, type WadServiceItem } from '../wadServices'
import styles from './WadServiceHub.module.css'

function ServiceContent({ item, index }: { item: WadServiceItem; index: number }) {
  const isAvailable = item.available && !!item.route

  return (
    <>
      <span className={styles.serviceHeader}>
        <span className={styles.serviceIndex}>{String(index + 1).padStart(2, '0')}</span>
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
            <p>Pilih data sensor atau pemeriksaan lapangan yang ingin Anda buka.</p>
          </div>
        </div>
        <div className={styles.serviceCount} aria-label={`${availableCount} layanan aktif dari ${WAD_SERVICE_ITEMS.length} layanan`}>
          <strong>{availableCount}</strong>
          <span>aktif</span>
          <span className={styles.countDivider} aria-hidden="true">/</span>
          <strong>{WAD_SERVICE_ITEMS.length}</strong>
          <span>total</span>
        </div>
      </div>

      <ul className={styles.serviceGrid}>
        {WAD_SERVICE_ITEMS.map((item, index) => {
          const isAvailable = item.available && !!item.route

          return (
            <li key={item.code} className={styles.serviceItem}>
              {isAvailable ? (
                <Link
                  className={styles.serviceTile}
                  to={item.route!}
                  aria-label={`${item.label}: ${item.description}`}
                >
                  <ServiceContent item={item} index={index} />
                </Link>
              ) : (
                <div className={`${styles.serviceTile} ${styles.serviceTileDisabled}`} aria-disabled="true">
                  <ServiceContent item={item} index={index} />
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
        <span>Data sensor terbaru diambil dari endpoint monitoring IoT ringkas.</span>
      </div>
    </section>
  )
}
