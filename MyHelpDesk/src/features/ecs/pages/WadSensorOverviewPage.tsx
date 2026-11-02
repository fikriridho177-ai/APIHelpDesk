import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTMonitoring } from '../../iot/api/iotApi'
import {
  formatMonitoringDate,
  formatMonitoringNumber,
  monitoringStatusLabel,
  monitoringStatusTone,
} from '../../iot/lib/monitoring'
import { adjustTmakValue } from '../../iot/lib/tmakOffset'
import type { IoTMonitoringDevice, IoTMonitoringResponse } from '../../iot/types/iot'
import styles from './WadSensorOverviewPage.module.css'

export type WadSensorKind = 'tmat' | 'tmak' | 'curah-hujan'
export type WadSensorFilter = 'all' | WadSensorKind

type SensorReading = [string, number | null] | undefined
type SensorReadings = Record<WadSensorKind, SensorReading>
type FilterIcon = 'grid' | 'water' | 'chart'

interface SensorDefinition {
  title: string
  eyebrow: string
  description: string
  valueLabel: string
  shortLabel: string
  unit: string
  parameterAliases: string[]
  menuCode: string
  icon: FilterIcon
}

const SENSOR_DEFINITIONS: Record<WadSensorKind, SensorDefinition> = {
  tmat: {
    title: 'Pantauan TMAT',
    eyebrow: 'WAD / LIVE TMAT',
    description: 'Pembacaan terakhir tinggi muka air tanah dari logger yang aktif.',
    valueLabel: 'Tinggi muka air tanah',
    shortLabel: 'TMAT',
    unit: 'cm',
    parameterAliases: ['TMAT', 'TMAT_DISTANCE', 'WATER_TABLE'],
    menuCode: 'WAD_ECS_STASIUN',
    icon: 'water',
  },
  tmak: {
    title: 'Pantauan TMAK',
    eyebrow: 'WAD / LIVE TMAK',
    description: 'Pembacaan terakhir tinggi muka air kanal dari logger yang aktif.',
    valueLabel: 'Tinggi muka air kanal',
    shortLabel: 'TMAK',
    unit: 'cm',
    parameterAliases: ['TMAK', 'TMAK_DISTANCE', 'TMAK_CM'],
    menuCode: 'WAD_ECS_TMAK',
    icon: 'water',
  },
  'curah-hujan': {
    title: 'Pengecekan Curah Hujan',
    eyebrow: 'WAD / LIVE RAINFALL',
    description: 'Pembacaan terakhir curah hujan per logger dari endpoint monitoring terbaru.',
    valueLabel: 'Curah hujan',
    shortLabel: 'Hujan',
    unit: 'mm',
    parameterAliases: ['RAIN_TIP', 'RAINFALL', 'RAIN', 'CURAH_HUJAN', 'CURAHHUJAN'],
    menuCode: 'WAD_ECS_CURAH_HUJAN',
    icon: 'chart',
  },
}

const SENSOR_KINDS = Object.keys(SENSOR_DEFINITIONS) as WadSensorKind[]

const FILTER_OPTIONS: Array<{
  key: WadSensorFilter
  label: string
  description: string
  icon: FilterIcon
}> = [
  { key: 'all', label: 'Semua sensor', description: 'Semua parameter terbaru', icon: 'grid' },
  ...SENSOR_KINDS.map(kind => ({
    key: kind,
    label: SENSOR_DEFINITIONS[kind].title,
    description: SENSOR_DEFINITIONS[kind].valueLabel,
    icon: SENSOR_DEFINITIONS[kind].icon,
  })),
]

function isWadSensorFilter(value: string | null): value is WadSensorFilter {
  return value === 'all' || SENSOR_KINDS.includes(value as WadSensorKind)
}

function normalizeParameter(value: string) {
  return value.trim().toUpperCase().replace(/[^A-Z0-9]/g, '')
}

function getMatchingReading(device: IoTMonitoringDevice, aliases: string[]): SensorReading {
  const normalizedAliases = aliases.map(normalizeParameter)
  return Object.entries(device.data ?? {}).find(([key]) => {
    const normalizedKey = normalizeParameter(key.split('#', 1)[0])
    return normalizedAliases.some(alias => normalizedKey === alias || normalizedKey.includes(alias))
  })
}

function getNumber(value: unknown) {
  if (value === null || value === undefined || value === '') return null
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function displayValue(
  kind: WadSensorKind,
  device: IoTMonitoringDevice,
  reading: SensorReading,
) {
  if (!reading) return null
  const value = kind === 'tmak'
    ? adjustTmakValue(device.loggerCode, reading[0], reading[1], device.namaLogger)
    : reading[1]
  return getNumber(value)
}

function sensorRows(filter: WadSensorFilter, response: IoTMonitoringResponse | null) {
  if (!response) return []

  return response.devices
    .map(device => {
      const readings = {} as SensorReadings
      for (const kind of SENSOR_KINDS) {
        readings[kind] = getMatchingReading(device, SENSOR_DEFINITIONS[kind].parameterAliases)
      }

      return {
        device,
        readings,
        reading: filter === 'all' ? undefined : readings[filter],
        value: filter === 'all' ? null : displayValue(filter, device, readings[filter]),
      }
    })
    .sort((left, right) => {
      const leftMatch = filter === 'all' || left.reading ? 0 : 1
      const rightMatch = filter === 'all' || right.reading ? 0 : 1
      const leftStatus = left.device.status === 'ONLINE' ? 0 : 1
      const rightStatus = right.device.status === 'ONLINE' ? 0 : 1
      return leftMatch - rightMatch || leftStatus - rightStatus || (left.device.namaLogger ?? '').localeCompare(right.device.namaLogger ?? '', 'id-ID')
    })
}

export default function WadSensorOverviewPage({
  kind,
  menuCode = 'WAD_ECS_STASIUN',
}: {
  kind?: WadSensorKind
  menuCode?: string
}) {
  const [searchParams, setSearchParams] = useSearchParams()
  const queryFilter = searchParams.get('filter')
  const activeFilter: WadSensorFilter = isWadSensorFilter(queryFilter) ? queryFilter : kind ?? 'all'
  const activeDefinition = activeFilter === 'all' ? undefined : SENSOR_DEFINITIONS[activeFilter]
  const activeFilterOption = FILTER_OPTIONS.find(option => option.key === activeFilter) ?? FILTER_OPTIONS[0]
  const { permission, loaded } = useMenuPermission(menuCode)
  const canRead = !!permission?.canRead
  const [monitoring, setMonitoring] = useState<IoTMonitoringResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  const loadMonitoring = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setMonitoring(await getIoTMonitoring())
    } catch (loadError) {
      console.error('Gagal memuat monitoring sensor WAD:', loadError)
      setMonitoring(null)
      setError('Data sensor belum dapat dimuat. Periksa koneksi API lalu coba lagi.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (loaded && canRead) void loadMonitoring()
  }, [canRead, loaded, loadMonitoring, refreshKey])

  const rows = useMemo(() => sensorRows(activeFilter, monitoring), [activeFilter, monitoring])
  const onlineCount = rows.filter(row => row.device.status === 'ONLINE').length
  const mappedCount = rows.filter(row => row.device.latitude != null && row.device.longitude != null).length
  const readingCount = rows.filter(row => activeFilter !== 'all' && row.reading).length
  const pageTitle = activeDefinition?.title ?? 'Monitoring Sensor WAD'
  const pageDescription = activeDefinition?.description ?? 'Pantau seluruh pembacaan logger dalam satu layar dan fokuskan tampilan dengan filter parameter.'

  const changeFilter = (filter: WadSensorFilter) => {
    setSearchParams({ filter }, { replace: true })
  }

  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  return (
    <main className={styles.page}>
      <header className={styles.hero}>
        <div className={styles.heroCopy}>
          <div className={styles.heroHeading}>
            <span className={styles.heroIcon}><DashboardIcon name={activeDefinition?.icon ?? 'grid'} /></span>
            <div>
              <p className={styles.eyebrow}><span className={styles.liveDot} /> {activeDefinition?.eyebrow ?? 'WAD / LIVE SENSOR'}</p>
              <h1>{pageTitle}</h1>
              <p className={styles.subtitle}>{pageDescription}</p>
            </div>
          </div>
          <div className={styles.heroMeta}>
            <span><strong>{rows.length}</strong> logger tampil</span>
            <span><strong>{onlineCount}</strong> online</span>
            <span>Fokus: {activeFilterOption.label}</span>
          </div>
        </div>
        <div className={styles.heroActions}>
          <span className={styles.liveBadge}><i /> LIVE MONITORING</span>
          <button type="button" className={styles.refreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading}>
            <DashboardIcon name="chart" />
            {loading ? 'Memuat...' : 'Refresh data'}
          </button>
        </div>
      </header>

      {error && (
        <section className={styles.alert} role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button>
        </section>
      )}

      <section className={styles.summaryPanel} aria-label="Filter dan ringkasan monitoring sensor">
        <div className={styles.panelHeading}>
          <div>
            <span className={styles.panelEyebrow}>MONITORING SENSOR WAD</span>
            <h2>Semua sensor dalam satu tampilan</h2>
            <p>Pilih fokus untuk menonjolkan parameter tanpa berpindah halaman.</p>
          </div>
          <span className={styles.panelStamp}>1 snapshot API</span>
        </div>

        <div className={styles.filterBar}>
          <div className={styles.filterCopy}>
            <span className={styles.filterEyebrow}>FOKUS PEMANTAUAN</span>
            <strong>{activeFilterOption.label}</strong>
            <small>{activeFilterOption.description}</small>
          </div>
          <div className={styles.filterTabs} role="tablist" aria-label="Filter parameter sensor">
            {FILTER_OPTIONS.map(option => (
              <button
                key={option.key}
                type="button"
                role="tab"
                aria-selected={activeFilter === option.key}
                className={`${styles.filterButton} ${activeFilter === option.key ? styles.filterButtonActive : ''}`}
                onClick={() => changeFilter(option.key)}
              >
                <span className={styles.filterIcon}><DashboardIcon name={option.icon} /></span>
                <span>
                  <strong>{option.label}</strong>
                  <small>{option.description}</small>
                </span>
              </button>
            ))}
          </div>
        </div>

        <div className={styles.summaryGrid}>
          <Summary icon="chart" tone="cyan" label="Logger tampil" value={rows.length} hint="Sesuai fokus monitoring" />
          <Summary icon="check" tone="green" label="Online" value={onlineCount} hint="Status dalam interval logger" />
          <Summary
            icon="map"
            tone="blue"
            label={activeFilter === 'all' ? 'Terpetakan' : 'Parameter terbaca'}
            value={activeFilter === 'all' ? mappedCount : readingCount}
            hint={activeFilter === 'all' ? 'Memiliki koordinat lokasi' : activeDefinition?.valueLabel ?? 'Parameter terpilih'}
          />
        </div>
      </section>

      {loading && !monitoring ? (
        <section className={styles.state}>
          <span className={styles.spinner} />
          <strong>Mengambil pembacaan terbaru...</strong>
          <small>Memakai satu endpoint monitoring ringkas.</small>
        </section>
      ) : rows.length === 0 ? (
        <section className={styles.state}>
          <DashboardIcon name="info" />
          <strong>Belum ada logger pada snapshot ini</strong>
          <small>Refresh data atau tunggu pembacaan sensor terbaru.</small>
        </section>
      ) : (
        <section className={styles.sensorPanel} aria-label="Daftar pembacaan sensor">
          <div className={styles.panelHeading}>
            <div>
              <span className={styles.panelEyebrow}>LOGGER &amp; PEMBACAAN</span>
              <h2>{activeFilter === 'all' ? 'Semua logger' : `Logger dengan ${activeDefinition?.shortLabel}`}</h2>
              <p>{activeFilter === 'all' ? 'Semua parameter terbaru diringkas di setiap kartu sensor.' : `${activeDefinition?.valueLabel} dibuat paling menonjol pada kartu sensor.`}</p>
            </div>
            <span className={styles.panelStamp}>{rows.length} stasiun</span>
          </div>
          <div className={styles.sensorGrid}>
            {rows.map(({ device, readings, reading, value }, index) => (
              <article className={`${styles.sensorCard} ${activeFilter !== 'all' ? styles.sensorCardActive : ''} ${activeFilter !== 'all' && !reading ? styles.sensorCardMuted : ''}`} key={device.loggerId}>
                <div className={styles.cardTop}>
                  <span className={styles.cardNumber}>{String(index + 1).padStart(2, '0')}</span>
                  <span className={`${styles.status} ${styles['status-' + monitoringStatusTone(device.status)]}`}><i />{monitoringStatusLabel(device.status)}</span>
                </div>

                <div className={styles.identity}>
                  <span className={styles.sensorIcon}><DashboardIcon name={activeDefinition?.icon ?? 'grid'} /></span>
                  <div>
                    <small>{device.loggerCode ?? 'LOGGER-' + device.loggerId}</small>
                    <h3>{device.namaLogger ?? 'Logger tanpa nama'}</h3>
                    <span>{device.sourceType ?? 'Sumber tidak diketahui'}</span>
                  </div>
                </div>

                {activeFilter === 'all' ? (
                  <MetricList device={device} readings={readings} />
                ) : (
                  <div className={styles.reading}>
                    <div>
                      <small>{activeDefinition?.valueLabel}</small>
                      <strong>{value == null ? '—' : formatMonitoringNumber(value)}</strong>
                    </div>
                    <span>{activeDefinition?.unit}</span>
                  </div>
                )}

                <div className={styles.cardMeta}>
                  <span>Data terakhir<strong>{formatMonitoringDate(device.lastSensorAt ?? device.lastReceivedAt)}</strong></span>
                  <span>{activeFilter === 'all' ? 'Parameter tersedia' : 'Parameter'}<strong>{activeFilter === 'all' ? availableParameters(readings) : reading?.[0] ?? 'Belum tersedia'}</strong></span>
                </div>

                <div className={styles.cardFooter}>
                  <span>{device.lokasi || 'Lokasi belum diatur'}</span>
                  <Link to={'/ecs/data-historis?loggerId=' + device.loggerId}>Lihat histori <span aria-hidden="true">→</span></Link>
                </div>
              </article>
            ))}
          </div>
        </section>
      )}
    </main>
  )
}

function availableParameters(readings: SensorReadings) {
  const labels = SENSOR_KINDS
    .filter(kind => readings[kind])
    .map(kind => SENSOR_DEFINITIONS[kind].shortLabel)
  return labels.length > 0 ? labels.join(' · ') : 'Belum tersedia'
}

function MetricList({ device, readings }: { device: IoTMonitoringDevice; readings: SensorReadings }) {
  return (
    <div className={styles.metricList}>
      <small className={styles.metricListLabel}>PEMBACAAN TERBARU</small>
      <div className={styles.metricGrid}>
        {SENSOR_KINDS.map(kind => {
          const definition = SENSOR_DEFINITIONS[kind]
          const reading = readings[kind]
          const value = displayValue(kind, device, reading)
          return (
            <div className={`${styles.metric} ${reading ? styles.metricAvailable : styles.metricEmpty}`} key={kind}>
              <span>{definition.shortLabel}</span>
              <strong>{value == null ? '—' : formatMonitoringNumber(value)}</strong>
              <small>{definition.unit}</small>
            </div>
          )
        })}
      </div>
    </div>
  )
}

function Summary({ icon, tone, label, value, hint }: { icon: 'chart' | 'check' | 'map'; tone: 'cyan' | 'green' | 'blue'; label: string; value: number; hint: string }) {
  return (
    <article className={`${styles.summary} ${styles['summary-' + tone]}`}>
      <span className={styles.summaryIcon}><DashboardIcon name={icon} /></span>
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        <small>{hint}</small>
      </div>
    </article>
  )
}
