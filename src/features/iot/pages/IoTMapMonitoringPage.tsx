import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTMonitoring, getIoTParameters } from '../api/iotApi'
import { adjustTmakValue } from '../lib/tmakOffset'
import IoTMonitoringMap from '../components/IoTMonitoringMap'
import {
  formatMonitoringNumber as formatNumber,
  getMonitoringParameterCode as getParameterCode,
  getMonitoringParameterName as getParameterName,
  getMonitoringParameterUnit as getParameterUnit,
  monitoringStatusLabel,
  monitoringStatusTone,
} from '../lib/monitoring'
import type { IoTMonitoringResponse, IoTParameter } from '../types/iot'
import styles from './IoTMapMonitoringPage.module.css'

const MENU_CODE = 'DASHBOARD_IOT'

interface IoTMapMonitoringPageProps {
  permissionCode?: string
}
export default function IoTMapMonitoringPage({ permissionCode = MENU_CODE }: IoTMapMonitoringPageProps) {
  const { permission, loaded } = useMenuPermission(permissionCode)
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
        setParametersError('Metadata parameter belum dapat dimuat. Nama parameter akan mengikuti data monitoring.')
      })
      .finally(() => {
        if (requestId === parameterRequestRef.current) setParametersLoading(false)
      })

    return undefined
  }, [selectedDevice])

  const parameterMap = useMemo(
    () => new Map(parameters.filter(parameter => parameter.parameterCode).map(parameter => [parameter.parameterCode!, parameter])),
    [parameters],
  )
  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  return (
    <main className={styles.page}>
      {error && <section className={styles.alert} role="alert"><span>{error}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></section>}
      {mapError && <section className={styles.mapAlert} role="status"><span>{mapError}</span><button type="button" onClick={() => setMapError(null)}>Tutup</button></section>}

      {loading && !monitoring ? (
        <section className={styles.state}><strong>Menyiapkan peta monitoring...</strong><span>Mengambil status fleet dan pembacaan terakhir.</span></section>
      ) : !monitoring || monitoring.devices.length === 0 ? (
        <section className={styles.state}><strong>Belum ada perangkat untuk dipetakan</strong><span>Data monitoring belum tersedia atau fleet masih kosong.</span></section>
      ) : (
        <section className={styles.workspace}>
          <section className={styles.mapPanel} aria-label="Peta posisi logger IoT">
            <div className={`${styles.panelHeader} ${styles.mapPanelHeader}`}>
              <div><span className={styles.panelEyebrow}>LIVE SENSOR MAP</span><h2>Peta Monitoring IoT</h2><p>Gunakan marker atau pilih logger untuk membaca pembacaan terbaru.</p></div>
              <div className={styles.mapTools}>
                <button type="button" className={styles.mapRefreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading}>
                  {loading ? 'Memuat...' : 'Refresh'}
                </button>
                <label className={styles.loggerSelect}>
                  <span>PILIH LOGGER</span>
                  <select aria-label="Pilih logger pada peta" value={selectedLoggerId ?? ''} onChange={event => {
                    const loggerId = Number(event.target.value)
                    if (loggerId > 0) selectLogger(loggerId)
                  }}>
                    <option value="">Semua marker</option>
                    {monitoring.devices.map(device => <option key={device.loggerId} value={device.loggerId}>{device.loggerCode ?? device.namaLogger ?? `Logger ${device.loggerId}`}</option>)}
                  </select>
                </label>
                <div className={styles.legend} aria-label="Legenda status"><span><i className={styles.legendOnline} /> Online</span><span><i className={styles.legendStale} /> Stale</span><span><i className={styles.legendIssue} /> Issue</span></div>
              </div>
            </div>
            <IoTMonitoringMap devices={monitoring.devices} selectedLoggerId={selectedLoggerId} onSelectLogger={selectLogger} onMapError={setMapError} />
          </section>

          <div className={styles.detailSlot}>
            {selectedDevice && detailOpen ? (
              <section className={styles.detailPanel} aria-label="Detail logger terpilih">
                <div className={styles.detailHeader}><div><span className={styles.panelEyebrow}>SELECTED DEVICE</span><h2>{selectedDevice.namaLogger ?? selectedDevice.loggerCode ?? 'Logger IoT'}</h2><p>{selectedDevice.loggerCode ?? `Logger ${selectedDevice.loggerId}`} · {selectedDevice.sourceType ?? 'Sumber tidak diketahui'}</p></div><div className={styles.drawerHeaderActions}><span className={`${styles.statusBadge} ${styles[`status-${monitoringStatusTone(selectedDevice.status)}`]}`}><i />{monitoringStatusLabel(selectedDevice.status)}</span><button type="button" className={styles.closeButton} onClick={() => setDetailOpen(false)}>Tutup</button></div></div>
                <div className={styles.observationSection}><div className={styles.sectionHeading}><div><span className={styles.panelEyebrow}>ACTUAL OBSERVATION</span><h3>Pembacaan terbaru</h3></div>{selectedDevice.ageMinutes != null && <small>Usia data {formatNumber(selectedDevice.ageMinutes)} menit</small>}</div>{parametersLoading ? <div className={styles.inlineState}>Memuat metadata parameter...</div> : Object.keys(selectedDevice.data).length === 0 ? <div className={styles.inlineState}>Belum ada pembacaan sensor pada response monitoring.</div> : <div className={styles.observationGrid}>{Object.entries(selectedDevice.data).map(([key, value]) => { const code = getParameterCode(key); const parameter = parameterMap.get(code); const displayedValue = adjustTmakValue(selectedDevice.loggerCode, code, value, selectedDevice.namaLogger); const numericValue = displayedValue == null ? null : Number(displayedValue); return <div className={styles.observation} key={key}><span>{getParameterName(parameter)}</span><strong>{formatNumber(numericValue != null && Number.isFinite(numericValue) ? numericValue : null)}</strong><small>{getParameterUnit(parameter)}</small></div> })}</div>}{parametersError && <small className={styles.detailNote}>{parametersError}</small>}</div>
              </section>
            ) : !selectedDevice ? <section className={styles.noSelection}><span>+</span><strong>Pilih marker atau logger</strong><p>Pembacaan terbaru akan tampil di sini setelah memilih perangkat.</p></section> : null}
          </div>
        </section>
      )}
    </main>
  )
}

