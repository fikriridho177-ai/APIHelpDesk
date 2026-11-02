import { useCallback, useEffect, useMemo, useState, type CSSProperties, type ReactNode } from 'react'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  type TooltipProps,
} from 'recharts'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { useSearchParams } from 'react-router-dom'

import { getIoTHistory, getIoTMonitoring, getIoTParameters } from '../api/iotApi'
import { adjustTmakValue } from '../lib/tmakOffset'
import type {
  IoTDeviceWithLatest,
  IoTHistoryAggregation,
  IoTHistoryPeriod,
  IoTHistoryQuery,
  IoTHistoryResponse,
  IoTMonitoringDevice,
  IoTParameter,
  IoTReading,
  IoTSourceType,
  SensorValue,
} from '../types/iot'
import styles from './IoTMonitoringPage.module.css'

type Freshness = 'fresh' | 'late' | 'none' | 'unknown'
type AutoRefreshSeconds = 0 | 30 | 60 | 300

interface ChartPoint { ts: number; value: number }
interface ChartParameter { parameterCode: string; metadata?: IoTParameter }
interface ParameterGroup { code: string; name: string; order: number; parameters: ChartParameter[] }
type VisualizationKind = 'trend' | 'rainfall' | 'direction' | 'level' | 'gauge' | 'heat'

interface ChartStats {
  min: number | null
  max: number | null
  average: number | null
}

const MENU_CODE = 'DASHBOARD_IOT'
const CHART_COLORS = ['#30d5c8', '#54a8ff', '#f5b84b', '#b88cff', '#ff7b8b', '#72d572']
const HISTORY_PERIOD_OPTIONS: Array<{ value: IoTHistoryPeriod; label: string }> = [
  { value: 'day', label: 'Harian · 24 jam' },
  { value: 'week', label: 'Mingguan · 7 hari' },
  { value: 'month', label: 'Bulanan · 30 hari' },
  { value: 'year', label: 'Tahunan · 12 bulan' },
]

interface IoTMonitoringPageProps {
  sourceType?: IoTSourceType
  menuCode?: string
  permissionCode?: string
  lockLogger?: boolean
  title?: string
  subtitle?: string
  eyebrow?: string
  showAllParameters?: boolean
}

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

function toDeviceWithLatest(device: IoTMonitoringDevice): IoTDeviceWithLatest {
  const sourceType: IoTSourceType = device.sourceType === 'INTERNAL' ? 'INTERNAL' : 'VENDOR'
  const hasLatest = device.rawId !== null && (device.lastSensorAt !== null || device.lastReceivedAt !== null)

  return {
    loggerId: device.loggerId,
    loggerCode: device.loggerCode ?? `LOGGER-${device.loggerId}`,
    namaLogger: device.namaLogger ?? `Logger ${device.loggerId}`,
    sourceType,
    lokasi: device.lokasi,
    latitude: device.latitude,
    longitude: device.longitude,
    controller: null,
    communication: null,
    intervalMenit: device.intervalMenit,
    aktif: device.aktif,
    latest: hasLatest
      ? {
          rawId: device.rawId!,
          loggerId: device.loggerId,
          ts: device.lastSensorAt ?? device.lastReceivedAt ?? '',
          receivedAt: device.lastReceivedAt ?? device.lastSensorAt ?? '',
          data: device.data,
        }
      : null,
    readingError: false,
  }
}

function freshnessLabel(freshness: Freshness) {
  return { fresh: 'TERKINI', late: 'TERLAMBAT', none: 'BELUM ADA DATA', unknown: 'TIDAK DIKETAHUI' }[freshness]
}

function formatDate(value: string | null | undefined, withSeconds = false) {
  const timestamp = parseTimestamp(value)
  if (timestamp === null) return '-'
  return new Intl.DateTimeFormat('id-ID', { dateStyle: 'medium', timeStyle: withSeconds ? 'medium' : 'short' }).format(timestamp)
}

function getDateInputValue(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function getDateTimeInputValue(date: Date) {
  const dateValue = getDateInputValue(date)
  const hours = String(date.getHours()).padStart(2, '0')
  const minutes = String(date.getMinutes()).padStart(2, '0')
  return `${dateValue}T${hours}:${minutes}`
}

function parseDateInputValue(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

function getNextDateInputValue(value: string) {
  const nextDate = parseDateInputValue(value)
  nextDate.setDate(nextDate.getDate() + 1)
  return getDateInputValue(nextDate)
}

function getHistoryRangeDefaults(anchorDate: string, period: Exclude<IoTHistoryPeriod, 'day'>) {
  const anchor = parseDateInputValue(anchorDate)
  const start = new Date(anchor)
  const end = new Date(anchor)

  if (period === 'week') start.setDate(start.getDate() - 6)
  if (period === 'month') start.setDate(start.getDate() - 29)
  if (period === 'year') {
    start.setDate(1)
    start.setMonth(start.getMonth() - 11)
    end.setDate(1)
    end.setMonth(end.getMonth() + 1)
  } else {
    end.setDate(end.getDate() + 1)
  }

  const now = new Date()
  const boundedEnd = end > now ? now : end
  return {
    from: getDateTimeInputValue(start),
    to: getDateTimeInputValue(boundedEnd),
  }
}

function formatChartTime(value: string | number, aggregation: IoTHistoryAggregation = 'hour') {
  const timestamp = parseTimestamp(value)
  if (timestamp === null) return '-'
  if (aggregation === 'sample') return new Intl.DateTimeFormat('id-ID', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(timestamp)
  if (aggregation === 'hour') return new Intl.DateTimeFormat('id-ID', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(timestamp)
  if (aggregation === 'month') return new Intl.DateTimeFormat('id-ID', { month: 'long', year: 'numeric' }).format(timestamp)
  return new Intl.DateTimeFormat('id-ID', { day: '2-digit', month: 'short', year: 'numeric' }).format(timestamp)
}

function formatAxisTime(value: string | number, aggregation: IoTHistoryAggregation = 'hour') {
  const timestamp = parseTimestamp(value)
  if (timestamp === null) return '-'
  if (aggregation === 'sample') return new Intl.DateTimeFormat('id-ID', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }).format(timestamp)
  if (aggregation === 'hour') return new Intl.DateTimeFormat('id-ID', { day: '2-digit', month: 'short', hour: '2-digit' }).format(timestamp)
  if (aggregation === 'month') return new Intl.DateTimeFormat('id-ID', { month: 'short', year: '2-digit' }).format(timestamp)
  return new Intl.DateTimeFormat('id-ID', { day: '2-digit', month: 'short' }).format(timestamp)
}

function getNumericValue(value: SensorValue | undefined): number | null {
  if (typeof value !== 'number' && typeof value !== 'string') return null
  if (typeof value === 'string' && value.trim() === '') return null
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function getNumericKeys(readings: IoTReading[]) {
  const keys = new Set<string>()
  readings.forEach(reading => Object.entries(reading.data).forEach(([key, value]) => { if (getNumericValue(value) !== null) keys.add(key) }))
  return [...keys]
}

function getChartPoints(readings: IoTReading[], key: string, loggerCode: string | null | undefined, loggerName: string | null | undefined): ChartPoint[] {
  const points: ChartPoint[] = []
  readings.forEach(reading => {
    const timestamp = parseTimestamp(reading.ts)
    const value = getNumericValue(adjustTmakValue(loggerCode, key, reading.data[key], loggerName))
    if (timestamp === null || value === null) return
    points.push({ ts: timestamp, value })
  })
  return points.sort((left, right) => left.ts - right.ts)
}

function getVisualizationKind(parameter: ChartParameter): VisualizationKind {
  const searchable = `${parameter.parameterCode} ${parameter.metadata?.namaParameter ?? ''}`.toLocaleUpperCase('id-ID')
  if (/RAIN|RAINFALL|CURAH|HUJAN|PRECIP/.test(searchable)) return 'rainfall'
  if (/WIND_DIRECTION|WIND_DIR|WIND_DEGREE|DIRECTION|ARAH.*ANGIN|ANGIN.*ARAH/.test(searchable)) return 'direction'
  if (/TMAK|TMAT|TINGGI MUKA AIR|WATER LEVEL|LEVEL AIR/.test(searchable)) return 'level'
  if (/KELEMBAPAN|HUMIDITY|WIND_SPEED|WIND VELOCITY|KECEPATAN ANGIN/.test(searchable)) return 'gauge'
  if (/RADIASI|RADIATION|PENYINARAN|EVAPOTRANSPIRASI|SOLAR/.test(searchable)) return 'heat'
  return 'trend'
}

function getChartStats(points: ChartPoint[]): ChartStats {
  const values = points.map(point => point.value).filter((value): value is number => value !== null && Number.isFinite(value))
  if (values.length === 0) return { min: null, max: null, average: null }
  return {
    min: Math.min(...values),
    max: Math.max(...values),
    average: values.reduce((total, value) => total + value, 0) / values.length,
  }
}

function getDirectionLabel(value: number | null) {
  if (value === null || !Number.isFinite(value)) return 'Belum ada arah'
  const normalized = ((value % 360) + 360) % 360
  const directions = ['Utara', 'Timur Laut', 'Timur', 'Tenggara', 'Selatan', 'Barat Daya', 'Barat', 'Barat Laut']
  return directions[Math.round(normalized / 45) % directions.length]
}

function formatSensorValue(value: SensorValue | undefined) {
  const numericValue = getNumericValue(value)
  if (numericValue !== null) return new Intl.NumberFormat('id-ID', { maximumFractionDigits: 2 }).format(numericValue)
  if (typeof value === 'string' && value.trim()) return value
  return '—'
}

function getParameterLabel(parameter: IoTParameter | undefined) {
  const name = parameter?.namaParameter?.trim() || 'Parameter sensor'
  const unit = parameter?.satuan?.trim()
  if (!unit || name.toLocaleLowerCase('id-ID').includes(unit.toLocaleLowerCase('id-ID'))) return name
  return `${name} (${unit})`
}

function findParameterMetadata(parameters: IoTParameter[], parameterCode: string) {
  const matches = parameters.filter(parameter => parameter.parameterCode?.trim().toLocaleUpperCase('id-ID') === parameterCode.trim().toLocaleUpperCase('id-ID'))
  return matches.find(parameter => parameter.categoryName?.trim() || parameter.categoryCode?.trim() || parameter.kategoriNama?.trim() || parameter.kategoriCode?.trim()) ?? matches[0]
}

function getParameterCategory(parameter: IoTParameter | undefined) {
  const code = parameter?.categoryCode?.trim() || parameter?.kategoriCode?.trim() || ''
  const name = parameter?.categoryName?.trim() || parameter?.kategoriNama?.trim() || ''
  const order = parameter?.categoryOrder ?? parameter?.kategoriUrutan ?? 999
  return { code: code || name.toLocaleUpperCase('id-ID') || 'LAINNYA', name: name || code || 'DATA LAINNYA', order }
}

function getLatestSummary(devices: IoTDeviceWithLatest[]) {
  return devices.reduce((summary, device) => {
    const freshness = device.readingError ? 'unknown' : getFreshness(device.latest, device.intervalMenit)
    summary[freshness] += 1
    return summary
  }, { fresh: 0, late: 0, none: 0, unknown: 0 } as Record<Freshness, number>)
}

function getCategoryColor(index: number) { return CHART_COLORS[index % CHART_COLORS.length] }

function getAggregationLabel(aggregation: IoTHistoryAggregation | undefined) {
  if (aggregation === 'sample') return 'interval logger'
  return aggregation === 'hour' ? 'jam' : aggregation === 'month' ? 'bulan' : 'hari'
}

function getDefaultHistoryAggregation(period: IoTHistoryPeriod): IoTHistoryAggregation {
  return period === 'day' ? 'sample' : period === 'year' ? 'month' : 'day'
}

export default function IoTMonitoringPage({
  sourceType,
  menuCode = MENU_CODE,
  permissionCode,
  lockLogger = false,
  title = 'Monitoring Sensor IoT',
  subtitle = 'Pantau logger Vendor dan Internal dalam satu halaman dengan histori pembacaan bertimestamp.',
  eyebrow = 'IOT MONITORING / UNIFIED TELEMETRY',
}: IoTMonitoringPageProps = {}) {
  const { permission, loaded: permissionLoaded } = useMenuPermission(permissionCode ?? menuCode)
  const [searchParams] = useSearchParams()
  const requestedLoggerId = Number(searchParams.get('loggerId'))
  const isLoggerLocked = lockLogger || (Number.isInteger(requestedLoggerId) && requestedLoggerId > 0)
  const canRead = !!permission?.canRead
  const [devices, setDevices] = useState<IoTDeviceWithLatest[]>([])
  const [history, setHistory] = useState<IoTHistoryResponse | null>(null)
  const [parameters, setParameters] = useState<IoTParameter[]>([])
  const [loading, setLoading] = useState(false)
  const [historyLoading, setHistoryLoading] = useState(false)
  const [parametersLoading, setParametersLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [historyError, setHistoryError] = useState<string | null>(null)
  const [parametersError, setParametersError] = useState<string | null>(null)
  const [sourceFilter, setSourceFilter] = useState<IoTSourceType | ''>(sourceType ?? '')
  const [deviceFilter, setDeviceFilter] = useState('')
  const [selectedLoggerId, setSelectedLoggerId] = useState<number | null>(null)
  const [historyPeriod, setHistoryPeriod] = useState<IoTHistoryPeriod>('day')
  const [historyAnchorDate, setHistoryAnchorDate] = useState(() => getDateInputValue(new Date()))
  const [historyRangeStart, setHistoryRangeStart] = useState(() => getHistoryRangeDefaults(getDateInputValue(new Date()), 'week').from)
  const [historyRangeEnd, setHistoryRangeEnd] = useState(() => getHistoryRangeDefaults(getDateInputValue(new Date()), 'week').to)
  const [autoRefresh, setAutoRefresh] = useState<AutoRefreshSeconds>(0)
  const [refreshKey, setRefreshKey] = useState(0)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)

  const loadDevices = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await getIoTMonitoring()
      const allRows = response.devices.map(toDeviceWithLatest)
      const rows = sourceType
        ? allRows.filter(device => device.sourceType === sourceType)
        : allRows
      setDevices(rows)
      setLastUpdated(new Date().toISOString())
      const requestedDevice = allRows.find(device => device.loggerId === requestedLoggerId)
      setSelectedLoggerId(current => {
        if (isLoggerLocked) return requestedDevice?.loggerId ?? null
        return current !== null && rows.some(device => device.loggerId === current)
          ? current
          : requestedDevice?.loggerId ?? rows[0]?.loggerId ?? null
      })
    } catch (loadError) {
      console.error('Gagal memuat perangkat IoT:', loadError)
      setError('Daftar perangkat IoT gagal dimuat. Silakan coba lagi.')
      setDevices([])
      setSelectedLoggerId(null)
    } finally { setLoading(false) }
  }, [isLoggerLocked, requestedLoggerId, sourceType])

  useEffect(() => { if (permissionLoaded && canRead) void loadDevices() }, [permissionLoaded, canRead, loadDevices, refreshKey])

  useEffect(() => {
    if (autoRefresh === 0 || !canRead) return undefined
    const timer = window.setInterval(() => setRefreshKey(key => key + 1), autoRefresh * 1000)
    return () => window.clearInterval(timer)
  }, [autoRefresh, canRead])

  const filteredDevices = useMemo(() => {
    const search = deviceFilter.trim().toLocaleLowerCase('id-ID')
    return devices.filter(device => {
      const matchesSource = !sourceFilter || device.sourceType === sourceFilter
      const searchable = `${device.namaLogger} ${device.loggerCode} ${device.lokasi ?? ''}`.toLocaleLowerCase('id-ID')
      return matchesSource && (!search || searchable.includes(search))
    })
  }, [devices, sourceFilter, deviceFilter])

  const selectedDevice = isLoggerLocked
    ? devices.find(device => device.loggerId === requestedLoggerId) ?? null
    : filteredDevices.find(device => device.loggerId === selectedLoggerId) ?? filteredDevices[0] ?? null

  const historyRangeError = historyPeriod === 'day'
    ? null
    : !historyRangeStart || !historyRangeEnd
      ? 'Waktu mulai dan akhir wajib diisi.'
      : historyRangeStart >= historyRangeEnd
        ? 'Waktu mulai harus sebelum waktu akhir.'
        : null

  const handleHistoryPeriodChange = (nextPeriod: IoTHistoryPeriod) => {
    if (nextPeriod === 'day') {
      setHistoryAnchorDate(historyRangeStart.slice(0, 10) || getDateInputValue(new Date()))
    } else {
      const defaults = getHistoryRangeDefaults(historyAnchorDate, nextPeriod)
      setHistoryRangeStart(defaults.from)
      setHistoryRangeEnd(defaults.to)
    }
    setHistoryPeriod(nextPeriod)
  }

  useEffect(() => {
    if (isLoggerLocked) return
    if (requestedLoggerId > 0 && selectedDevice?.loggerId === requestedLoggerId) return
    if (selectedDevice && selectedDevice.loggerId !== selectedLoggerId) setSelectedLoggerId(selectedDevice.loggerId)
  }, [isLoggerLocked, requestedLoggerId, selectedDevice, selectedLoggerId])

  useEffect(() => {
    if (!selectedDevice) { setParameters([]); setParametersError(null); return undefined }
    let active = true
    setParameters([]); setParametersLoading(true); setParametersError(null)
    getIoTParameters(selectedDevice.loggerId)
      .then(result => { if (active) setParameters(result.parameters) })
      .catch(loadError => {
        if (!active) return
        console.error('Gagal memuat metadata parameter IoT:', loadError)
        setParameters([]); setParametersError('Metadata gagal dimuat; label menggunakan ParameterCode.')
      })
      .finally(() => { if (active) setParametersLoading(false) })
    return () => { active = false }
  }, [selectedDevice?.loggerId, refreshKey])

  useEffect(() => {
    if (!selectedDevice) {
      setHistory(null)
      setHistoryError(null)
      setHistoryLoading(false)
      return undefined
    }
    if (historyRangeError) {
      setHistory(null)
      setHistoryError(null)
      setHistoryLoading(false)
      return undefined
    }
    let active = true
    setHistoryLoading(true); setHistoryError(null)
    const historyQuery: IoTHistoryQuery = historyPeriod === 'day'
      ? { anchorDate: historyAnchorDate }
      : { from: historyRangeStart, to: historyRangeEnd }
    getIoTHistory(selectedDevice.loggerId, historyPeriod, historyQuery)
      .then(result => {
        if (!active) return
        setHistory(result)
        setLastUpdated(new Date().toISOString())
      })
      .catch(loadError => {
        if (!active) return
        console.error('Gagal memuat histori IoT:', loadError)
        setHistory(null); setHistoryError('Histori perangkat gagal dimuat. Silakan coba lagi.')
      })
      .finally(() => { if (active) setHistoryLoading(false) })
    return () => { active = false }
  }, [selectedDevice?.loggerId, historyPeriod, historyAnchorDate, historyRangeStart, historyRangeEnd, historyRangeError, refreshKey])

  const summary = useMemo(() => getLatestSummary(devices), [devices])
  const chartKeys = useMemo(() => getNumericKeys(history?.data ?? []), [history])
  const chartParameters = useMemo<ChartParameter[]>(() => chartKeys.map(parameterCode => ({ parameterCode, metadata: findParameterMetadata(parameters, parameterCode) })), [chartKeys, parameters])
  const parameterGroups = useMemo<ParameterGroup[]>(() => {
    const groups = new Map<string, ParameterGroup>()
    chartParameters.forEach(parameter => {
      const category = getParameterCategory(parameter.metadata)
      const existing = groups.get(category.code)
      if (existing) existing.parameters.push(parameter)
      else groups.set(category.code, { code: category.code, name: category.name, order: category.order, parameters: [parameter] })
    })
    return [...groups.values()].sort((left, right) => left.order - right.order || left.name.localeCompare(right.name, 'id-ID'))
  }, [chartParameters])
  const selectedFreshness = selectedDevice ? getFreshness(selectedDevice.latest, selectedDevice.intervalMenit) : 'none'
  const historyAggregation = history?.aggregation ?? getDefaultHistoryAggregation(historyPeriod)
  const todayDateInput = getDateInputValue(new Date())
  const nowDateTimeInput = getDateTimeInputValue(new Date())
  const requestedHistoryWindow = historyPeriod === 'day'
    ? `${formatDate(`${historyAnchorDate}T00:00:00`)} – ${formatDate(`${getNextDateInputValue(historyAnchorDate)}T00:00:00`)}`
    : historyRangeStart && historyRangeEnd
      ? `${formatDate(historyRangeStart)} – ${formatDate(historyRangeEnd)}`
      : 'Atur waktu mulai dan akhir'
  const historyWindow = history?.from && history?.to
    ? `${formatDate(history.from)} – ${formatDate(history.to)}`
    : requestedHistoryWindow
  const invalidCount = useMemo(() => history?.data.reduce((count, reading) => count + Object.values(reading.data).filter(value => getNumericValue(value) === null && value !== null).length, 0) ?? 0, [history])

  if (!permissionLoaded || !canRead) return <NoAccess loaded={permissionLoaded} />

  return (
    <main className={styles.page}>
      <header className={styles.header}>
        <div className={styles.headerCopy}>
          <p className={styles.eyebrow}><span className={styles.liveDot} /> {eyebrow}</p>
          <h1>{title}</h1>
          <p className={styles.subtitle}>{subtitle}</p>
          <div className={styles.headerMeta}><span>UPDATE {lastUpdated ? formatDate(lastUpdated, true) : '—'}</span><span>{selectedDevice ? `LOGGER ${selectedDevice.loggerCode}` : isLoggerLocked ? 'LOGGER TERKUNCI' : 'NO LOGGER SELECTED'}</span></div>
        </div>
        <div className={styles.headerActions}>
          {!isLoggerLocked && !sourceType && <label className={styles.compactControl}><span>SUMBER LOGGER</span><select value={sourceFilter} onChange={event => setSourceFilter(event.target.value as IoTSourceType | '')}><option value="">Semua sumber</option><option value="INTERNAL">Internal</option><option value="VENDOR">Vendor</option></select></label>}
          <label className={styles.compactControl}><span>AUTO REFRESH</span><select value={autoRefresh} onChange={event => setAutoRefresh(Number(event.target.value) as AutoRefreshSeconds)}><option value={0}>Off</option><option value={30}>30 detik</option><option value={60}>1 menit</option><option value={300}>5 menit</option></select></label>
          <button className={styles.refreshButton} type="button" onClick={() => setRefreshKey(key => key + 1)} disabled={loading || historyLoading}><span>↻</span>{loading || historyLoading ? 'Memuat...' : 'Refresh data'}</button>
        </div>
      </header>

      {error && <section className={styles.error} role="alert"><span>{error}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></section>}

      {!isLoggerLocked && <section className={styles.statusPanel} aria-label="Ringkasan kondisi logger">
        <div className={styles.statusPanelHeading}>
          <div>
            <span className={styles.panelEyebrow}>DEVICE HEALTH</span>
            <h2>Kondisi logger hari ini</h2>
            <p>Ringkasan freshness berdasarkan interval sampling masing-masing perangkat.</p>
          </div>
          <span className={styles.healthBadge}><i /> {loading ? 'Memuat...' : `${summary.fresh} logger terkini`}</span>
        </div>
        <div className={styles.statusPanelBody}>
          <div className={styles.statusTotals}>
            <div><span>Total logger</span><strong>{loading ? '—' : devices.length}</strong><small>Vendor + Internal</small></div>
            <div><span>Data terkini</span><strong>{loading ? '—' : summary.fresh}</strong><small>Dalam interval</small></div>
            <div><span>Perlu perhatian</span><strong>{loading ? '—' : summary.late + summary.none + summary.unknown}</strong><small>Terlambat / kosong</small></div>
          </div>
          <div className={styles.statusBars}>
            <StatusBar label="TERKINI" value={summary.fresh} total={devices.length} tone="good" />
            <StatusBar label="TERLAMBAT" value={summary.late} total={devices.length} tone="warning" />
            <StatusBar label="BELUM ADA DATA" value={summary.none} total={devices.length} tone="muted" />
            <StatusBar label="TIDAK DIKETAHUI" value={summary.unknown} total={devices.length} tone="unknown" />
          </div>
        </div>
      </section>}

      {!isLoggerLocked && <section className={styles.workspace}>
        <section className={`${styles.panel} ${styles.devicePanel}`}>
          <PanelHeading eyebrow="LOGGER INVENTORY" title="Perangkat terpantau" subtitle={`${filteredDevices.length} dari ${devices.length} logger ditampilkan`} />
          <div className={styles.deviceToolbar}><label className={styles.searchControl}><span>Cari perangkat</span><input value={deviceFilter} onChange={event => setDeviceFilter(event.target.value)} placeholder="Nama, kode, atau lokasi..." /></label></div>
          {loading ? <StateMessage>Memuat daftar logger dan pembacaan terakhir...</StateMessage> : filteredDevices.length === 0 ? <StateMessage>{error || 'Tidak ada perangkat yang cocok dengan filter.'}</StateMessage> : <div className={styles.deviceList}>{filteredDevices.map(device => <DeviceRow key={device.loggerId} device={device} selected={device.loggerId === selectedDevice?.loggerId} onSelect={() => setSelectedLoggerId(device.loggerId)} />)}</div>}
        </section>

        <section className={`${styles.panel} ${styles.qualityPanel}`}>
          <PanelHeading eyebrow="DATA QUALITY" title="Kondisi pengukuran" subtitle={selectedDevice?.namaLogger || 'Pilih logger'} />
          <div className={styles.qualityBody}>
            <QualityRow label="Status logger" value={selectedDevice ? freshnessLabel(selectedFreshness) : '—'} tone={selectedFreshness} />
            <QualityRow label="Pembacaan terakhir" value={selectedDevice?.latest ? formatDate(selectedDevice.latest.ts, true) : 'Belum tersedia'} tone={selectedDevice?.latest ? 'fresh' : 'none'} />
            <QualityRow label="Interval sampling" value={selectedDevice ? `${selectedDevice.intervalMenit || '—'} menit` : '—'} tone="neutral" />
            <QualityRow label="Metadata parameter" value={parametersLoading ? 'Memuat...' : parametersError ? 'Nama umum' : `${parameters.length} metadata`} tone={parametersError ? 'late' : 'fresh'} />
            <QualityRow label="Nilai non-numerik" value={history ? `${invalidCount} terdeteksi` : '—'} tone={invalidCount > 0 ? 'warning' : 'fresh'} />
          </div>
          <div className={styles.qualityNote}>Threshold belum dikonfigurasi oleh API. Dashboard tidak menebak batas normal atau bahaya.</div>
        </section>
      </section>}

      <section className={styles.categoryBoard} aria-label="Grafik histori parameter">
        <div className={styles.categoryBoardHeading}>
          <div><span className={styles.panelEyebrow}>HISTORICAL TELEMETRY</span><h2>Grafik histori per parameter</h2><p>{selectedDevice ? `${selectedDevice.namaLogger} · ${history?.total ?? 0} pembacaan terakhir` : isLoggerLocked ? 'Logger dari detail sensor tidak ditemukan.' : 'Pilih logger untuk melihat histori pembacaan.'}</p></div>
          <div className={styles.historyControls}>
            <div className={styles.historyRangeSummary}>
              <span>RENTANG AKTIF</span>
              <strong>{historyWindow}</strong>
              <small>{history ? `${history.total} titik hasil` : historyRangeError || 'Sesuaikan rentang untuk memuat histori.'}</small>
            </div>
            <div className={styles.historyControlGrid}>
              {historyPeriod === 'day' ? (
                <label className={styles.historyDateControl}>
                  <span>TANGGAL</span>
                  <input type="date" value={historyAnchorDate} max={todayDateInput} onChange={event => setHistoryAnchorDate(event.target.value || todayDateInput)} />
                </label>
              ) : (
                <>
                  <label className={styles.historyDateControl}>
                    <span>MULAI</span>
                    <input type="datetime-local" value={historyRangeStart} max={nowDateTimeInput} onChange={event => setHistoryRangeStart(event.target.value)} />
                  </label>
                  <label className={styles.historyDateControl}>
                    <span>AKHIR</span>
                    <input type="datetime-local" value={historyRangeEnd} max={nowDateTimeInput} onChange={event => setHistoryRangeEnd(event.target.value)} />
                  </label>
                </>
              )}
              <label className={styles.historyPeriodControl}>
                <span>PERIODE HISTORY</span>
                <select value={historyPeriod} onChange={event => handleHistoryPeriodChange(event.target.value as IoTHistoryPeriod)}>{HISTORY_PERIOD_OPTIONS.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select>
              </label>
            </div>
            {historyRangeError && <small className={styles.historyValidation} role="alert">{historyRangeError}</small>}
            <span className={styles.dailyBadge}>{history ? `Agregasi per ${getAggregationLabel(historyAggregation)}` : `Agregasi ${getAggregationLabel(historyAggregation)}`}</span>
          </div>
        </div>
        {historyLoading ? <div className={styles.panel}><StateMessage>Memuat histori pembacaan...</StateMessage></div> : historyError ? <div className={styles.panel}><StateMessage>{historyError}</StateMessage></div> : parameterGroups.length === 0 ? <div className={styles.panel}><StateMessage>{selectedDevice ? (parametersLoading ? 'Memuat metadata parameter...' : 'Belum ada nilai numerik untuk grafik histori.') : isLoggerLocked ? 'Logger dari tautan histori tidak ditemukan.' : 'Pilih logger dari daftar perangkat.'}</StateMessage></div> : parameterGroups.map((group, index) => <CategorySection key={group.code} group={group} index={index} history={history?.data ?? []} latest={selectedDevice?.latest ?? null} loggerCode={selectedDevice?.loggerCode} loggerName={selectedDevice?.namaLogger} aggregation={historyAggregation} />)}
      </section>
    </main>
  )
}

function StatusBar({ label, value, total, tone }: { label: string; value: number; total: number; tone: string }) {
  const percentage = total > 0 ? Math.min(100, Math.round((value / total) * 100)) : 0
  return <div className={styles.statusBar}><span className={styles.statusBarName}>{label}</span><div className={styles.statusTrack}><span className={`${styles.statusFill} ${styles[`status-${tone}`]}`} style={{ width: `${percentage}%` }} /></div><strong className={styles.statusBarValue}>{value}</strong></div>
}

function PanelHeading({ eyebrow, title, subtitle }: { eyebrow: string; title: string; subtitle: string }) {
  return <div className={styles.panelHeading}><div><span className={styles.panelEyebrow}>{eyebrow}</span><h2>{title}</h2><p>{subtitle}</p></div></div>
}

function StateMessage({ children, className = '' }: { children: ReactNode; className?: string }) { return <div className={`${styles.state} ${className}`.trim()}>{children}</div> }

function DeviceRow({ device, selected, onSelect }: { device: IoTDeviceWithLatest; selected: boolean; onSelect: () => void }) {
  const freshness = device.readingError ? 'unknown' : getFreshness(device.latest, device.intervalMenit)
  return <button type="button" className={`${styles.deviceRow} ${selected ? styles.selectedDevice : ''}`} onClick={onSelect} aria-pressed={selected}><span className={styles.deviceMain}><strong>{device.namaLogger}</strong><small>{device.loggerCode} · {device.lokasi || 'Lokasi belum diatur'}</small></span><span className={`${styles.status} ${styles[freshness]}`}><i aria-hidden="true" />{freshnessLabel(freshness)}<small>{device.latest ? formatDate(device.latest.ts) : device.readingError ? 'Gagal mengambil data' : 'Belum tersedia'}</small></span><span className={styles.deviceMeta}><small>{device.sourceType}</small><small>{device.intervalMenit || '—'} min</small></span></button>
}

function QualityRow({ label, value, tone }: { label: string; value: string; tone: string }) { return <div className={styles.qualityRow}><span>{label}</span><strong className={styles[`quality-${tone}`]}>{value}</strong></div> }

function CategorySection({ group, index, history, latest, loggerCode, loggerName, aggregation }: { group: ParameterGroup; index: number; history: IoTReading[]; latest: IoTReading | null; loggerCode: string | null | undefined; loggerName: string | null | undefined; aggregation?: IoTHistoryAggregation }) {
  const color = getCategoryColor(index)
  return <section className={styles.categorySection} style={{ '--category-color': color } as CSSProperties}><div className={styles.categoryHeading}><div><span className={styles.categoryMarker} /><span className={styles.panelEyebrow}>CATEGORY {String(index + 1).padStart(2, '0')}</span><h2>{group.name}</h2></div><span className={styles.categoryCount}>{group.parameters.length} parameter</span></div><div className={styles.sensorGrid}>{group.parameters.map((parameter, parameterIndex) => <SensorCard key={parameter.parameterCode} parameter={parameter} points={getChartPoints(history, parameter.parameterCode, loggerCode, loggerName)} latest={latest} loggerCode={loggerCode} loggerName={loggerName} aggregation={aggregation} fullWidth={group.parameters.length % 2 === 1 && parameterIndex === group.parameters.length - 1} />)}</div></section>
}

function SensorCard({ parameter, points, latest, loggerCode, loggerName, aggregation = 'hour', fullWidth }: { parameter: ChartParameter; points: ChartPoint[]; latest: IoTReading | null; loggerCode: string | null | undefined; loggerName: string | null | undefined; aggregation?: IoTHistoryAggregation; fullWidth: boolean }) {
  const label = getParameterLabel(parameter.metadata)
  const parameterName = parameter.metadata?.namaParameter?.trim() || 'Parameter sensor'
  const unit = parameter.metadata?.satuan?.trim() || 'nilai'
  const kind = getVisualizationKind(parameter)
  const stats = getChartStats(points)
  const latestValue = getNumericValue(adjustTmakValue(loggerCode, parameter.parameterCode, latest?.data[parameter.parameterCode], loggerName)) ?? points[points.length - 1]?.value ?? null
  const chartLatestValue = points[points.length - 1]?.value ?? latestValue
  const visualizationLabel = kind === 'rainfall'
    ? 'AKUMULASI PER INTERVAL'
    : kind === 'direction'
      ? 'PERSEBARAN ARAH'
      : kind === 'level'
        ? 'LEVEL DALAM RENTANG'
        : kind === 'gauge'
          ? 'GAUGE NILAI TERAKHIR'
          : kind === 'heat'
            ? 'INTENSITAS PERIODE'
            : 'TREN PEMBACAAN'
  const summary = kind === 'direction'
    ? `Arah ${getDirectionLabel(chartLatestValue)}`
    : stats.min === null || stats.max === null
      ? label
      : `Min ${formatSensorValue(stats.min)} · Maks ${formatSensorValue(stats.max)}`
  const chart = points.length === 0
    ? <span className={styles.noDailyData}>Belum ada pembacaan</span>
    : kind === 'rainfall'
      ? <RainfallChart points={points} parameterLabel={parameterName} unit={unit} aggregation={aggregation} />
      : kind === 'direction'
        ? <DirectionChart points={points} />
        : kind === 'level'
          ? <LevelVisual value={chartLatestValue} stats={stats} unit={unit} />
          : kind === 'gauge'
            ? <GaugeVisual value={chartLatestValue} stats={stats} unit={unit} />
            : kind === 'heat'
              ? <HeatVisual points={points} stats={stats} unit={unit} aggregation={aggregation} />
              : <TrendChart points={points} parameterLabel={parameterName} unit={unit} aggregation={aggregation} />

  return <article className={`${styles.sensorCard} ${fullWidth ? styles.sensorCardFull : ''}`}><span className={styles.sensorCardTop}><strong>{parameterName}</strong><small>{visualizationLabel}</small></span><span className={styles.sensorValue}><strong>{formatSensorValue(latestValue)}</strong><small>{unit}</small></span><span className={`${styles.sensorCardChart} ${styles[`chart-${kind}`]}`}>{chart}</span><span className={styles.sensorCardFooter}><span>{summary}</span><span>{points.length} titik</span></span></article>
}

function TrendChart({ points, parameterLabel, unit, aggregation }: { points: ChartPoint[]; parameterLabel: string; unit: string; aggregation: IoTHistoryAggregation }) {
  return <ResponsiveContainer width="100%" height="100%"><AreaChart data={points} margin={{ top: 7, right: 8, bottom: 2, left: 2 }}><CartesianGrid stroke="#dceeee" strokeDasharray="3 5" vertical={false} /><XAxis dataKey="ts" axisLine={false} tickLine={false} tick={{ fill: '#78949d', fontSize: 8.5 }} tickFormatter={value => formatAxisTime(value, aggregation)} minTickGap={34} interval="preserveStartEnd" /><YAxis hide domain={['auto', 'auto']} /><Tooltip content={<HistoryTooltip parameterLabel={parameterLabel} unit={unit} aggregation={aggregation} />} /><Area type="monotone" dataKey="value" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" fill="currentColor" fillOpacity={0.1} activeDot={{ r: 5, fill: '#fff', stroke: 'currentColor', strokeWidth: 2 }} isAnimationActive={false} connectNulls={false} /></AreaChart></ResponsiveContainer>
}

function RainfallChart({ points, parameterLabel, unit, aggregation }: { points: ChartPoint[]; parameterLabel: string; unit: string; aggregation: IoTHistoryAggregation }) {
  return <ResponsiveContainer width="100%" height="100%"><BarChart data={points} margin={{ top: 7, right: 8, bottom: 2, left: 2 }}><CartesianGrid stroke="#dceeee" strokeDasharray="3 5" vertical={false} /><XAxis dataKey="ts" axisLine={false} tickLine={false} tick={{ fill: '#78949d', fontSize: 8.5 }} tickFormatter={value => formatAxisTime(value, aggregation)} minTickGap={34} interval="preserveStartEnd" /><YAxis hide domain={[0, 'auto']} /><Tooltip content={<HistoryTooltip parameterLabel={parameterLabel} unit={unit} aggregation={aggregation} />} /><Bar dataKey="value" fill="currentColor" radius={[4, 4, 0, 0]} maxBarSize={18} isAnimationActive={false} /></BarChart></ResponsiveContainer>
}

function DirectionChart({ points }: { points: ChartPoint[] }) {
  const latest = points[points.length - 1]?.value ?? null
  const direction = getDirectionLabel(latest)
  const plottedPoints = points.map((point, index) => {
    const radians = (point.value * Math.PI) / 180
    const radius = 27 + ((index * 13) % 14)
    return {
      point,
      left: 50 + Math.sin(radians) * radius,
      top: 50 - Math.cos(radians) * radius,
      isLatest: index === points.length - 1,
    }
  })
  return <div className={styles.directionChart} aria-label={`Persebaran arah angin, arah terakhir ${formatSensorValue(latest)} derajat, ${direction}`}><div className={styles.directionScatter}><span className={`${styles.compassLabel} ${styles.compassNorth}`}>U</span><span className={`${styles.compassLabel} ${styles.compassEast}`}>T</span><span className={`${styles.compassLabel} ${styles.compassSouth}`}>S</span><span className={`${styles.compassLabel} ${styles.compassWest}`}>B</span><span className={styles.compassRing} />{plottedPoints.map(({ point, left, top, isLatest }, index) => <span key={`${point.ts}-${index}`} className={`${styles.directionPoint} ${isLatest ? styles.directionPointLatest : ''}`} style={{ left: `${left}%`, top: `${top}%` }} title={`${formatSensorValue(point.value)}° · ${getDirectionLabel(point.value)}`} />)}<span className={styles.compassCenter} /></div><div className={styles.directionReadout}><span>ARAH TERAKHIR</span><strong>{formatSensorValue(latest)}<small>°</small></strong><em>{direction}</em><small className={styles.directionHint}>Titik menunjukkan persebaran histori</small></div></div>
}

function LevelVisual({ value, stats, unit }: { value: number | null; stats: ChartStats; unit: string }) {
  const min = stats.min ?? value ?? 0
  const max = stats.max ?? value ?? 0
  const range = max - min
  const percentage = value === null ? 0 : range > 0 ? Math.min(100, Math.max(0, ((value - min) / range) * 100)) : 50
  return <div className={styles.levelVisual} aria-label={`Level terakhir ${formatSensorValue(value)} ${unit}`}><div className={styles.levelScale}><span>{formatSensorValue(max)}</span><div className={styles.levelTank}><span className={styles.levelFill} style={{ height: `${percentage}%` }} /><span className={styles.levelTankLine} /></div><span>{formatSensorValue(min)}</span></div><div className={styles.levelReadout}><span>POSISI TERKINI</span><strong>{formatSensorValue(value)}<small>{unit}</small></strong><em>Rentang histori {formatSensorValue(min)} – {formatSensorValue(max)}</em></div></div>
}

function GaugeVisual({ value, stats, unit }: { value: number | null; stats: ChartStats; unit: string }) {
  const maximum = Math.max(stats.max ?? 0, value ?? 0, 1)
  const percentage = value === null ? 0 : Math.min(100, Math.max(0, (value / maximum) * 100))
  return <div className={styles.gaugeVisual} aria-label={`Nilai terakhir ${formatSensorValue(value)} ${unit}`}><div className={styles.gaugeRing} style={{ background: `conic-gradient(currentColor 0 ${percentage}%, #e3eff1 ${percentage}% 100%)` }}><div className={styles.gaugeHole}><strong>{formatSensorValue(value)}</strong><small>{unit}</small></div></div><div className={styles.gaugeReadout}><span>SKALA HISTORI</span><strong>Maks {formatSensorValue(maximum)}</strong><em>Posisi nilai terakhir</em></div></div>
}

function HeatVisual({ points, stats, unit, aggregation }: { points: ChartPoint[]; stats: ChartStats; unit: string; aggregation: IoTHistoryAggregation }) {
  const visiblePoints = points.slice(-48)
  const range = (stats.max ?? 0) - (stats.min ?? 0)
  return <div className={styles.heatVisual} aria-label={`Intensitas ${visiblePoints.length} pembacaan terakhir`}><div className={styles.heatStrip}>{visiblePoints.map((point, index) => { const ratio = range > 0 ? ((point.value - (stats.min ?? 0)) / range) : 1; const hue = 205 - ratio * 155; return <span key={`${point.ts}-${index}`} className={styles.heatCell} style={{ background: `hsl(${hue} 78% 56%)`, opacity: 0.38 + ratio * 0.62 }} title={`${formatSensorValue(point.value)} ${unit} · ${formatChartTime(point.ts, aggregation)}`} /> })}</div><div className={styles.heatLegend}><span>Rendah {formatSensorValue(stats.min)}</span><i /><span>Tinggi {formatSensorValue(stats.max)}</span></div></div>
}

function HistoryTooltip({ active, payload, label, parameterLabel, unit, aggregation = 'hour' }: TooltipProps<number, string> & { parameterLabel: string; unit: string; aggregation?: IoTHistoryAggregation }) {
  if (!active || !payload?.length) return null
  const value = payload[0]?.value
  const tooltipTime = typeof label === 'number' || typeof label === 'string' ? formatChartTime(label, aggregation) : '-'
  return <div className={styles.chartTooltip}><span className={styles.chartTooltipTime}>{tooltipTime}</span><strong>{parameterLabel}</strong><span className={styles.chartTooltipValue}>{formatSensorValue(value)} <small>{unit}</small></span></div>
}
