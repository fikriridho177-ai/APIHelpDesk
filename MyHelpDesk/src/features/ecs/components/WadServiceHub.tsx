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
        <span className={styles.serviceAction}>{isAvailable ? 'Buka layanan' : 'Modul disiapkan'}</span>
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
            <span className={styles.eyebrow}>WAD / WATER MANAGEMENT</span>
            <h2 id="wad-service-hub-title">Ruang kerja layanan</h2>
            <p>Pilih pemantauan atau pemeriksaan yang ingin Anda buka.</p>
          </div>
        </div>
        <div className={styles.serviceCount} aria-label={`${availableCount} layanan aktif dari 6 layanan`}>
          <strong>{availableCount}</strong>
          <span>aktif</span>
          <i aria-hidden="true" />
          <strong>{WAD_SERVICE_ITEMS.length}</strong>
          <span>layanan</span>
        </div>
      </div>

      <ul className={styles.serviceGrid}>
        {WAD_SERVICE_ITEMS.map((item, index) => {
            const isAvailable = item.available && !!item.route

            if (!isAvailable) {
              return (
                <li
                  key={item.code}
                  className={styles.serviceItem}
                >
                  <div className={`${styles.serviceTile} ${styles.serviceTileDisabled}`} aria-disabled="true">
                    <ServiceContent item={item} index={index} />
                  </div>
                </li>
              )
            }

            return (
              <li
                key={item.code}
                className={styles.serviceItem}
              >
                <Link
                  className={styles.serviceTile}
                  to={item.route!}
                  aria-label={`${item.label}: ${item.description}`}
                >
                  <ServiceContent item={item} index={index} />
                </Link>
              </li>
            )
          })}
      </ul>

      <div className={styles.sectionNote}>
        <span className={styles.noteIcon}>
          <DashboardIcon name="info" />
        </span>
        <span>Data sensor akan tampil setelah memilih layanan pemantauan yang tersedia.</span>
      </div>
    </section>
  )
}
