import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTHistory, getIoTMonitoring } from '../api/iotApi'
import { formatMonitoringDate, monitoringStatusLabel, monitoringStatusTone } from '../lib/monitoring'
import { summarizeCalendarRainfall } from '../lib/rainCalendar'
import {
  CALENDAR_KINDS, getCalendarHistoryQuery, getCalendarKind, getCalendarSelection,
  getCalendarStatistics, getLevelCellStyle, getSensorCalendar,
} from '../lib/sensorCalendar'
import type { CalendarKind } from '../lib/sensorCalendar'
import { classifyTmak, getTmakCellStyle, TMAK_CATEGORIES } from '../lib/tmakThresholds'
import { classifyTmat, getTmatCellStyle, TMAT_CATEGORIES } from '../lib/tmatThresholds'
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
  const year = getYear(searchParams.get('year'))
  const kind = getCalendarKind(searchParams.get('kind'))
  const definition = CALENDAR_KINDS[kind]
  const isRain = kind === 'rain'
  const [monitoring, setMonitoring] = useState<IoTMonitoringResponse | null>(null)
  const [history, setHistory] = useState<{ key: string; response: IoTHistoryResponse } | null>(null)
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
      console.error('Gagal memuat logger untuk kalender sensor:', loadError)
      setMonitoring(null)
      setError('Daftar logger gagal dimuat. Silakan coba lagi.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (loaded && canRead) void loadMonitoring()
  }, [canRead, loaded, loadMonitoring, refreshKey])

  const { devices, selectedDevice } = useMemo(
    () => getCalendarSelection(monitoring?.devices ?? [], kind, requestedLoggerId),
    [monitoring, kind, requestedLoggerId],
  )
  const requestKey = `${selectedDevice?.loggerId ?? 0}:${kind}:${year}`
  const visibleHistory = history?.key === requestKey ? history.response : null

  const updateFilters = (nextKind: CalendarKind, nextYear: number, loggerId = selectedDevice?.loggerId ?? 0) => {
    const next = getCalendarSelection(monitoring?.devices ?? [], nextKind, loggerId)
    const params = new URLSearchParams({ kind: nextKind, year: String(nextYear) })
    if (next.selectedDevice) params.set('loggerId', String(next.selectedDevice.loggerId))
    setSearchParams(params)
  }

  useEffect(() => {
    if (!selectedDevice || requestedLoggerId === selectedDevice.loggerId) return
    const params = new URLSearchParams(searchParams)
    params.set('loggerId', String(selectedDevice.loggerId))
    setSearchParams(params, { replace: true })
  }, [selectedDevice?.loggerId, requestedLoggerId, searchParams, setSearchParams])

  useEffect(() => {
    if (!selectedDevice) {
      setHistory(null)
      setHistoryError(null)
      setHistoryLoading(false)
      return undefined
    }

    let active = true
    setHistoryLoading(true)
    setHistoryError(null)

    getIoTHistory(selectedDevice.loggerId, 'month', getCalendarHistoryQuery(kind, year))
      .then(result => {
        if (active) setHistory({ key: requestKey, response: result })
      })
      .catch(loadError => {
        if (!active) return
        console.error('Gagal memuat kalender data sensor:', loadError)
        setHistory(null)
        setHistoryError('Histori data sensor gagal dimuat. Silakan coba lagi.')
      })
      .finally(() => {
        if (active) setHistoryLoading(false)
      })

    return () => { active = false }
  }, [selectedDevice?.loggerId, kind, year, refreshKey])

  const calendar = useMemo(
    () => getSensorCalendar(visibleHistory, selectedDevice, kind, year),
    [visibleHistory, selectedDevice, kind, year],
  )

  const summary = useMemo(() => summarizeCalendarRainfall(calendar.values()), [calendar])
  const statistics = useMemo(() => getCalendarStatistics(calendar.values()), [calendar])

  const yearOptions = useMemo(() => {
    const currentYear = new Date().getFullYear()
    return Array.from({ length: 6 }, (_, index) => currentYear - index)
  }, [])

  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  return (
    <main className={styles.page}>
      <header className={styles.hero}>
        <div>
          <p className={styles.eyebrow}><span className={styles.liveDot} /> WAD / SENSOR CALENDAR</p>
          <h1>Kalender Data Sensor</h1>
          <p className={styles.subtitle}>Telusuri curah hujan, tinggi muka air kanal, dan tinggi muka air tanah per sensor dan tahun.</p>
          <div className={styles.heroMeta}><span>{devices.length} SENSOR SESUAI</span><span>{definition.label.toUpperCase()}</span><span>{isRain ? 'AKUMULASI HARIAN' : 'RATA-RATA HARIAN'}</span></div>
        </div>
        <button type="button" className={styles.refreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading || historyLoading}>
          {loading || historyLoading ? 'Memuat...' : 'Refresh data'}
        </button>
      </header>

      {error && <section className={styles.alert} role="alert"><span>{error}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></section>}

      <section className={styles.controlPanel} aria-label="Filter kalender data sensor">
        <div className={styles.controlCopy}>
          <span className={styles.panelEyebrow}>FILTER SENSOR</span>
          <h2>Pilih jenis data, sensor, dan tahun</h2>
          <p>Daftar sensor mengikuti parameter yang tersedia.</p>
        </div>
        <label className={styles.control}>
          <span>JENIS DATA</span>
          <select value={kind} onChange={event => updateFilters(event.target.value as CalendarKind, year)}>
            {Object.entries(CALENDAR_KINDS).map(([key, item]) => <option key={key} value={key}>{item.label}</option>)}
          </select>
        </label>
        <label className={styles.control}>
          <span>LOGGER SENSOR</span>
          <select value={selectedDevice?.loggerId ?? ''} onChange={event => updateFilters(kind, year, Number(event.target.value))} disabled={loading || devices.length === 0}>
            {devices.length === 0 && <option value="">Tidak ada sensor {definition.label}</option>}
            {devices.map(device => <option key={device.loggerId} value={device.loggerId}>{getDisplayName(device)} · {device.loggerCode}</option>)}
          </select>
        </label>
        <label className={styles.control}>
          <span>TAHUN</span>
          <select value={year} onChange={event => updateFilters(kind, Number(event.target.value))}>
            {yearOptions.map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </label>
      </section>

      <section className={styles.selectedBar} aria-label="Logger kalender terpilih">
        <div>
          <span className={styles.panelEyebrow}>LOGGER TERPILIH</span>
          <strong>{selectedDevice ? getDisplayName(selectedDevice) : 'Belum ada logger'}</strong>
          <small>{selectedDevice?.loggerCode ?? '-'} · {selectedDevice?.sourceType ?? '-'} · {definition.label} ({definition.unit}) · {isRain ? 'Akumulasi harian' : 'Rata-rata harian'}</small>
        </div>
        {selectedDevice && <span className={`${styles.statusBadge} ${styles[`status-${monitoringStatusTone(selectedDevice.status)}`]}`}><i />{monitoringStatusLabel(selectedDevice.status)}</span>}
      </section>

      <section className={styles.summaryGrid} aria-label={`Ringkasan ${definition.label}`}>
        {isRain ? <>
          <SummaryCard label="Hari dengan hujan" value={summary.rainyDays} hint={`Ambang ≥ 0,5 mm pada ${year}`} tone="rain" />
          <SummaryCard label="Akumulasi terbaca" value={formatNumber(summary.total)} hint="Total curah hujan tersedia (mm)" tone="total" />
          <SummaryCard label="Pembacaan tertinggi" value={formatNumber(summary.maximum)} hint="Nilai harian tertinggi (mm)" tone="peak" />
        </> : <>
          <SummaryCard label="Rata-rata" value={formatNumber(statistics.average)} hint="Rata-rata dari nilai harian tersedia (cm)" tone="total" />
          <SummaryCard label="Terendah" value={formatNumber(statistics.minimum)} hint="Rata-rata harian terendah (cm)" tone="rain" />
          <SummaryCard label="Tertinggi" value={formatNumber(statistics.maximum)} hint="Rata-rata harian tertinggi (cm)" tone="peak" />
        </>}
        <SummaryCard label="Data harian" value={calendar.size} hint="Jumlah hari dengan pembacaan valid" tone="days" />
      </section>

      {historyError && <section className={styles.alert} role="alert"><span>{historyError}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></section>}

      <section className={styles.calendarPanel} aria-label={`Kalender pembacaan ${definition.label}`}>
        <div className={styles.panelHeader}>
          <div><span className={styles.panelEyebrow}>SENSOR HISTORY</span><h2>Kalender {definition.label}</h2><p>{isRain ? 'Akumulasi' : 'Rata-rata'} harian ({definition.unit}) · data tahun {year}</p></div>
          <span className={styles.sourceBadge}>{selectedDevice ? `${getDisplayName(selectedDevice)} · ${year}` : 'Pilih logger'}</span>
        </div>
        {loading || historyLoading ? <div className={styles.state}><strong>Memuat kalender data sensor...</strong><span>Menyiapkan pembacaan harian sensor terpilih.</span></div> : !selectedDevice ? <div className={styles.state}><strong>Tidak ada sensor dengan parameter {definition.label}</strong><span>Pilih jenis data lain untuk melihat sensor yang tersedia.</span></div> : calendar.size === 0 ? <div className={styles.state}><strong>Belum ada pembacaan {definition.label} untuk filter ini</strong><span>Tidak ada data valid pada tahun {year}. Data kosong tidak dianggap nol.</span></div> : <div className={styles.tableWrap}>
          <table className={styles.calendarTable}>
            <thead><tr><th>Bulan / Tanggal</th>{Array.from({ length: 31 }, (_, index) => <th key={index}>{String(index + 1).padStart(2, '0')}</th>)}</tr></thead>
            <tbody>{MONTHS.map((month, monthIndex) => <tr key={month}><th>{month}</th>{Array.from({ length: 31 }, (_, dayIndex) => {
              const day = dayIndex + 1
              if (day > daysInMonth(year, monthIndex)) return <td key={day} className={styles.invalidDay} />
              const key = `${year}-${String(monthIndex + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
              const value = calendar.get(key)
              const category = kind === 'tmat' ? classifyTmat(value) : kind === 'tmak' ? classifyTmak(value) : null
              const cell = isRain
                ? getRainCellStyle(value)
                : kind === 'tmat'
                  ? getTmatCellStyle(value)
                  : kind === 'tmak'
                    ? getTmakCellStyle(value)
                    : getLevelCellStyle(value, statistics.minimum, statistics.maximum)
              return <td key={day} style={{ background: cell.background, color: cell.color }} title={value === undefined ? `${key}: tidak ada data` : `${key}: ${formatNumber(value)} ${definition.unit}${category ? ` · ${category.label}` : ''}`}>{value === undefined ? '–' : formatNumber(value)}</td>
            })}</tr>)}</tbody>
          </table>
        </div>}
      </section>

      {isRain ? <div className={styles.legend} aria-label="Legenda curah hujan harian">
        <strong>Intensitas:</strong><Legend color="#f1f5f4" label="Tidak ada data" /><Legend color="#ffffff" label="&lt; 0,5 mm" /><Legend color="#c8f0dc" label="0,5–20 mm" /><Legend color="#f8e7a9" label="21–50 mm" /><Legend color="#f9c58e" label="51–100 mm" /><Legend color="#f3a1a1" label="101–150 mm" /><Legend color="#cdb0ec" label="&gt; 150 mm" />
      </div> : kind === 'tmat' ? <div className={styles.legend} aria-label="Legenda kategori TMAT vendor">
        <strong>Kategori TMAT:</strong><Legend color="#f1f5f4" label="Tidak ada data" />
        {TMAT_CATEGORIES.map(category => <Legend key={category.id} color={category.background} label={`${category.label}: ${category.range}`} />)}
      </div> : kind === 'tmak' ? <div className={styles.legend} aria-label="Legenda kategori TMAK SiTMAT">
        <strong>Kategori TMAK SiTMAT:</strong><Legend color="#f1f5f4" label="Tidak ada data" />
        {TMAK_CATEGORIES.map(category => <Legend key={category.id} color={category.background} label={`${category.label}: ${category.range}`} />)}
        <span>*Kategori warna mengikuti referensi SiTMAT/KLH. PP 57/2016 menetapkan kriteria muka air tanah lebih dari 40 cm di bawah permukaan gambut.</span>
      </div> : <div className={styles.legend} aria-label="Legenda rata-rata muka air">
        <strong>Rentang data:</strong><Legend color="#f1f5f4" label="Tidak ada data" /><Legend color="#e8f3fb" label={`Lebih rendah (${formatNumber(statistics.minimum)} cm)`} /><Legend color="#91c5e8" label="Nilai tengah" /><Legend color="#246a9a" label={`Lebih tinggi (${formatNumber(statistics.maximum)} cm)`} /><span>Warna mengikuti rentang data, bukan ambang bahaya.</span>
      </div>}

      {selectedDevice?.lastReceivedAt && <p className={styles.freshness}>Snapshot logger terakhir diterima {formatMonitoringDate(selectedDevice.lastReceivedAt)}. Kalender mengikuti jenis data, sensor, dan tahun terpilih.</p>}
    </main>
  )
}

function SummaryCard({ label, value, hint, tone }: { label: string; value: number | string; hint: string; tone: string }) {
  return <article className={`${styles.summaryCard} ${styles[`tone-${tone}`]}`}><span>{label}</span><strong>{value}</strong><small>{hint}</small></article>
}

function Legend({ color, label }: { color: string; label: string }) {
  return <span className={styles.legendItem}><i style={{ background: color }} />{label}</span>
}
