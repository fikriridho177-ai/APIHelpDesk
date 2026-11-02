import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
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

interface SensorDefinition {
  title: string
  eyebrow: string
  description: string
  valueLabel: string
  unit: string
  parameterAliases: string[]
  menuCode: string
}

const SENSOR_DEFINITIONS: Record<WadSensorKind, SensorDefinition> = {
  tmat: {
    title: 'Pantauan TMAT',
    eyebrow: 'WAD / LIVE TMAT',
    description: 'Pembacaan terakhir tinggi muka air tanah dari logger yang aktif.',
    valueLabel: 'Tinggi muka air tanah',
    unit: 'cm',
    parameterAliases: ['TMAT', 'TMAT_DISTANCE', 'WATER_TABLE'],
    menuCode: 'WAD_ECS_STASIUN',
  },
  tmak: {
    title: 'Pantauan TMAK',
    eyebrow: 'WAD / LIVE TMAK',
    description: 'Pembacaan terakhir tinggi muka air kanal tanpa memuat rentang data besar.',
    valueLabel: 'Tinggi muka air kanal',
    unit: 'cm',
    parameterAliases: ['TMAK', 'TMAK_DISTANCE', 'TMAK_CM'],
    menuCode: 'WAD_ECS_TMAK',
  },
  'curah-hujan': {
    title: 'Pengecekan Curah Hujan',
    eyebrow: 'WAD / LIVE RAINFALL',
    description: 'Pembacaan terakhir curah hujan per logger dari endpoint monitoring terbaru.',
    valueLabel: 'Curah hujan',
    unit: 'mm',
    parameterAliases: ['RAIN_TIP', 'RAINFALL', 'RAIN', 'CURAH_HUJAN', 'CURAHHUJAN'],
    menuCode: 'WAD_ECS_CURAH_HUJAN',
  },
}

function normalizeParameter(value: string) {
  return value.trim().toUpperCase().replace(/[^A-Z0-9]/g, '')
}

function getMatchingReading(device: IoTMonitoringDevice, aliases: string[]) {
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
  reading: [string, number | null] | undefined,
) {
  if (!reading) return null
  const value = kind === 'tmak'
    ? adjustTmakValue(device.loggerCode, reading[0], reading[1], device.namaLogger)
    : reading[1]
  return getNumber(value)
}

function sensorRows(kind: WadSensorKind, response: IoTMonitoringResponse | null) {
  if (!response) return []
  const definition = SENSOR_DEFINITIONS[kind]

  return response.devices
    .map(device => {
      const reading = getMatchingReading(device, definition.parameterAliases)
      return {
        device,
        reading,
        value: displayValue(kind, device, reading),
      }
    })
    .filter(row => row.reading || Object.keys(row.device.data ?? {}).length === 0)
    .sort((left, right) => {
      const leftStatus = left.device.status === 'ONLINE' ? 0 : 1
      const rightStatus = right.device.status === 'ONLINE' ? 0 : 1
      return leftStatus - rightStatus || (left.device.namaLogger ?? '').localeCompare(right.device.namaLogger ?? '', 'id-ID')
    })
}

export default function WadSensorOverviewPage({ kind }: { kind: WadSensorKind }) {
  const definition = SENSOR_DEFINITIONS[kind]
  const { permission, loaded } = useMenuPermission(definition.menuCode)
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
      console.error('Gagal memuat ' + kind + ' WAD:', loadError)
      setMonitoring(null)
      setError('Data sensor belum dapat dimuat. Periksa koneksi API lalu coba lagi.')
    } finally {
      setLoading(false)
    }
  }, [kind])

  useEffect(() => {
    if (loaded && canRead) void loadMonitoring()
  }, [canRead, loaded, loadMonitoring, refreshKey])

  const rows = useMemo(() => sensorRows(kind, monitoring), [kind, monitoring])
  const onlineCount = rows.filter(row => row.device.status === 'ONLINE').length
  const mappedCount = rows.filter(row => row.device.latitude != null && row.device.longitude != null).length

  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  return (
    <main className={styles.page}>
      <header className={styles.header}>
        <div>
          <p className={styles.eyebrow}><span className={styles.liveDot} /> {definition.eyebrow}</p>
          <h1>{definition.title}</h1>
          <p className={styles.subtitle}>{definition.description}</p>
        </div>
        <button type="button" className={styles.refreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading}>
          {loading ? 'Memuat...' : 'Refresh data'}
        </button>
      </header>

      {error && (
        <section className={styles.alert} role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button>
        </section>
      )}

      <section className={styles.summaryGrid} aria-label={'Ringkasan ' + definition.title}>
        <Summary label="Logger terpantau" value={rows.length} hint="Dari satu response monitoring" />
        <Summary label="Online" value={onlineCount} hint="Status sesuai interval logger" />
        <Summary label="Terpetakan" value={mappedCount} hint="Memiliki koordinat lokasi" />
      </section>

      {loading && !monitoring ? (
        <section className={styles.state}><span className={styles.spinner} /><strong>Mengambil pembacaan terbaru...</strong><small>Memakai endpoint monitoring ringkas.</small></section>
      ) : rows.length === 0 ? (
        <section className={styles.state}><strong>Belum ada pembacaan {definition.title.toLowerCase()}</strong><small>Logger belum mengirim parameter yang sesuai atau data belum tersedia.</small></section>
      ) : (
        <section className={styles.sensorGrid} aria-label="Daftar pembacaan sensor">
          {rows.map(({ device, reading, value }, index) => (
            <article className={styles.sensorCard} key={device.loggerId}>
              <div className={styles.cardTop}>
                <span className={styles.cardNumber}>{String(index + 1).padStart(2, '0')}</span>
                <span className={styles.status + ' ' + styles['status-' + monitoringStatusTone(device.status)]}><i />{monitoringStatusLabel(device.status)}</span>
              </div>

              <div className={styles.identity}>
                <span className={styles.sensorIcon}><DashboardIcon name="water" /></span>
                <div>
                  <small>{device.loggerCode ?? 'LOGGER-' + device.loggerId}</small>
                  <h2>{device.namaLogger ?? 'Logger tanpa nama'}</h2>
                  <span>{device.sourceType ?? 'Sumber tidak diketahui'}</span>
                </div>
              </div>

              <div className={styles.reading}>
                <div>
                  <small>{definition.valueLabel}</small>
                  <strong>{value == null ? '—' : formatMonitoringNumber(value)}</strong>
                </div>
                <span>{definition.unit}</span>
              </div>

              <div className={styles.cardMeta}>
                <span>Data terakhir<strong>{formatMonitoringDate(device.lastSensorAt ?? device.lastReceivedAt)}</strong></span>
                <span>Parameter<strong>{reading?.[0] ?? 'Belum tersedia'}</strong></span>
              </div>

              <div className={styles.cardFooter}>
                <span>{device.lokasi || 'Lokasi belum diatur'}</span>
                <Link to={'/ecs/data-historis?loggerId=' + device.loggerId}>Lihat histori <span aria-hidden="true">→</span></Link>
              </div>
            </article>
          ))}
        </section>
      )}
    </main>
  )
}

function Summary({ label, value, hint }: { label: string; value: number; hint: string }) {
  return <article className={styles.summary}><span>{label}</span><strong>{value}</strong><small>{hint}</small></article>
}
