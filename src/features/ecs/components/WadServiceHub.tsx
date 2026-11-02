import { Link } from 'react-router-dom'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { WAD_SERVICE_ITEMS, type WadServiceItem } from '../wadServices'
import styles from './WadServiceHub.module.css'

function ServiceContent({ item }: { item: WadServiceItem }) {
  const isAvailable = item.available && !!item.route

  return (
    <>
      <span className={styles.serviceIcon}>
        <DashboardIcon name={item.icon} />
      </span>
      <span className={styles.serviceCopy}>
        <strong>{item.label}</strong>
        <small>{item.description}</small>
      </span>
      <span className={`${styles.serviceStatus} ${isAvailable ? styles.statusAvailable : styles.statusPlanned}`}>
        {isAvailable ? 'Buka layanan' : 'Segera tersedia'}
      </span>
      <span className={styles.serviceArrow} aria-hidden="true">
        <DashboardIcon name="arrow" />
      </span>
    </>
  )
}

export default function WadServiceHub() {
  return (
    <section className={styles.section} aria-labelledby="wad-service-hub-title">
      <div className={styles.heading}>
        <div>
          <span className={styles.eyebrow}>WATER MANAGEMENT / WAD</span>
          <h2 id="wad-service-hub-title">Pilih layanan WAD</h2>
          <p>Pilih pemantauan atau pemeriksaan yang ingin Anda buka.</p>
        </div>
        <span className={styles.serviceCount}>6 layanan</span>
      </div>

      <div className={styles.flow}>
        <div className={styles.rootNode} aria-label="WAD Water Management">
          <span className={styles.rootIcon}>
            <DashboardIcon name="water" />
          </span>
          <strong>WAD</strong>
          <small>Water Management</small>
        </div>

        <div className={styles.connector} aria-hidden="true" />

        <div className={styles.serviceList}>
          {WAD_SERVICE_ITEMS.map(item => {
            const isAvailable = item.available && !!item.route

            if (!isAvailable) {
              return (
                <div
                  key={item.code}
                  className={`${styles.serviceRow} ${styles.serviceRowDisabled}`}
                  aria-disabled="true"
                >
                  <ServiceContent item={item} />
                </div>
              )
            }

            return (
              <Link
                key={item.code}
                className={styles.serviceRow}
                to={item.route!}
                aria-label={`${item.label}: ${item.description}`}
              >
                <ServiceContent item={item} />
              </Link>
            )
          })}
        </div>
      </div>
    </section>
  )
}
