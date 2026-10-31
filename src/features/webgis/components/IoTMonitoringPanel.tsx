import { useEffect, useMemo, useRef } from 'react'

import WeatherForecastPanel from '../../weather/components/WeatherForecastPanel'
import type { WeatherSensorLocation } from '../../weather/types/weather'
import {
  formatMonitoringDate,
  formatMonitoringNumber,
  getMonitoringParameterName,
  getMonitoringParameterUnit,
  getMonitoringSummary,
  getOrderedMonitoringReadings,
  hasValidCoordinates,
  monitoringStatusLabel,
  monitoringStatusTone,
} from '../../iot/lib/monitoring'
import type { IoTMonitoringDevice } from '../../iot/types/iot'
import type { IoTMapMonitoringState } from '../hooks/useIoTMapMonitoring'
import styles from './IoTMonitoringPanel.module.css'

export interface IoTMonitoringPanelProps {
  state: IoTMapMonitoringState
  onFocusDevices: () => void
  expanded: boolean
  onToggle: () => void
}

export default function IoTMonitoringPanel({ state, onFocusDevices, expanded, onToggle }: IoTMonitoringPanelProps) {
  const detailRef = useRef<HTMLDivElement | null>(null)
  useEffect(() => {
    if (!expanded || !state.detailOpen || !state.selectedDevice) return
    detailRef.current?.scrollIntoView({ block: 'nearest' })
  }, [expanded, state.detailOpen, state.selectedDevice?.loggerId])

  if (!state.permissionLoaded) return <div className={styles.loading} role="status">Memuat akses monitoring IoT...</div>
  if (!state.canRead) return null

  const summary = state.monitoring ? getMonitoringSummary(state.monitoring) : null
  const devices = state.monitoring?.devices ?? []
  const hasMappableDevices = devices.some(device => hasValidCoordinates(device.latitude, device.longitude))

  return (
    <section className={styles.panel} aria-label="Monitoring IoT">
      <header className={styles.header}>
        <button type="button" className={styles.toggle} aria-expanded={expanded} aria-controls="iot-monitoring-content" onClick={onToggle}>
          <span className={styles.headerCopy}>
            <span className={styles.eyebrow}>IoT / LIVE MONITORING</span>
            <h2>Monitoring logger</h2>
            <p>{state.monitoring ? `Diperbarui ${formatMonitoringDate(new Date().toISOString())}` : 'Status fleet dan pembacaan terakhir.'}</p>
          </span>
          <span className={styles.chevron} aria-hidden="true">{expanded ? '−' : '+'}</span>
        </button>
        {expanded && <button type="button" className={styles.iconButton} onClick={state.refresh} disabled={state.loading} aria-label="Refresh monitoring IoT">
          {state.loading ? 'Memuat...' : 'Refresh'}
        </button>}
      </header>

      <div id="iot-monitoring-content" className={styles.content} hidden={!expanded}>
        {state.error && <div className={styles.error} role="alert"><span>{state.error}</span><button type="button" onClick={state.refresh}>Coba lagi</button></div>}
        {state.loading && !state.monitoring ? <div className={styles.state} role="status">Mengambil status fleet dan pembacaan terakhir...</div> : null}
        {!state.loading && !state.monitoring && !state.error ? <div className={styles.state}>Belum ada data monitoring.</div> : null}

        {state.monitoring && (
          <>
            <div className={styles.summary} aria-label="Ringkasan monitoring IoT">
              <Summary label="Total logger" value={summary?.total ?? 0} />
              <Summary label="Online" value={summary?.online ?? 0} tone="online" />
              <Summary label="Tidak online" value={summary?.offline ?? 0} tone="warning" />
              <Summary label="Issue status" value={summary?.issues ?? 0} tone="issue" />
              <Summary label="Terpetakan" value={summary?.mapped ?? 0} tone="mapped" />
            </div>
            <div className={styles.actions}>
              <span>{devices.length} logger · {summary?.mapped ?? 0} terpetakan</span>
              <button type="button" onClick={onFocusDevices} disabled={!hasMappableDevices}>Pusatkan logger</button>
            </div>
            {devices.length === 0 ? <div className={styles.state}>Belum ada perangkat untuk dipantau.</div> : <div className={styles.deviceList} aria-label="Daftar logger monitoring">
              {devices.map(device => <DeviceListItem key={device.loggerId} device={device} selected={device.loggerId === state.selectedLoggerId} onSelect={() => state.selectLogger(device.loggerId)} />)}
            </div>}
            {state.selectedDevice && state.detailOpen ? <div ref={detailRef}><LoggerDetail state={state} /></div> : state.selectedDevice ? <div className={styles.closedDetail} role="status">Detail logger ditutup. Pilih logger lagi untuk membuka detail.</div> : <div className={styles.noSelection}>Pilih logger untuk melihat detail sensor dan prakiraan cuaca.</div>}
          </>
        )}
      </div>
    </section>
  )
}

function Summary({ label, value, tone = 'neutral' }: { label: string; value: number; tone?: string }) {
  return <div className={`${styles.summaryItem} ${styles[`tone-${tone}`]}`}><span>{label}</span><strong>{value}</strong></div>
}

function DeviceListItem({ device, selected, onSelect }: { device: IoTMonitoringDevice; selected: boolean; onSelect: () => void }) {
  const tone = monitoringStatusTone(device.status)
  return <button type="button" className={`${styles.deviceItem} ${selected ? styles.selected : ''}`} onClick={onSelect} aria-pressed={selected}>
    <span className={`${styles.statusDot} ${styles[`dot-${tone}`]}`} aria-hidden="true" />
    <span className={styles.deviceCopy}><strong>{device.namaLogger ?? device.loggerCode ?? `Logger ${device.loggerId}`}</strong><small>{device.loggerCode ?? 'Kode tidak tersedia'} · {device.sourceType ?? 'UNKNOWN'}</small></span>
    <span className={styles.deviceStatus}>{monitoringStatusLabel(device.status)}<small>{hasValidCoordinates(device.latitude, device.longitude) ? 'Terpetakan' : 'Tanpa koordinat'}</small></span>
  </button>
}

function LoggerDetail({ state }: { state: IoTMapMonitoringState }) {
  const device = state.selectedDevice
  const parameterMap = useMemo(() => new Map(state.parameters.filter(parameter => parameter.parameterCode).map(parameter => [parameter.parameterCode!.trim().toUpperCase(), parameter])), [state.parameters])
  const readings = useMemo(() => device ? getOrderedMonitoringReadings(device.data) : [], [device?.data])
  const priorityReadings = readings.filter(reading => ['TMAK', 'TMAT'].includes(reading.code.toUpperCase()))
  const otherReadings = readings.filter(reading => !['TMAK', 'TMAT'].includes(reading.code.toUpperCase()))
  const weatherLocation = useMemo<WeatherSensorLocation[]>(() => device ? [{
    loggerId: device.loggerId,
    loggerCode: device.loggerCode ?? `LOGGER-${device.loggerId}`,
    namaLogger: device.namaLogger ?? 'Logger IoT',
    lokasi: device.lokasi,
    sourceType: device.sourceType ?? 'UNKNOWN',
    latitude: device.latitude,
    longitude: device.longitude,
  }] : [], [device])
  if (!device) return null

  const renderReading = (reading: ReturnType<typeof getOrderedMonitoringReadings>[number]) => {
    const code = reading.code.toUpperCase()
    const parameter = parameterMap.get(code)
    return <div className={styles.value} key={reading.key}>
      <span>{getMonitoringParameterName(parameter, code)}</span>
      <strong>{formatMonitoringNumber(reading.value)}</strong>
      <small>{getMonitoringParameterUnit(parameter)} · {code}</small>
    </div>
  }

  return <section className={styles.detail} aria-label="Detail logger terpilih">
    <div className={styles.detailHeader}><div><span className={styles.eyebrow}>LOGGER TERPILIH</span><h3>{device.namaLogger ?? device.loggerCode ?? 'Logger IoT'}</h3><p>{device.loggerCode ?? `Logger ${device.loggerId}`} · {device.sourceType ?? 'Sumber tidak diketahui'}</p></div><div className={styles.detailActions}><span className={`${styles.statusBadge} ${styles[`status-${monitoringStatusTone(device.status)}`]}`}>{monitoringStatusLabel(device.status)}</span><button type="button" onClick={state.closeDetail} aria-label="Tutup detail logger">Tutup</button></div></div>
    <div className={styles.observation}><div className={styles.sectionHeading}><strong>Pembacaan terbaru</strong>{device.ageMinutes != null && <small>Usia data {formatMonitoringNumber(device.ageMinutes)} menit</small>}</div>
      {state.parametersLoading ? <div className={styles.inlineState}>Memuat metadata parameter...</div> : readings.length === 0 ? <div className={styles.inlineState}>Belum ada pembacaan sensor pada response monitoring.</div> : <>
        {priorityReadings.length > 0 && <div className={styles.priorityObservation}><div className={styles.sectionHeading}><strong>Data utama TMAK / TMAT</strong><small>Pembacaan utama sensor</small></div><div className={`${styles.values} ${styles.priorityValues}`}>{priorityReadings.map(renderReading)}</div></div>}
        {otherReadings.length > 0 && <div className={styles.otherObservation}><div className={styles.sectionHeading}><strong>Pembacaan lain</strong></div><div className={styles.values}>{otherReadings.map(renderReading)}</div></div>}
      </>}
      {state.parametersError && <small className={styles.detailError}>{state.parametersError}</small>}
    </div>
    <WeatherForecastPanel compact sensorLocations={weatherLocation} selectedSensorId={device.loggerId} allowDefaultLocation={false} allowManualLocation={false} />
  </section>
}
