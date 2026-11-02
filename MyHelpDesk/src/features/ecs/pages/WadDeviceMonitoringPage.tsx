import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTMonitoring } from '../../iot/api/iotApi'
import {
  formatMonitoringDate,
  monitoringStatusLabel,
  monitoringStatusTone,
} from '../../iot/lib/monitoring'
import type { IoTMonitoringDevice, IoTMonitoringResponse } from '../../iot/types/iot'
import common from './WadSensorOverviewPage.module.css'
import styles from './WadDeviceMonitoringPage.module.css'

type DeviceFilter = 'all' | 'online' | 'attention'

const DEVICE_REFRESH_INTERVAL_MS = 60_000

const DEVICE_FILTERS: Array<{ key: DeviceFilter; label: string; description: string }> = [
  { key: 'all', label: 'Semua perangkat', description: 'Seluruh logger terdaftar' },
  { key: 'online', label: 'Online', description: 'Payload masih dalam interval' },
  { key: 'attention', label: 'Perlu perhatian', description: 'Terlambat atau tidak ada payload' },
]

function isDeviceOnline(device: IoTMonitoringDevice) {
  return device.aktif && device.status === 'ONLINE'
}

function isDeviceAttention(device: IoTMonitoringDevice) {
  return !isDeviceOnline(device)
}

function formatDeviceAge(ageMinutes: number | null) {
  if (ageMinutes == null || !Number.isFinite(ageMinutes)) return 'Belum ada payload'
  if (ageMinutes < 1) return 'Baru saja'
  if (ageMinutes < 60) return `${Math.round(ageMinutes)} menit lalu`

  const hours = ageMinutes / 60
  if (hours < 24) return `${Math.round(hours * 10) / 10} jam lalu`

  return `${Math.round(hours / 24 * 10) / 10} hari lalu`
}

function formatInterval(intervalMenit: number) {
  return intervalMenit > 0 ? `${intervalMenit} menit` : 'Tidak diatur'
}

function getPayloadDate(device: IoTMonitoringDevice) {
  return device.lastReceivedAt ?? device.lastSensorAt
}

function getPayloadLabel(device: IoTMonitoringDevice) {
  return device.lastReceivedAt ? 'Payload terakhir diterima' : 'Data sensor terakhir'
}

function filteredDevices(response: IoTMonitoringResponse | null, filter: DeviceFilter) {
  const devices = response?.devices ?? []

  return devices
    .filter(device => {
      if (filter === 'online') return isDeviceOnline(device)
      if (filter === 'attention') return isDeviceAttention(device)
      return true
    })
    .sort((left, right) => {
      const onlineOrder = Number(isDeviceOnline(right)) - Number(isDeviceOnline(left))
      if (onlineOrder !== 0) return onlineOrder

      const leftAge = left.ageMinutes ?? Number.POSITIVE_INFINITY
      const rightAge = right.ageMinutes ?? Number.POSITIVE_INFINITY
      if (leftAge !== rightAge) return leftAge - rightAge

      return (left.namaLogger ?? '').localeCompare(right.namaLogger ?? '', 'id-ID')
    })
}

export default function WadDeviceMonitoringPage() {
  const { permission, loaded } = useMenuPermission('WAD_ECS_STASIUN')
  const canRead = !!permission?.canRead
  const [monitoring, setMonitoring] = useState<IoTMonitoringResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [filter, setFilter] = useState<DeviceFilter>('all')
  const [refreshKey, setRefreshKey] = useState(0)

  const loadMonitoring = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setMonitoring(await getIoTMonitoring())
    } catch (loadError) {
      console.error('Gagal memuat status perangkat WAD:', loadError)
      setMonitoring(null)
      setError('Status perangkat belum dapat dimuat. Periksa koneksi API lalu coba lagi.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!loaded || !canRead) return

    void loadMonitoring()

    const intervalId = window.setInterval(() => {
      if (document.visibilityState === 'visible') void loadMonitoring()
    }, DEVICE_REFRESH_INTERVAL_MS)

    return () => window.clearInterval(intervalId)
  }, [canRead, loaded, loadMonitoring, refreshKey])

  const devices = monitoring?.devices ?? []
  const rows = useMemo(() => filteredDevices(monitoring, filter), [filter, monitoring])
  const onlineCount = devices.filter(isDeviceOnline).length
  const attentionCount = devices.filter(isDeviceAttention).length
  const activeFilter = DEVICE_FILTERS.find(option => option.key === filter) ?? DEVICE_FILTERS[0]

  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  return (
    <main className={common.page}>
      <header className={common.hero}>
        <div className={common.heroCopy}>
          <div className={common.heroHeading}>
            <span className={common.heroIcon}><DashboardIcon name="desktop" /></span>
            <div>
              <p className={common.eyebrow}><span className={common.liveDot} /> WAD / DEVICE HEALTH</p>
              <h1>Status Perangkat IoT</h1>
              <p className={common.subtitle}>
                Pantau keaktifan logger, usia payload, dan waktu terakhir perangkat mengirim data.
              </p>
            </div>
          </div>
          <div className={common.heroMeta}>
            <span><strong>{devices.length}</strong> perangkat terpantau</span>
            <span><strong>{onlineCount}</strong> online</span>
            <span><strong>{attentionCount}</strong> perlu perhatian</span>
          </div>
        </div>
        <div className={common.heroActions}>
          <span className={common.liveBadge}><i /> DEVICE MONITORING</span>
          <button type="button" className={common.refreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading}>
            <DashboardIcon name="chart" />
            {loading ? 'Memuat...' : 'Refresh data'}
          </button>
        </div>
      </header>

      {error && (
        <section className={common.alert} role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button>
        </section>
      )}

      <section className={common.summaryPanel} aria-label="Ringkasan kesehatan perangkat">
        <div className={common.panelHeading}>
          <div>
            <span className={common.panelEyebrow}>STATUS PERANGKAT WAD</span>
            <h2>Kesehatan logger dalam satu tampilan</h2>
            <p>Gunakan status online untuk melihat perangkat yang masih mengirim payload sesuai interval.</p>
          </div>
          <span className={common.panelStamp}>Refresh tiap 60 detik</span>
        </div>

        <div className={common.summaryGrid}>
          <DeviceSummary icon="desktop" tone="cyan" label="Perangkat terpantau" value={devices.length} hint="Logger pada snapshot terbaru" />
          <DeviceSummary icon="check" tone="green" label="Online" value={onlineCount} hint="Payload dalam interval logger" />
          <DeviceSummary icon="clock" tone="blue" label="Perlu perhatian" value={attentionCount} hint="Terlambat, nonaktif, atau tanpa data" />
        </div>

        <div className={styles.deviceFilterBar}>
          <div className={common.filterCopy}>
            <span className={common.filterEyebrow}>TAMPILAN PERANGKAT</span>
            <strong>{activeFilter.label}</strong>
            <small>{activeFilter.description}</small>
          </div>
          <div className={styles.deviceFilterTabs} role="tablist" aria-label="Filter status perangkat">
            {DEVICE_FILTERS.map(option => (
              <button
                key={option.key}
                type="button"
                role="tab"
                aria-selected={filter === option.key}
                className={`${styles.deviceFilterButton} ${filter === option.key ? styles.deviceFilterButtonActive : ''}`}
                onClick={() => setFilter(option.key)}
              >
                <span className={styles.deviceFilterIcon}><DashboardIcon name={option.key === 'online' ? 'check' : option.key === 'attention' ? 'clock' : 'grid'} /></span>
                <span>
                  <strong>{option.label}</strong>
                  <small>{option.description}</small>
                </span>
              </button>
            ))}
          </div>
        </div>
      </section>

      {loading && !monitoring ? (
        <section className={common.state}>
          <span className={common.spinner} />
          <strong>Mengambil status perangkat terbaru...</strong>
          <small>Memakai satu endpoint monitoring ringkas.</small>
        </section>
      ) : rows.length === 0 ? (
        <section className={common.state}>
          <DashboardIcon name="info" />
          <strong>{devices.length === 0 ? 'Belum ada perangkat pada snapshot ini' : 'Tidak ada perangkat pada filter ini'}</strong>
          <small>{devices.length === 0 ? 'Refresh data atau periksa konfigurasi logger.' : 'Pilih tampilan perangkat yang lain.'}</small>
        </section>
      ) : (
        <section className={common.sensorPanel} aria-label="Daftar status perangkat IoT">
          <div className={common.panelHeading}>
            <div>
              <span className={common.panelEyebrow}>LOGGER &amp; PAYLOAD</span>
              <h2>Daftar perangkat</h2>
              <p>Lihat waktu payload terakhir dan status setiap logger tanpa membuka halaman sensor satu per satu.</p>
            </div>
            <span className={common.panelStamp}>{rows.length} perangkat</span>
          </div>
          <div className={styles.deviceGrid}>
            {rows.map((device, index) => <DeviceCard key={device.loggerId} device={device} index={index} />)}
          </div>
        </section>
      )}
    </main>
  )
}

function DeviceCard({ device, index }: { device: IoTMonitoringDevice; index: number }) {
  const online = isDeviceOnline(device)
  const payloadDate = getPayloadDate(device)
  const tone = monitoringStatusTone(device.status)

  return (
    <article className={`${styles.deviceCard} ${online ? styles.deviceCardOnline : styles.deviceCardAttention}`}>
      <div className={styles.deviceTop}>
        <span className={styles.deviceNumber}>{String(index + 1).padStart(2, '0')}</span>
        <span className={`${common.status} ${common['status-' + tone]}`}><i />{monitoringStatusLabel(device.status)}</span>
      </div>

      <div className={styles.deviceIdentity}>
        <span className={styles.deviceIcon}><DashboardIcon name="desktop" /></span>
        <div>
          <small>{device.loggerCode ?? `LOGGER-${device.loggerId}`}</small>
          <h3>{device.namaLogger ?? 'Logger tanpa nama'}</h3>
          <span>{device.sourceType ?? 'Sumber tidak diketahui'}</span>
        </div>
      </div>

      <div className={styles.payloadBlock}>
        <div>
          <small>{getPayloadLabel(device)}</small>
          <strong>{formatMonitoringDate(payloadDate)}</strong>
          <span>{formatDeviceAge(device.ageMinutes)}</span>
        </div>
        <span className={`${styles.payloadBadge} ${online ? styles.payloadBadgeOnline : styles.payloadBadgeAttention}`}>
          {online ? 'ONLINE' : 'PERLU CEK'}
        </span>
      </div>

      <div className={styles.detailGrid}>
        <div>
          <span>Interval logger</span>
          <strong>{formatInterval(device.intervalMenit)}</strong>
        </div>
        <div>
          <span>Konfigurasi logger</span>
          <strong>{device.aktif ? 'Aktif' : 'Nonaktif'}</strong>
        </div>
      </div>

      <div className={styles.deviceFooter}>
        <span>{device.lokasi || 'Lokasi belum diatur'}</span>
        <Link to={`/ecs/data-historis?loggerId=${device.loggerId}`}>
          Lihat histori <DashboardIcon name="arrow" />
        </Link>
      </div>
    </article>
  )
}

function DeviceSummary({
  icon,
  tone,
  label,
  value,
  hint,
}: {
  icon: 'desktop' | 'check' | 'clock'
  tone: 'cyan' | 'green' | 'blue'
  label: string
  value: number
  hint: string
}) {
  return (
    <article className={`${common.summary} ${common['summary-' + tone]}`}>
      <span className={common.summaryIcon}><DashboardIcon name={icon} /></span>
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        <small>{hint}</small>
      </div>
    </article>
  )
}
