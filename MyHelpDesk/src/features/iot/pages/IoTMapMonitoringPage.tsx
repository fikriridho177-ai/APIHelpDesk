import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import WeatherForecastPanel from '../../weather/components/WeatherForecastPanel'
import type { WeatherSensorLocation } from '../../weather/types/weather'
import { getIoTMonitoring, getIoTParameters } from '../api/iotApi'
import IoTMonitoringMap from '../components/IoTMonitoringMap'
import {
  formatMonitoringDate as formatDate,
  formatMonitoringNumber as formatNumber,
  getMonitoringParameterCode as getParameterCode,
  getMonitoringParameterName as getParameterName,
  getMonitoringParameterUnit as getParameterUnit,
  getMonitoringSummary,
  hasValidCoordinates,
  monitoringStatusLabel,
  monitoringStatusTone,
} from '../lib/monitoring'
import type { IoTMonitoringDevice, IoTMonitoringResponse, IoTParameter } from '../types/iot'
import styles from './IoTMapMonitoringPage.module.css'

const MENU_CODE = 'DASHBOARD_IOT'

export default function IoTMapMonitoringPage() {
  const { permission, loaded } = useMenuPermission(MENU_CODE)
  const canRead = !!permission?.canRead
  const [monitoring, setMonitoring] = useState<IoTMonitoringResponse | null>(null)
  const [selectedLoggerId, setSelectedLoggerId] = useState<number | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [parameters, setParameters] = useState<IoTParameter[]>([])
  const [loading, setLoading] = useState(true)
  const [parametersLoading, setParametersLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [parametersError, setParametersError] = useState<string | null>(null)
  const [mapError, setMapError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const monitoringRequestRef = useRef(0)
  const parameterRequestRef = useRef(0)

  const loadMonitoring = useCallback(async () => {
    const requestId = ++monitoringRequestRef.current
    setLoading(true)
    setError(null)
    try {
      const response = await getIoTMonitoring()
      if (requestId !== monitoringRequestRef.current) return
      setMonitoring(response)
      setSelectedLoggerId(current => current !== null && response.devices.some(device => device.loggerId === current) ? current : null)
    } catch (loadError) {
      if (requestId !== monitoringRequestRef.current) return
      console.error('Gagal memuat monitoring IoT berbasis peta:', loadError)
      setMonitoring(null)
      setSelectedLoggerId(null)
      setError('Data monitoring IoT gagal dimuat. Periksa koneksi API lalu coba lagi.')
    } finally {
      if (requestId === monitoringRequestRef.current) setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (loaded && canRead) void loadMonitoring()
  }, [canRead, loaded, loadMonitoring, refreshKey])

  const selectedDevice = useMemo(
    () => monitoring?.devices.find(device => device.loggerId === selectedLoggerId) ?? null,
    [monitoring?.devices, selectedLoggerId],
  )

  useEffect(() => {
    if (!selectedDevice) setDetailOpen(false)
  }, [selectedDevice])

  useEffect(() => {
    if (!detailOpen) return undefined
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setDetailOpen(false)
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [detailOpen])

  const selectLogger = useCallback((loggerId: number) => {
    setSelectedLoggerId(loggerId)
    setDetailOpen(true)
  }, [])

  useEffect(() => {
    const requestId = ++parameterRequestRef.current
    setParameters([])
    setParametersError(null)
    if (!selectedDevice) {
      setParametersLoading(false)
      return undefined
    }

    setParametersLoading(true)
    getIoTParameters(selectedDevice.loggerId)
      .then(response => {
        if (requestId === parameterRequestRef.current) setParameters(response.parameters)
      })
      .catch(loadError => {
        if (requestId !== parameterRequestRef.current) return
        console.error('Gagal memuat metadata parameter logger:', loadError)
        setParametersError('Metadata parameter belum dapat dimuat. Kode parameter tetap ditampilkan.')
      })
      .finally(() => {
        if (requestId === parameterRequestRef.current) setParametersLoading(false)
      })

    return undefined
  }, [selectedDevice])

  const summary = useMemo(
    () => monitoring ? getMonitoringSummary(monitoring) : null,
    [monitoring],
  )
  const parameterMap = useMemo(
    () => new Map(parameters.filter(parameter => parameter.parameterCode).map(parameter => [parameter.parameterCode!, parameter])),
    [parameters],
  )
  const weatherLocation = useMemo<WeatherSensorLocation[]>(() => selectedDevice ? [{
    loggerId: selectedDevice.loggerId,
    loggerCode: selectedDevice.loggerCode ?? `LOGGER-${selectedDevice.loggerId}`,
    namaLogger: selectedDevice.namaLogger ?? 'Logger IoT',
    lokasi: selectedDevice.lokasi,
    sourceType: selectedDevice.sourceType ?? 'UNKNOWN',
    latitude: selectedDevice.latitude,
    longitude: selectedDevice.longitude,
  }] : [], [selectedDevice])

  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  return (
    <main className={styles.page}>
      <header className={styles.hero}>
        <div>
          <p className={styles.eyebrow}><span className={styles.liveDot} /> IOT MANAGEMENT / MAP OPERATIONS</p>
          <h1>Peta Monitoring IoT</h1>
          <p className={styles.subtitle}>Pantau posisi, status, pembacaan terakhir, dan konteks cuaca setiap logger dari satu layar.</p>
          <div className={styles.heroMeta}><span>SUMBER VENDOR + INTERNAL</span><span>{monitoring ? `UPDATE ${formatDate(new Date().toISOString())}` : 'DATA BELUM DIMUAT'}</span></div>
        </div>
        <button type="button" className={styles.refreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading}>
          {loading ? 'Memuat...' : 'Refresh monitoring'}
        </button>
      </header>

      {error && <section className={styles.alert} role="alert"><span>{error}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></section>}
      {mapError && <section className={styles.mapAlert} role="status"><span>{mapError}</span><button type="button" onClick={() => setMapError(null)}>Tutup</button></section>}

      {loading && !monitoring ? (
        <section className={styles.state}><strong>Menyiapkan peta monitoring...</strong><span>Mengambil status fleet dan pembacaan terakhir.</span></section>
      ) : !monitoring || monitoring.devices.length === 0 ? (
        <section className={styles.state}><strong>Belum ada perangkat untuk dipetakan</strong><span>Data monitoring belum tersedia atau fleet masih kosong.</span></section>
      ) : (
        <>
          <section className={styles.summaryGrid} aria-label="Ringkasan peta monitoring IoT">
            <SummaryCard label="Total logger" value={summary?.total ?? 0} hint="Vendor + Internal" tone="neutral" />
            <SummaryCard label="Online" value={summary?.online ?? 0} hint="Status dalam interval" tone="online" />
            <SummaryCard label="Tidak online" value={summary?.offline ?? 0} hint="Stale, kosong, atau lain" tone="warning" />
            <SummaryCard label="Issue status" value={summary?.issues ?? 0} hint="Nonaktif, no data, unknown" tone="issue" />
            <SummaryCard label="Terpetakan" value={summary?.mapped ?? 0} hint="Koordinat valid" tone="mapped" />
          </section>

          <section className={styles.workspace}>
            <section className={styles.mapPanel} aria-label="Peta posisi logger IoT">
              <div className={styles.panelHeader}>
                <div><span className={styles.panelEyebrow}>LIVE FLEET MAP</span><h2>Posisi perangkat</h2><p>Klik marker atau pilih perangkat dari daftar untuk membuka detail.</p></div>
                <div className={styles.legend} aria-label="Legenda status"><span><i className={styles.legendOnline} /> Online</span><span><i className={styles.legendStale} /> Stale</span><span><i className={styles.legendIssue} /> Issue</span></div>
              </div>
              <IoTMonitoringMap devices={monitoring.devices} selectedLoggerId={selectedLoggerId} onSelectLogger={selectLogger} onMapError={setMapError} />
            </section>

            <aside className={styles.sidePanel}>
              <section className={styles.devicePanel} aria-label="Daftar perangkat monitoring">
                <div className={styles.panelHeader}><div><span className={styles.panelEyebrow}>DEVICE FLEET</span><h2>Daftar logger</h2><p>{monitoring.devices.length} perangkat · {summary?.mapped ?? 0} terpetakan</p></div></div>
                <div className={styles.deviceList}>
                  {monitoring.devices.map(device => <DeviceListItem key={device.loggerId} device={device} selected={device.loggerId === selectedLoggerId} onSelect={() => selectLogger(device.loggerId)} />)}
                </div>
              </section>

              {selectedDevice && detailOpen ? (
                <section className={styles.detailPanel} aria-label="Detail logger terpilih">
                  <div className={styles.detailHeader}><div><span className={styles.panelEyebrow}>SELECTED DEVICE</span><h2>{selectedDevice.namaLogger ?? selectedDevice.loggerCode ?? 'Logger IoT'}</h2><p>{selectedDevice.loggerCode ?? `Logger ${selectedDevice.loggerId}`} · {selectedDevice.sourceType ?? 'Sumber tidak diketahui'}</p></div><div className={styles.drawerHeaderActions}><span className={`${styles.statusBadge} ${styles[`status-${monitoringStatusTone(selectedDevice.status)}`]}`}><i />{monitoringStatusLabel(selectedDevice.status)}</span><button type="button" className={styles.closeButton} onClick={() => setDetailOpen(false)}>Tutup</button></div></div>
                  <div className={styles.observationSection}><div className={styles.sectionHeading}><div><span className={styles.panelEyebrow}>ACTUAL OBSERVATION</span><h3>Pembacaan terbaru</h3></div>{selectedDevice.ageMinutes != null && <small>Usia data {formatNumber(selectedDevice.ageMinutes)} menit</small>}</div>{parametersLoading ? <div className={styles.inlineState}>Memuat metadata parameter...</div> : Object.keys(selectedDevice.data).length === 0 ? <div className={styles.inlineState}>Belum ada pembacaan sensor pada response monitoring.</div> : <div className={styles.observationGrid}>{Object.entries(selectedDevice.data).map(([key, value]) => { const code = getParameterCode(key); const parameter = parameterMap.get(code); return <div className={styles.observation} key={key}><span>{getParameterName(parameter, code)}</span><strong>{formatNumber(value)}</strong><small>{getParameterUnit(parameter)} · {code}</small></div> })}</div>}{parametersError && <small className={styles.detailNote}>{parametersError}</small>}</div>
                  <div className={styles.weatherSection}><WeatherForecastPanel comfortable sensorLocations={weatherLocation} selectedSensorId={selectedDevice.loggerId} allowDefaultLocation={false} allowManualLocation={false} /></div>
                </section>
              ) : !selectedDevice ? <section className={styles.noSelection}><span>+</span><strong>Pilih marker atau logger</strong><p>Detail sensor dan prakiraan cuaca akan tampil di sini.</p></section> : null}
            </aside>
          </section>
        </>
      )}
    </main>
  )
}

function SummaryCard({ label, value, hint, tone }: { label: string; value: number; hint: string; tone: string }) {
  return <article className={`${styles.summaryCard} ${styles[`tone-${tone}`]}`}><span className={styles.summaryLabel}>{label}</span><strong>{value}</strong><small>{hint}</small></article>
}

function DeviceListItem({ device, selected, onSelect }: { device: IoTMonitoringDevice; selected: boolean; onSelect: () => void }) {
  return <button type="button" className={`${styles.deviceItem} ${selected ? styles.deviceSelected : ''}`} onClick={onSelect} aria-pressed={selected}><span className={`${styles.deviceDot} ${styles[`dot-${monitoringStatusTone(device.status)}`]}`} /><span className={styles.deviceCopy}><strong>{device.namaLogger ?? device.loggerCode ?? `Logger ${device.loggerId}`}</strong><small>{device.loggerCode ?? 'Kode tidak tersedia'} · {device.sourceType ?? 'UNKNOWN'}</small></span><span className={styles.deviceStatus}>{monitoringStatusLabel(device.status)}<small>{hasValidCoordinates(device.latitude, device.longitude) ? 'Terpetakan' : 'Tanpa koordinat'}</small></span></button>
}

