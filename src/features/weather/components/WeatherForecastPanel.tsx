import { useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { getWeatherForecast } from '../api/weatherApi'
import {
  WEATHER_FORECAST_DAYS,
  type WeatherDayForecast,
  type WeatherForecastResponse,
  type WeatherQuery,
  type WeatherSensorLocation,
} from '../types/weather'
import styles from './WeatherForecastPanel.module.css'

interface WeatherForecastPanelProps {
  compact?: boolean
  comfortable?: boolean
  sensorLocations?: WeatherSensorLocation[]
  selectedSensorId?: number | null
  onSelectedSensorIdChange?: (loggerId: number) => void
  allowDefaultLocation?: boolean
  allowManualLocation?: boolean
}

interface LocationDraft {
  label: string
  latitude: string
  longitude: string
}

const DEFAULT_LOCATION: LocationDraft = {
  label: 'Pekanbaru · default lokasi awal',
  latitude: '-0.5071',
  longitude: '101.4478',
}

function hasCoordinates(sensor: WeatherSensorLocation | null | undefined): sensor is WeatherSensorLocation & { latitude: number; longitude: number } {
  return Boolean(
    sensor
      && Number.isFinite(sensor.latitude)
      && Number.isFinite(sensor.longitude),
  )
}

function sensorDraft(sensor: WeatherSensorLocation): LocationDraft {
  return {
    label: `${sensor.loggerCode} · ${sensor.namaLogger}`,
    latitude: sensor.latitude === null ? '' : String(sensor.latitude),
    longitude: sensor.longitude === null ? '' : String(sensor.longitude),
  }
}

function formatDate(value: string | null | undefined, withTime = false) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('id-ID', {
    dateStyle: 'medium',
    ...(withTime ? { timeStyle: 'short' as const } : {}),
  }).format(date)
}

function formatDay(value: string) {
  const date = new Date(`${value}T12:00:00`)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('id-ID', { weekday: 'short', day: '2-digit', month: 'short' }).format(date)
}

function formatNumber(value: number | null, suffix = '') {
  return value === null || !Number.isFinite(value) ? '-' : `${new Intl.NumberFormat('id-ID', { maximumFractionDigits: 1 }).format(value)}${suffix}`
}

function weatherGlyph(code: number | null) {
  if (code === null) return '·'
  if (code >= 95) return '⚡'
  if (code >= 51) return '☂'
  if (code >= 45) return '◌'
  if (code === 3) return '☁'
  if (code === 2) return '◐'
  return '☀'
}

function buildQuery(draft: LocationDraft): WeatherQuery | null {
  const latitude = Number(draft.latitude)
  const longitude = Number(draft.longitude)
  if (!Number.isFinite(latitude) || latitude < -90 || latitude > 90) return null
  if (!Number.isFinite(longitude) || longitude < -180 || longitude > 180) return null
  return { latitude, longitude, days: WEATHER_FORECAST_DAYS }
}

export default function WeatherForecastPanel({
  compact = false,
  comfortable = false,
  sensorLocations = [],
  selectedSensorId,
  onSelectedSensorIdChange,
  allowDefaultLocation = true,
  allowManualLocation = true,
}: WeatherForecastPanelProps) {
  const firstSensorWithCoordinates = useMemo(
    () => sensorLocations.find(hasCoordinates) ?? null,
    [sensorLocations],
  )
  const [internalSensorId, setInternalSensorId] = useState<number | null>(
    () => selectedSensorId ?? firstSensorWithCoordinates?.loggerId ?? null,
  )
  const effectiveSensorId = selectedSensorId === undefined ? internalSensorId : selectedSensorId
  const selectedSensor = useMemo(
    () => sensorLocations.find(sensor => sensor.loggerId === effectiveSensorId) ?? null,
    [effectiveSensorId, sensorLocations],
  )
  const hasSensorSelection = sensorLocations.length === 0
    ? allowDefaultLocation
    : selectedSensor !== null
  const sensorMissingCoordinates = Boolean(selectedSensor && !hasCoordinates(selectedSensor))
  const [draft, setDraft] = useState<LocationDraft>(DEFAULT_LOCATION)
  const [activeLocation, setActiveLocation] = useState<LocationDraft>(DEFAULT_LOCATION)
  const [manualOverride, setManualOverride] = useState(false)
  const [weather, setWeather] = useState<WeatherForecastResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [locationError, setLocationError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const weatherRequestRef = useRef(0)

  useEffect(() => {
    if (selectedSensorId !== undefined || sensorLocations.length === 0) return
    const currentIsAvailable = internalSensorId !== null
      && sensorLocations.some(sensor => sensor.loggerId === internalSensorId)
    if (currentIsAvailable) return
    setInternalSensorId(firstSensorWithCoordinates?.loggerId ?? sensorLocations[0].loggerId)
  }, [firstSensorWithCoordinates, internalSensorId, selectedSensorId, sensorLocations])

  useEffect(() => {
    if (!selectedSensor) return
    const nextDraft = sensorDraft(selectedSensor)
    setDraft(nextDraft)
    setActiveLocation(nextDraft)
    setManualOverride(false)
    setLocationError(
      hasCoordinates(selectedSensor)
        ? null
        : allowManualLocation
          ? 'Logger ini belum memiliki latitude/longitude. Pilih logger lain atau gunakan override manual.'
          : 'Logger ini belum memiliki latitude/longitude. Prakiraan tidak tersedia untuk perangkat ini.',
    )
  }, [
    selectedSensor?.latitude,
    selectedSensor?.loggerCode,
    selectedSensor?.loggerId,
    selectedSensor?.longitude,
    selectedSensor?.namaLogger,
    allowManualLocation,
  ])

  const query = useMemo(() => {
    if (!hasSensorSelection || (sensorMissingCoordinates && !manualOverride)) return null
    const sensorLocation = selectedSensor && hasCoordinates(selectedSensor) && !manualOverride
      ? sensorDraft(selectedSensor)
      : activeLocation
    return buildQuery(sensorLocation)
  }, [activeLocation, hasSensorSelection, manualOverride, selectedSensor, sensorMissingCoordinates])

  useEffect(() => {
    const requestId = ++weatherRequestRef.current
    if (!query) {
      setWeather(null)
      setError(null)
      setLoading(false)
      return
    }

    setLoading(true)
    setError(null)
    setWeather(null)
    getWeatherForecast(query)
      .then(response => {
        if (requestId === weatherRequestRef.current) setWeather(response)
      })
      .catch(loadError => {
        if (requestId !== weatherRequestRef.current) return
        console.error('Gagal memuat prakiraan cuaca:', loadError)
        setError('Prakiraan cuaca belum dapat dimuat. Periksa koneksi API atau coba lagi.')
        setWeather(null)
      })
      .finally(() => {
        if (requestId === weatherRequestRef.current) setLoading(false)
      })
  }, [query, refreshKey])

  const visibleForecast = compact ? weather?.forecast.slice(0, 5) ?? [] : weather?.forecast ?? []
  function applyLocation(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!buildQuery(draft)) {
      setLocationError('Latitude harus -90 sampai 90 dan longitude -180 sampai 180.')
      return
    }
    setLocationError(null)
    setActiveLocation({ ...draft })
    setManualOverride(Boolean(selectedSensor) && allowManualLocation)
    setRefreshKey(key => key + 1)
  }

  function selectSensor(value: string) {
    const loggerId = Number(value)
    if (!Number.isInteger(loggerId)) return
    if (selectedSensorId === undefined) setInternalSensorId(loggerId)
    onSelectedSensorIdChange?.(loggerId)
    setManualOverride(false)
  }

  return (
    <section className={`${styles.panel} ${compact ? styles.compact : ''} ${comfortable ? styles.comfortable : ''}`} aria-label={`Prakiraan hujan ${WEATHER_FORECAST_DAYS} hari`}>
      <div className={styles.heading}>
        <div>
          <span className={styles.eyebrow}>WEATHER INTELLIGENCE / WAD</span>
          <h2>Prakiraan Hujan {WEATHER_FORECAST_DAYS} Hari</h2>
          <p>Forecast operasional berdasarkan koordinat logger terpilih.</p>
        </div>
        <div className={styles.headingActions}>
          {compact && <Link className={styles.detailLink} to="/ecs/prakiraan-hujan">Buka detail {WEATHER_FORECAST_DAYS} hari <span>↗</span></Link>}
          <button type="button" className={styles.refreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading}>{loading ? 'Memuat...' : 'Refresh'}</button>
        </div>
      </div>

      <div className={styles.locationBar}>
        <div>
          <span className={styles.locationLabel}>LOKASI PRAKIRAAN</span>
          <strong>{activeLocation.label || 'Lokasi terpilih'}{manualOverride && selectedSensor ? ' · override manual' : ''}</strong>
          <small>{activeLocation.latitude}, {activeLocation.longitude}</small>
        </div>
        {sensorLocations.length > 1 && <label className={styles.sensorSelect}>
          <span>LOGGER CUACA</span>
          <select value={effectiveSensorId ?? ''} onChange={event => selectSensor(event.target.value)}>
            <option value="" disabled>Pilih logger</option>
            {sensorLocations.map(sensor => <option key={sensor.loggerId} value={sensor.loggerId} disabled={!hasCoordinates(sensor)}>
              {!hasCoordinates(sensor) ? '⚠ ' : ''}{sensor.loggerCode} · {sensor.namaLogger}
            </option>)}
          </select>
        </label>}
        {allowManualLocation && <details className={styles.locationEditor}>
          <summary>Ubah lokasi</summary>
          <form onSubmit={applyLocation}>
            <label>Nama lokasi<input value={draft.label} onChange={event => setDraft(current => ({ ...current, label: event.target.value }))} /></label>
            <label>Latitude<input inputMode="decimal" value={draft.latitude} onChange={event => setDraft(current => ({ ...current, latitude: event.target.value }))} /></label>
            <label>Longitude<input inputMode="decimal" value={draft.longitude} onChange={event => setDraft(current => ({ ...current, longitude: event.target.value }))} /></label>
            <button type="submit">Terapkan lokasi</button>
          </form>
          {locationError && <small className={styles.locationError}>{locationError}</small>}
        </details>}
      </div>

      {sensorMissingCoordinates && !manualOverride && <div className={styles.noCoordinates} role="status">
        {allowManualLocation
          ? 'Logger terpilih belum memiliki koordinat. Prakiraan tidak dijalankan sampai logger lain dipilih atau koordinat diisi melalui override manual.'
          : 'Logger terpilih belum memiliki koordinat. Prakiraan tidak tersedia untuk perangkat ini.'}
      </div>}

      {error && <div className={styles.error} role="alert"><span>{error}</span><button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button></div>}

      {loading && <div className={styles.state}>Mengambil forecast berdasarkan koordinat logger...</div>}
      {!loading && !error && <div className={styles.forecastGrid}>{visibleForecast.map(day => <ForecastCard key={day.date} day={day} />)}</div>}
      {!loading && !error && weather && weather.forecast.length === 0 && <div className={styles.state}>Provider belum mengembalikan data prakiraan untuk lokasi ini.</div>}

      <footer className={styles.footer}><span>Forecast: <strong>{weather?.forecastSource ?? 'Open-Meteo'}</strong></span><span>{weather ? `Diperbarui ${formatDate(weather.fetchedAt, true)}` : 'Belum diperbarui'}</span></footer>
    </section>
  )
}

function ForecastCard({ day }: { day: WeatherDayForecast }) {
  return <article className={styles.forecastCard}><div className={styles.cardTop}><span>{formatDay(day.date)}</span><strong>{weatherGlyph(day.weatherCode)}</strong></div><h3>{day.summary}</h3><div className={styles.temperature}><strong>{formatNumber(day.temperatureMaxC, '°')}</strong><span>{formatNumber(day.temperatureMinC, '°')}</span></div><div className={styles.metrics}><span><small>Hujan</small><strong>{formatNumber(day.precipitationProbability, '%')}</strong></span><span><small>Curah</small><strong>{formatNumber(day.precipitationMm, ' mm')}</strong></span><span><small>Angin</small><strong>{formatNumber(day.windMaxKmh, ' km/j')}</strong></span></div></article>
}
