import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTDevices, getIoTLatest } from '../api/iotApi'
import type { IoTDeviceWithLatest, IoTReading, IoTSourceType } from '../types/iot'
import styles from './IoTDashboardPage.module.css'

type Freshness = 'fresh' | 'late' | 'none' | 'unknown'
type AutoRefreshSeconds = 0 | 30 | 60 | 300

const MENU_CODE = 'DASHBOARD_IOT'

const QUICK_ACTIONS = [
  {
    index: '01',
    eyebrow: 'REGISTRY',
    title: 'Daftar perangkat',
    description: 'Lihat seluruh logger Vendor dan Internal yang terdaftar.',
    to: '/iot-management/daftar-perangkat',
    tone: 'teal',
  },
  {
    index: '02',
    eyebrow: 'LIVE MONITORING',
    title: 'Monitoring Sensor IoT',
    description: 'Pantau pembacaan, parameter, dan histori seluruh sumber logger.',
    to: '/iot-management/monitoring-sensor',
    tone: 'blue',
  },
  {
    index: '03',
    eyebrow: 'WEATHER INTELLIGENCE',
    title: 'Prediksi Hujan 14 Hari',
    description: 'Lihat prakiraan berbasis koordinat logger dan peringatan BMKG.',
    to: '/iot-management/prakiraan-cuaca',
    tone: 'sky',
  },
] as const

function parseTimestamp(value: string | number | null | undefined): number | null {
  if (!value) return null
  if (typeof value === 'number') return Number.isFinite(value) ? value : null
  const timestamp = new Date(value).getTime()
  return Number.isFinite(timestamp) ? timestamp : null
}

function getFreshness(reading: IoTReading | null, intervalMinutes: number): Freshness {
  if (!reading) return 'none'
  const timestamp = parseTimestamp(reading.ts)
  if (timestamp === null || timestamp > Date.now()) return 'unknown'
  const allowedDelayMs = Math.max(intervalMinutes * 2, 15) * 60_000
  return Date.now() - timestamp <= allowedDelayMs ? 'fresh' : 'late'
}

function freshnessLabel(freshness: Freshness) {
  return {
    fresh: 'TERKINI',
    late: 'TERLAMBAT',
    none: 'BELUM ADA DATA',
    unknown: 'TIDAK DIKETAHUI',
  }[freshness]
}

function freshnessDescription(freshness: Freshness) {
  return {
    fresh: 'Pembacaan masih dalam interval.',
    late: 'Pembacaan melewati interval yang diharapkan.',
    none: 'Belum ada pembacaan yang tersedia.',
    unknown: 'Status belum dapat dipastikan dari API.',
  }[freshness]
}

function formatDate(value: string | null | undefined) {
  const timestamp = parseTimestamp(value)
  if (timestamp === null) return '—'
  return new Intl.DateTimeFormat('id-ID', { dateStyle: 'medium', timeStyle: 'short' }).format(timestamp)
}

function getSourceLabel(sourceType: IoTSourceType) {
  return sourceType === 'VENDOR' ? 'Vendor' : 'Internal'
}

function getMonitoringPath(_sourceType: IoTSourceType) {
  return '/iot-management/monitoring-sensor'
}

function getFreshnessForDevice(device: IoTDeviceWithLatest): Freshness {
  return device.readingError ? 'unknown' : getFreshness(device.latest, device.intervalMenit)
}

function getAttentionOrder(freshness: Freshness) {
  return { late: 0, none: 1, unknown: 2, fresh: 3 }[freshness]
}

function getLatestSummary(devices: IoTDeviceWithLatest[]) {
  return devices.reduce((summary, device) => {
    const freshness = getFreshnessForDevice(device)
    summary[freshness] += 1
    return summary
  }, { fresh: 0, late: 0, none: 0, unknown: 0 } as Record<Freshness, number>)
}

export default function IoTDashboardPage() {
  const { permission, loaded: permissionLoaded } = useMenuPermission(MENU_CODE)
  const canRead = !!permission?.canRead
  const [devices, setDevices] = useState<IoTDeviceWithLatest[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [autoRefresh, setAutoRefresh] = useState<AutoRefreshSeconds>(0)
  const [refreshKey, setRefreshKey] = useState(0)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)

  const loadDashboard = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const response = await getIoTDevices()
      const latestResults = await Promise.allSettled(response.data.map(device => getIoTLatest(device.loggerId)))
      const rows = response.data.map((device, index) => {
        const result = latestResults[index]
        return {
          ...device,
          latest: result.status === 'fulfilled' ? result.value : null,
          readingError: result.status === 'rejected',
        }
      })

      setDevices(rows)
      setLastUpdated(new Date().toISOString())
    } catch (loadError) {
      console.error('Gagal memuat dashboard IoT:', loadError)
      setDevices([])
      setError('Ringkasan dashboard IoT gagal dimuat. Silakan coba lagi.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (permissionLoaded && canRead) void loadDashboard()
  }, [permissionLoaded, canRead, loadDashboard, refreshKey])

  useEffect(() => {
    if (autoRefresh === 0 || !canRead) return undefined
    const timer = window.setInterval(() => setRefreshKey(key => key + 1), autoRefresh * 1000)
    return () => window.clearInterval(timer)
  }, [autoRefresh, canRead])

  const summary = useMemo(() => getLatestSummary(devices), [devices])
  const activeCount = useMemo(() => devices.filter(device => device.aktif).length, [devices])
  const attentionCount = summary.late + summary.none + summary.unknown
  const attentionDevices = useMemo(() => devices
    .filter(device => getFreshnessForDevice(device) !== 'fresh')
    .sort((left, right) => {
      const freshnessDifference = getAttentionOrder(getFreshnessForDevice(left)) - getAttentionOrder(getFreshnessForDevice(right))
      return freshnessDifference || left.namaLogger.localeCompare(right.namaLogger, 'id-ID')
    })
    .slice(0, 5), [devices])

  const statusRows = [
    { key: 'fresh' as const, label: 'TERKINI', tone: 'fresh' },
    { key: 'late' as const, label: 'TERLAMBAT', tone: 'late' },
    { key: 'none' as const, label: 'BELUM ADA DATA', tone: 'none' },
    { key: 'unknown' as const, label: 'TIDAK DIKETAHUI', tone: 'unknown' },
  ]

  if (!permissionLoaded || !canRead) return <NoAccess loaded={permissionLoaded} />

  return (
    <main className={styles.page}>
      <header className={styles.hero}>
        <div className={styles.heroCopy}>
          <p className={styles.eyebrow}><span className={styles.liveDot} /> IOT MANAGEMENT / OPERATIONS CENTER</p>
          <h1>Dashboard IoT</h1>
          <p className={styles.subtitle}>Satu ringkasan untuk memantau kesehatan logger Vendor dan Internal.</p>
          <div className={styles.heroMeta}>
            <span>UPDATE {lastUpdated ? formatDate(lastUpdated) : '—'}</span>
            <span>SCOPE VENDOR + INTERNAL</span>
          </div>
        </div>
        <div className={styles.heroActions}>
          <label className={styles.compactControl}>
            <span>AUTO REFRESH</span>
            <select value={autoRefresh} onChange={event => setAutoRefresh(Number(event.target.value) as AutoRefreshSeconds)}>
              <option value={0}>Off</option>
              <option value={30}>30 detik</option>
              <option value={60}>1 menit</option>
              <option value={300}>5 menit</option>
            </select>
          </label>
          <button className={styles.refreshButton} type="button" onClick={() => setRefreshKey(key => key + 1)} disabled={loading}>
            <span aria-hidden="true">↻</span>
            {loading ? 'Memuat...' : 'Refresh data'}
          </button>
        </div>
      </header>

      {error && <section className={styles.error} role="alert"><span>{error}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></section>}

      {loading ? (
        <section className={styles.loadingState} aria-live="polite">
          <span className={styles.loadingSpinner} />
          <div><strong>Menyiapkan ringkasan operasional</strong><p>Mengambil daftar logger dan pembacaan terakhir...</p></div>
        </section>
      ) : devices.length === 0 ? (
        <section className={styles.emptyState}>
          <span className={styles.emptyIcon}>—</span>
          <strong>Belum ada logger yang dapat ditampilkan</strong>
          <p>Periksa koneksi API atau buka registry untuk memastikan perangkat sudah terdaftar.</p>
          <Link className={styles.inlineLink} to="/iot-management/daftar-perangkat">Buka daftar perangkat <span aria-hidden="true">↗</span></Link>
        </section>
      ) : (
        <>
          <section className={styles.kpiGrid} aria-label="KPI kesehatan logger">
            <MetricCard label="Total logger" value={devices.length} hint="Vendor + Internal" tone="neutral" icon="ALL" />
            <MetricCard label="Data terkini" value={summary.fresh} hint="Dalam interval sampling" tone="fresh" icon="LIVE" />
            <MetricCard label="Perlu perhatian" value={attentionCount} hint="Terlambat, kosong, atau unknown" tone="warning" icon="!" />
            <MetricCard label="Logger aktif" value={activeCount} hint={`${devices.length ? Math.round((activeCount / devices.length) * 100) : 0}% dari total logger`} tone="blue" icon="ON" />
          </section>

          <section className={styles.dashboardGrid}>
            <section className={styles.healthPanel} aria-label="Distribusi kesehatan logger">
              <div className={styles.panelHeading}>
                <div><span className={styles.panelEyebrow}>FRESHNESS DISTRIBUTION</span><h2>Kondisi fleet saat ini</h2><p>Status dihitung berdasarkan interval sampling masing-masing logger.</p></div>
                <span className={`${styles.healthBadge} ${attentionCount === 0 ? styles.healthyBadge : styles.attentionBadge}`}><i />{attentionCount === 0 ? 'Fleet sehat' : `${attentionCount} perlu perhatian`}</span>
              </div>
              <div className={styles.healthBody}>
                <div className={styles.healthSummary}>
                  <strong>{summary.fresh}</strong>
                  <span>logger mengirim data terkini</span>
                  <small>Terakhir diperbarui {lastUpdated ? formatDate(lastUpdated) : '—'}</small>
                </div>
                <div className={styles.statusBars}>
                  {statusRows.map(row => <StatusBar key={row.key} label={row.label} value={summary[row.key]} total={devices.length} tone={row.tone} />)}
                </div>
              </div>
            </section>

            <section className={styles.attentionPanel} aria-label="Logger yang perlu perhatian">
              <div className={styles.panelHeading}>
                <div><span className={styles.panelEyebrow}>ATTENTION QUEUE</span><h2>Perlu ditindaklanjuti</h2><p>Prioritas berdasarkan status pembacaan terbaru.</p></div>
                <span className={styles.queueCount}>{attentionCount}</span>
              </div>
              {attentionDevices.length === 0 ? (
                <div className={styles.healthyState}><span>✓</span><strong>Semua logger terkini</strong><p>Tidak ada antrian yang perlu ditindaklanjuti saat ini.</p></div>
              ) : (
                <div className={styles.attentionList}>
                  {attentionDevices.map(device => <AttentionRow key={device.loggerId} device={device} />)}
                  {attentionCount > attentionDevices.length && <p className={styles.moreAttention}>+ {attentionCount - attentionDevices.length} logger lainnya perlu ditinjau.</p>}
                </div>
              )}
            </section>
          </section>

          <section className={styles.quickActions} aria-label="Akses cepat IoT Management">
            <div className={styles.quickHeading}><div><span className={styles.panelEyebrow}>WORKSPACES</span><h2>Buka halaman kerja</h2><p>Pisahkan tugas operasional agar setiap halaman tetap fokus.</p></div></div>
            <div className={styles.quickGrid}>
              {QUICK_ACTIONS.map(action => <Link key={action.to} className={`${styles.quickCard} ${styles[action.tone]}`} to={action.to}><span className={styles.quickIndex}>{action.index}</span><span className={styles.quickCopy}><span>{action.eyebrow}</span><strong>{action.title}</strong><small>{action.description}</small></span><span className={styles.quickArrow} aria-hidden="true">↗</span></Link>)}
            </div>
          </section>
        </>
      )}
    </main>
  )
}

function MetricCard({ label, value, hint, tone, icon }: { label: string; value: number; hint: string; tone: string; icon: string }) {
  return <article className={`${styles.metricCard} ${styles[`metric-${tone}`]}`}><span className={styles.metricIcon}>{icon}</span><span className={styles.metricLabel}>{label}</span><strong>{value}</strong><small>{hint}</small></article>
}

function StatusBar({ label, value, total, tone }: { label: string; value: number; total: number; tone: string }) {
  const percentage = total > 0 ? Math.round((value / total) * 100) : 0
  return <div className={styles.statusBar}><span className={styles.statusBarName}>{label}</span><div className={styles.statusTrack}><span className={`${styles.statusFill} ${styles[`status-${tone}`]}`} style={{ width: `${percentage}%` }} /></div><strong className={styles.statusBarValue}>{value}</strong></div>
}

function AttentionRow({ device }: { device: IoTDeviceWithLatest }) {
  const freshness = getFreshnessForDevice(device)
  return <Link className={styles.attentionRow} to={getMonitoringPath(device.sourceType)}><span className={`${styles.attentionDot} ${styles[`dot-${freshness}`]}`} /><span className={styles.attentionMain}><strong>{device.namaLogger}</strong><small>{device.loggerCode} · {getSourceLabel(device.sourceType)} · {device.lokasi || 'Lokasi belum diatur'}</small></span><span className={`${styles.attentionStatus} ${styles[`text-${freshness}`]}`}>{freshnessLabel(freshness)}<small>{device.latest ? formatDate(device.latest.ts) : freshnessDescription(freshness)}</small></span><span className={styles.attentionArrow} aria-hidden="true">↗</span></Link>
}
