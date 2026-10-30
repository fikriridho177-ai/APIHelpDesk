import { useCallback, useEffect, useMemo, useState } from 'react'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTDevices } from '../api/iotApi'
import type { IoTDevice, IoTSourceType } from '../types/iot'
import styles from './IoTVendorDevicesPage.module.css'

const MENU_CODE = 'DASHBOARD_IOT'

type Props = {
  menuCode?: string
  sourceType?: IoTSourceType
  title?: string
  subtitle?: string
  eyebrow?: string
}

function formatInterval(value: number | null | undefined) {
  return value ? `${value} menit` : 'Tidak diatur'
}

function sourceLabel(sourceType?: IoTSourceType | '') {
  if (sourceType === 'INTERNAL') return 'logger lokal'
  if (sourceType === 'VENDOR') return 'perangkat vendor'
  return 'perangkat IoT'
}

function sourceTitle(sourceType?: IoTSourceType | '') {
  if (sourceType === 'INTERNAL') return 'Internal'
  if (sourceType === 'VENDOR') return 'Vendor'
  return 'Semua sumber'
}

export default function IoTVendorDevicesPage({
  menuCode = MENU_CODE,
  sourceType,
  title = 'Daftar Perangkat IoT',
  subtitle = 'Satu daftar untuk perangkat Vendor dan Internal dengan filter sumber.',
  eyebrow = 'IOT ENGINEERING / DEVICE REGISTRY',
}: Props = {}) {
  const { permission, loaded } = useMenuPermission(menuCode)
  const canRead = !!permission?.canRead
  const [devices, setDevices] = useState<IoTDevice[]>([])
  const [search, setSearch] = useState('')
  const [sourceFilter, setSourceFilter] = useState<IoTSourceType | ''>(sourceType ?? '')
  const [intervalFilter, setIntervalFilter] = useState('all')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  const loadDevices = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const response = await getIoTDevices(sourceType)
      setDevices(response.data)
    } catch (loadError) {
      console.error(`Gagal memuat ${sourceLabel(sourceType)}:`, loadError)
      setDevices([])
      setError(`Daftar ${sourceLabel(sourceType)} gagal dimuat. Silakan coba lagi.`)
    } finally {
      setLoading(false)
    }
  }, [sourceType])

  useEffect(() => {
    if (loaded && canRead) void loadDevices()
  }, [loaded, canRead, loadDevices, refreshKey])

  const filteredDevices = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('id-ID')
    const selectedSource = sourceType ?? sourceFilter

    return devices.filter(device => {
      const matchesSearch = !query || [device.namaLogger, device.loggerCode, device.lokasi, device.controller]
        .filter(Boolean)
        .some(value => value!.toLocaleLowerCase('id-ID').includes(query))
      const matchesInterval = intervalFilter === 'all' || String(device.intervalMenit ?? '') === intervalFilter
      const matchesSource = !selectedSource || device.sourceType === selectedSource

      return matchesSearch && matchesInterval && matchesSource
    })
  }, [devices, intervalFilter, search, sourceFilter, sourceType])

  const intervalOptions = useMemo(() => {
    return [...new Set(devices.map(device => device.intervalMenit).filter((value): value is number => value != null))]
      .sort((left, right) => left - right)
  }, [devices])

  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  const activeCount = filteredDevices.filter(device => device.aktif).length
  const visibleSource = sourceType ?? sourceFilter

  return (
    <main className={styles.page}>
      <section className={styles.hero}>
        <div>
          <p className={styles.eyebrow}><span /> {eyebrow}</p>
          <h1>{title}</h1>
          <p className={styles.subtitle}>{subtitle}</p>
        </div>
        <button
          className={styles.refreshButton}
          type="button"
          onClick={() => setRefreshKey(key => key + 1)}
          disabled={loading}
        >
          {loading ? 'Memuat...' : 'Perbarui data'}
        </button>
      </section>

      <section className={styles.metrics} aria-label={`Ringkasan ${title.toLocaleLowerCase('id-ID')}`}>
        <article className={styles.metricCard}>
          <span className={styles.metricIcon}>▦</span>
          <small>Total perangkat</small>
          <strong>{loading ? '—' : filteredDevices.length}</strong>
          <span>Terdaftar sebagai {sourceTitle(visibleSource)}</span>
        </article>
        <article className={`${styles.metricCard} ${styles.activeMetric}`}>
          <span className={styles.metricIcon}>✓</span>
          <small>Perangkat aktif</small>
          <strong>{loading ? '—' : activeCount}</strong>
          <span>Siap digunakan</span>
        </article>
        <article className={styles.metricCard}>
          <span className={styles.metricIcon}>⌁</span>
          <small>Interval monitoring</small>
          <select
            className={styles.metricSelect}
            value={intervalFilter}
            onChange={event => setIntervalFilter(event.target.value)}
            disabled={loading || intervalOptions.length === 0}
            aria-label="Filter interval monitoring"
          >
            <option value="all">Semua interval</option>
            {intervalOptions.map(interval => (
              <option key={interval} value={interval}>{interval} menit</option>
            ))}
          </select>
          <span>{filteredDevices.length} perangkat sesuai filter</span>
        </article>
      </section>

      <section className={styles.panel}>
        <div className={styles.panelHeader}>
          <div>
            <p className={styles.panelKicker}>{sourceType === 'INTERNAL' ? 'LOCAL LOGGER REGISTRY' : sourceType === 'VENDOR' ? 'VENDOR REGISTRY' : 'UNIFIED DEVICE REGISTRY'}</p>
            <h2>Perangkat terdaftar</h2>
            <span>{filteredDevices.length} perangkat ditampilkan</span>
          </div>
          <div className={styles.panelFilters}>
            {!sourceType && <label className={`${styles.searchBox} ${styles.sourceBox}`}>
              <span>Sumber perangkat</span>
              <select className={styles.filterSelect} value={sourceFilter} onChange={event => setSourceFilter(event.target.value as IoTSourceType | '')}>
                <option value="">Semua sumber</option>
                <option value="VENDOR">Vendor</option>
                <option value="INTERNAL">Internal</option>
              </select>
            </label>}
            <label className={styles.searchBox}>
              <span>Cari perangkat</span>
              <input
                value={search}
                onChange={event => setSearch(event.target.value)}
                placeholder="Nama, kode, lokasi..."
              />
            </label>
          </div>
        </div>

        {loading ? (
          <div className={styles.state}>Memuat {sourceLabel(sourceType)}...</div>
        ) : error ? (
          <div className={styles.state} role="alert">{error}</div>
        ) : filteredDevices.length === 0 ? (
          <div className={styles.state}>Tidak ada {sourceLabel(visibleSource)} yang ditemukan.</div>
        ) : (
          <div className={styles.deviceGrid}>
            {filteredDevices.map(device => (
              <article className={styles.deviceCard} key={device.loggerId}>
                <div className={styles.deviceTopline}>
                  <span className={styles.deviceCode}>{device.loggerCode}</span>
                  <span className={device.aktif ? styles.online : styles.offline}>
                    <i /> {device.aktif ? 'Aktif' : 'Nonaktif'}
                  </span>
                </div>
                <h3>{device.namaLogger}</h3>
                <div className={`${styles.locationLine} ${!device.lokasi ? styles.locationEmpty : ''}`}>
                  <span className={styles.locationIcon} aria-hidden="true">⌖</span>
                  <span>{device.lokasi || 'Lokasi belum diatur'}</span>
                </div>
                <div className={styles.deviceDetails}>
                  <span><small>Controller</small><strong>{device.controller || '—'}</strong></span>
                  <span><small>Interval</small><strong>{formatInterval(device.intervalMenit)}</strong></span>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </main>
  )
}
