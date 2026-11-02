import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTHistory, getIoTMonitoring } from '../api/iotApi'
import { formatMonitoringDate, monitoringStatusLabel, monitoringStatusTone } from '../lib/monitoring'
import type { IoTHistoryResponse, IoTMonitoringDevice, IoTMonitoringResponse } from '../types/iot'
import styles from './IoTRainCalendarPage.module.css'

const MENU_CODE = 'WAD_ECS_STASIUN'
const MONTHS = [
  'Januari', 'Februari', 'Maret', 'April', 'Mei', 'Juni',
  'Juli', 'Agustus', 'September', 'Oktober', 'November', 'Desember',
]

function getYear(value: string | null) {
  const year = Number(value)
  return Number.isInteger(year) && year >= 2000 && year <= 2100
    ? year
    : new Date().getFullYear()
}

function getNumericValue(value: unknown) {
  if (typeof value !== 'number' && typeof value !== 'string') return null
  if (typeof value === 'string' && value.trim() === '') return null
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function normalizeParameterCode(value: string) {
  return value.split('#', 1)[0].replace(/[\s_-]/g, '').toLocaleUpperCase('id-ID')
}

function getRainReading(data: Record<string, number | string | null>) {
  const entries = Object.entries(data)
    .map(([key, value]) => ({ key, value: getNumericValue(value) }))
    .filter((entry): entry is { key: string; value: number } => entry.value !== null)

  const preferred = entries.find(entry => {
    const code = normalizeParameterCode(entry.key)
    return code === 'RAINTIP'
      || code === 'CURAHHUJAN'
      || code === 'HUJAN'
      || code.includes('CURAH')
      || code.includes('RAIN')
  })

  return preferred ?? null
}

function getDateKey(value: string | null | undefined) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function daysInMonth(year: number, monthIndex: number) {
  return new Date(year, monthIndex + 1, 0).getDate()
}

function formatNumber(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return '-'
  return new Intl.NumberFormat('id-ID', { maximumFractionDigits: 2 }).format(value)
}

function getRainCellStyle(value: number | undefined) {
  if (value === undefined) return { background: '#f1f5f4', color: '#80918b' }
  if (value < 0.5) return { background: '#ffffff', color: '#315448' }
  if (value <= 20) return { background: '#c8f0dc', color: '#18563e' }
  if (value <= 50) return { background: '#f8e7a9', color: '#72570a' }
  if (value <= 100) return { background: '#f9c58e', color: '#74400f' }
  if (value <= 150) return { background: '#f3a1a1', color: '#7b2020' }
  return { background: '#cdb0ec', color: '#4c2677' }
}

function getDisplayName(device: IoTMonitoringDevice) {
  return device.namaLogger?.trim() || device.loggerCode?.trim() || `Logger ${device.loggerId}`
}

interface IoTRainCalendarPageProps {
  permissionCode?: string
}

export default function IoTRainCalendarPage({ permissionCode = MENU_CODE }: IoTRainCalendarPageProps) {
  const { permission, loaded } = useMenuPermission(permissionCode)
  const [searchParams, setSearchParams] = useSearchParams()
  const requestedLoggerId = Number(searchParams.get('loggerId'))
  const [year, setYear] = useState(() => getYear(searchParams.get('year')))
  const [monitoring, setMonitoring] = useState<IoTMonitoringResponse | null>(null)
  const [history, setHistory] = useState<IoTHistoryResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [historyLoading, setHistoryLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [historyError, setHistoryError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  const canRead = !!permission?.canRead

  const loadMonitoring = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setMonitoring(await getIoTMonitoring())
    } catch (loadError) {
      console.error('Gagal memuat logger untuk kalender hujan:', loadError)
      setMonitoring(null)
      setError('Daftar logger gagal dimuat. Silakan coba lagi.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (loaded && canRead) void loadMonitoring()
  }, [canRead, loaded, loadMonitoring, refreshKey])

  const devices = monitoring?.devices ?? []
  const selectedDevice = useMemo(() => {
    const requested = devices.find(device => device.loggerId === requestedLoggerId)
    if (requested) return requested
    return devices.find(device => getRainReading(device.data) !== null) ?? devices[0] ?? null
  }, [devices, requestedLoggerId])

  const selectLogger = (loggerId: number) => {
    setSearchParams({ loggerId: String(loggerId), year: String(year) })
  }

  useEffect(() => {
    if (!selectedDevice) {
      setHistory(null)
      setHistoryError(null)
      return undefined
    }

    let active = true
    setHistoryLoading(true)
    setHistoryError(null)

    getIoTHistory(selectedDevice.loggerId, 'month', {
      from: `${year}-01-01T00:00`,
      to: `${year + 1}-01-01T00:00`,
      aggregationMode: 'sum',
    })
      .then(result => {
        if (active) setHistory(result)
      })
      .catch(loadError => {
        if (!active) return
        console.error('Gagal memuat kalender hujan IoT:', loadError)
        setHistory(null)
        setHistoryError('Histori curah hujan logger ini gagal dimuat.')
      })
      .finally(() => {
        if (active) setHistoryLoading(false)
      })

    return () => { active = false }
  }, [selectedDevice?.loggerId, year, refreshKey])

  const calendar = useMemo(() => {
    const values = new Map<string, number>()
    history?.data.forEach(reading => {
      const rain = getRainReading(reading.data)
      const dateKey = getDateKey(reading.ts)
      if (!rain || !dateKey) return
      values.set(dateKey, rain.value)
    })
    return values
  }, [history])

  const summary = useMemo(() => {
    const values = [...calendar.values()]
    return {
      rainyDays: values.filter(value => value >= 0.5).length,
      total: values.reduce((sum, value) => sum + value, 0),
      maximum: values.length ? Math.max(...values) : null,
    }
  }, [calendar])

  const rainParameter = useMemo(() => {
    const latest = selectedDevice ? getRainReading(selectedDevice.data) : null
    if (latest) return normalizeParameterCode(latest.key)
    for (const reading of history?.data ?? []) {
      const value = getRainReading(reading.data)
      if (value) return normalizeParameterCode(value.key)
    }
    return 'PARAMETER HUJAN'
  }, [history, selectedDevice])

  const yearOptions = useMemo(() => {
    const currentYear = new Date().getFullYear()
    return Array.from({ length: 6 }, (_, index) => currentYear - index)
  }, [])

  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  return (
    <main className={styles.page}>
      <header className={styles.hero}>
        <div>
          <p className={styles.eyebrow}><span className={styles.liveDot} /> WAD / LIVE RAINFALL</p>
          <h1>Kalender Curah Hujan</h1>
          <p className={styles.subtitle}>Baca pola curah hujan harian dari logger terpilih tanpa memuat seluruh histori sensor sekaligus.</p>
          <div className={styles.heroMeta}><span>{devices.length} LOGGER TERSEDIA</span><span>{selectedDevice ? `LOGGER ${selectedDevice.loggerCode}` : 'NO LOGGER SELECTED'}</span><span>AGREGASI HARIAN</span></div>
        </div>
        <button type="button" className={styles.refreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading || historyLoading}>
          {loading || historyLoading ? 'Memuat...' : 'Refresh data'}
        </button>
      </header>

      {error && <section className={styles.alert} role="alert"><span>{error}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></section>}

      <section className={styles.controlPanel} aria-label="Filter kalender curah hujan">
        <div className={styles.controlCopy}>
          <span className={styles.panelEyebrow}>FILTER SENSOR</span>
          <h2>Pilih logger dan tahun</h2>
          <p>Kalender hanya mengambil histori dari logger yang dipilih.</p>
        </div>
        <label className={styles.control}>
          <span>LOGGER SENSOR</span>
          <select value={selectedDevice?.loggerId ?? ''} onChange={event => selectLogger(Number(event.target.value))} disabled={loading || devices.length === 0}>
            {devices.length === 0 && <option value="">Belum ada logger</option>}
            {devices.map(device => <option key={device.loggerId} value={device.loggerId}>{getDisplayName(device)} · {device.loggerCode}</option>)}
          </select>
        </label>
        <label className={styles.control}>
          <span>TAHUN</span>
          <select value={year} onChange={event => { const nextYear = Number(event.target.value); setYear(nextYear); setSearchParams({ loggerId: String(selectedDevice?.loggerId ?? ''), year: String(nextYear) }) }}>
            {yearOptions.map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </label>
      </section>

      <section className={styles.selectedBar} aria-label="Logger kalender terpilih">
        <div>
          <span className={styles.panelEyebrow}>LOGGER TERPILIH</span>
          <strong>{selectedDevice ? getDisplayName(selectedDevice) : 'Belum ada logger'}</strong>
          <small>{selectedDevice?.loggerCode ?? '-'} · {selectedDevice?.sourceType ?? '-'} · parameter {rainParameter}</small>
        </div>
        {selectedDevice && <span className={`${styles.statusBadge} ${styles[`status-${monitoringStatusTone(selectedDevice.status)}`]}`}><i />{monitoringStatusLabel(selectedDevice.status)}</span>}
      </section>

      <section className={styles.summaryGrid} aria-label="Ringkasan curah hujan">
        <SummaryCard label="Hari dengan hujan" value={summary.rainyDays} hint={`Ambang ≥ 0,5 mm pada ${year}`} tone="rain" />
        <SummaryCard label="Akumulasi terbaca" value={formatNumber(summary.total)} hint="Nilai parameter hujan (mm)" tone="total" />
        <SummaryCard label="Pembacaan tertinggi" value={formatNumber(summary.maximum)} hint="Nilai harian tertinggi (mm)" tone="peak" />
        <SummaryCard label="Data harian" value={history?.total ?? 0} hint={history?.aggregationMode === 'sum' ? 'Total interval per hari dari API IoT' : 'Menunggu snapshot'} tone="days" />
      </section>

      {historyError && <section className={styles.alert} role="alert"><span>{historyError}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></section>}

      <section className={styles.calendarPanel} aria-label="Kalender pembacaan curah hujan">
        <div className={styles.panelHeader}>
          <div><span className={styles.panelEyebrow}>RAIN SENSOR HISTORY</span><h2>Kalender pembacaan harian</h2><p>{rainParameter} · data tahun {year}</p></div>
          <span className={styles.sourceBadge}>{selectedDevice ? `${getDisplayName(selectedDevice)} · ${year}` : 'Pilih logger'}</span>
        </div>
        {historyLoading ? <div className={styles.state}><strong>Memuat histori hujan...</strong><span>API IoT sedang merangkum pembacaan harian logger terpilih.</span></div> : selectedDevice && calendar.size === 0 ? <div className={styles.state}><strong>Belum ada pembacaan hujan untuk filter ini</strong><span>Logger mungkin tidak memiliki parameter curah hujan atau belum memiliki data pada tahun {year}.</span></div> : <div className={styles.tableWrap}>
          <table className={styles.calendarTable}>
            <thead><tr><th>Bulan / Tanggal</th>{Array.from({ length: 31 }, (_, index) => <th key={index}>{String(index + 1).padStart(2, '0')}</th>)}</tr></thead>
            <tbody>{MONTHS.map((month, monthIndex) => <tr key={month}><th>{month}</th>{Array.from({ length: 31 }, (_, dayIndex) => {
              const day = dayIndex + 1
              if (day > daysInMonth(year, monthIndex)) return <td key={day} className={styles.invalidDay} />
              const key = `${year}-${String(monthIndex + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
              const value = calendar.get(key)
              const cell = getRainCellStyle(value)
              return <td key={day} style={{ background: cell.background, color: cell.color }} title={value === undefined ? `${key}: tidak ada data` : `${key}: ${formatNumber(value)} mm`}>{value === undefined ? '–' : formatNumber(value)}</td>
            })}</tr>)}</tbody>
          </table>
        </div>}
      </section>

      <div className={styles.legend} aria-label="Legenda intensitas hujan">
        <strong>Intensitas:</strong><Legend color="#f1f5f4" label="Tidak ada data" /><Legend color="#ffffff" label="&lt; 0,5 mm" /><Legend color="#c8f0dc" label="0,5–20 mm" /><Legend color="#f8e7a9" label="21–50 mm" /><Legend color="#f9c58e" label="51–100 mm" /><Legend color="#f3a1a1" label="101–150 mm" /><Legend color="#cdb0ec" label="&gt; 150 mm" />
      </div>

      {selectedDevice?.lastReceivedAt && <p className={styles.freshness}>Snapshot logger terakhir diterima {formatMonitoringDate(selectedDevice.lastReceivedAt)}. Data kalender dipanggil hanya ketika logger atau tahun berubah.</p>}
    </main>
  )
}

function SummaryCard({ label, value, hint, tone }: { label: string; value: number | string; hint: string; tone: string }) {
  return <article className={`${styles.summaryCard} ${styles[`tone-${tone}`]}`}><span>{label}</span><strong>{value}</strong><small>{hint}</small></article>
}

function Legend({ color, label }: { color: string; label: string }) {
  return <span className={styles.legendItem}><i style={{ background: color }} />{label}</span>
}
