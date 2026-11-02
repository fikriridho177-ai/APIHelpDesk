import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { getIoTMonitoring, getIoTParameters } from '../../iot/api/iotApi'
import {
  formatMonitoringDate,
  formatMonitoringNumber,
  getMonitoringParameterCode,
  getMonitoringParameterName,
  getMonitoringParameterUnit,
  monitoringStatusLabel,
  monitoringStatusTone,
} from '../../iot/lib/monitoring'
import { adjustTmakValue } from '../../iot/lib/tmakOffset'
import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import type { IoTMonitoringDevice, IoTParameter } from '../../iot/types/iot'
import { groupLiveParameters, type LiveParameter } from '../utils/wadSensorDetail'
import common from './WadSensorOverviewPage.module.css'
import styles from './WadSensorDetailPage.module.css'

function normalizeParameter(value: string) {
  return value.trim().toUpperCase().replace(/[^A-Z0-9]/g, '')
}

function parameterMap(parameters: IoTParameter[]) {
  return new Map(
    parameters
      .filter(parameter => parameter.parameterCode)
      .map(parameter => [normalizeParameter(parameter.parameterCode ?? ''), parameter]),
  )
}

function getNumber(value: unknown) {
  if (value === null || value === undefined || value === '') return null
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function formatDeviceAge(ageMinutes: number | null) {
  if (ageMinutes == null || !Number.isFinite(ageMinutes)) return 'Belum diketahui'
  if (ageMinutes < 1) return 'Baru saja'
  if (ageMinutes < 60) return `${Math.round(ageMinutes)} menit lalu`

  const hours = ageMinutes / 60
  if (hours < 24) return `${Math.round(hours * 10) / 10} jam lalu`
  return `${Math.round(hours / 24 * 10) / 10} hari lalu`
}

function getLiveParameters(device: IoTMonitoringDevice, parameters: IoTParameter[]) {
  const metadata = parameterMap(parameters)

  return Object.entries(device.data ?? {})
    .filter(([, rawValue]) => rawValue !== null && rawValue !== undefined)
    .map(([key, rawValue]): LiveParameter => {
      const code = getMonitoringParameterCode(key)
      const parameter = metadata.get(normalizeParameter(code))
      const adjustedValue = adjustTmakValue(device.loggerCode, code, rawValue, device.namaLogger)

      return {
        code,
        label: getMonitoringParameterName(parameter),
        unit: getMonitoringParameterUnit(parameter),
        value: getNumber(adjustedValue),
        categoryName: parameter?.categoryName?.trim() || parameter?.kategoriNama?.trim() || '',
        categoryOrder: parameter?.categoryOrder ?? parameter?.kategoriUrutan ?? Number.MAX_SAFE_INTEGER,
      }
    })
    .sort((left, right) => left.categoryOrder - right.categoryOrder || left.label.localeCompare(right.label, 'id-ID'))
}

export default function WadSensorDetailPage() {
  const [searchParams] = useSearchParams()
  const requestedLoggerId = Number(searchParams.get('loggerId'))
  const hasLoggerId = Number.isInteger(requestedLoggerId) && requestedLoggerId > 0
  const { permission, loaded } = useMenuPermission('WAD_ECS_STASIUN')
  const canRead = !!permission?.canRead
  const [device, setDevice] = useState<IoTMonitoringDevice | null>(null)
  const [parameters, setParameters] = useState<IoTParameter[]>([])
  const [loading, setLoading] = useState(true)
  const [parametersLoading, setParametersLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [parametersError, setParametersError] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)

  const loadMonitoring = useCallback(async () => {
    if (!hasLoggerId) {
      setLoading(false)
      return
    }

    setLoading(true)
    setError(null)
    try {
      const response = await getIoTMonitoring()
      const nextDevice = response.devices.find(item => item.loggerId === requestedLoggerId) ?? null
      setDevice(nextDevice)
      setError(nextDevice ? null : 'Logger yang diminta tidak ditemukan pada snapshot monitoring terbaru.')
    } catch (loadError) {
      console.error('Gagal memuat detail sensor WAD:', loadError)
      setError('Detail sensor belum dapat dimuat. Periksa koneksi API lalu coba lagi.')
    } finally {
      setLoading(false)
    }
  }, [hasLoggerId, requestedLoggerId])

  useEffect(() => {
    if (loaded && canRead) void loadMonitoring()
  }, [canRead, loaded, loadMonitoring, refreshKey])

  const deviceLoggerId = device?.loggerId

  useEffect(() => {
    if (!deviceLoggerId) {
      setParameters([])
      setParametersLoading(false)
      return
    }

    let cancelled = false
    setParametersLoading(true)
    setParametersError(false)

    void getIoTParameters(deviceLoggerId)
      .then(response => {
        if (!cancelled) setParameters(response.parameters ?? [])
      })
      .catch(parameterError => {
        console.error('Gagal memuat metadata parameter sensor WAD:', parameterError)
        if (!cancelled) {
          setParameters([])
          setParametersError(true)
        }
      })
      .finally(() => {
        if (!cancelled) setParametersLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [deviceLoggerId])

  const liveParameters = useMemo(
    () => device ? getLiveParameters(device, parameters) : [],
    [device, parameters],
  )
  const liveParameterGroups = useMemo(() => groupLiveParameters(liveParameters), [liveParameters])

  if (!loaded || !canRead) return <NoAccess loaded={loaded} />

  if (!hasLoggerId) {
    return (
      <main className={common.page}>
        <section className={common.state}>
          <DashboardIcon name="info" />
          <strong>Logger belum dipilih</strong>
          <small>Pilih logger dari halaman monitoring sensor untuk melihat detail realtime.</small>
          <Link className={styles.stateLink} to="/ecs/stasiun-monitoring?filter=all">Kembali ke monitoring sensor</Link>
        </section>
      </main>
    )
  }

  return (
    <main className={common.page}>
      {error && (
        <section className={common.alert} role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => setRefreshKey(key => key + 1)}>Coba lagi</button>
        </section>
      )}

      {loading && !device ? (
        <section className={common.state}>
          <span className={common.spinner} />
          <strong>Mengambil snapshot sensor...</strong>
          <small>Memakai endpoint monitoring ringkas.</small>
        </section>
      ) : !device ? (
        <section className={common.state}>
          <DashboardIcon name="info" />
          <strong>Detail logger belum tersedia</strong>
          <small>Kembali ke daftar untuk memilih logger lain.</small>
          <Link className={styles.stateLink} to="/ecs/stasiun-monitoring?filter=all">Kembali ke monitoring sensor</Link>
        </section>
      ) : (
        <>
          <header className={common.hero}>
            <div className={common.heroCopy}>
              <div className={common.heroHeading}>
                <span className={common.heroIcon}><DashboardIcon name="water" /></span>
                <div>
                  <p className={common.eyebrow}><span className={common.liveDot} /> WAD / DETAIL SENSOR</p>
                  <h1>{device.namaLogger ?? 'Detail logger'}</h1>
                  <p className={common.subtitle}>
                    Snapshot realtime seluruh parameter yang dikirim logger ini. Histori dibuka terpisah saat diperlukan.
                  </p>
                </div>
              </div>
              <div className={common.heroMeta}>
                <span><strong>{device.loggerCode ?? `LOGGER-${device.loggerId}`}</strong> kode logger</span>
                <span><strong>{liveParameters.length}</strong> parameter terbaca</span>
                <span><strong>{formatDeviceAge(device.ageMinutes)}</strong> usia payload</span>
              </div>
            </div>
            <div className={common.heroActions}>
              <span className={common.liveBadge}><i /> {monitoringStatusLabel(device.status).toUpperCase()}</span>
              <button type="button" className={common.refreshButton} onClick={() => setRefreshKey(key => key + 1)} disabled={loading}>
                <DashboardIcon name="chart" />
                {loading ? 'Memuat...' : 'Refresh data'}
              </button>
            </div>
          </header>

          <div className={styles.detailGrid}>
            <section className={`${common.sensorPanel} ${styles.readingPanel}`} aria-label="Pembacaan realtime logger">
              <div className={common.panelHeading}>
                <div>
                  <span className={common.panelEyebrow}>REALTIME SNAPSHOT</span>
                  <h2>Pembacaan terbaru</h2>
                  <p>Nilai yang tersedia dari payload terakhir logger.</p>
                </div>
                <span className={common.panelStamp}>{formatMonitoringDate(device.lastReceivedAt ?? device.lastSensorAt)}</span>
              </div>

              {liveParameters.length > 0 ? (
                <div className={styles.categoryStack}>
                  {liveParameterGroups.map((group, index) => (
                    <section className={styles.parameterSection} key={group.key} aria-labelledby={`sensor-category-${group.key}`}>
                      <div className={styles.categoryHeading}>
                        <div className={styles.categoryHeadingCopy}>
                          <span className={styles.categoryMarker} />
                          <span className={styles.categoryEyebrow}>CATEGORY {String(index + 1).padStart(2, '0')}</span>
                          <h3 id={`sensor-category-${group.key}`}>{group.name}</h3>
                        </div>
                        <span className={styles.categoryCount}>{group.parameters.length} parameter</span>
                      </div>
                      <div className={styles.parameterGrid}>
                        {group.parameters.map(parameter => (
                          <article className={styles.parameterCard} key={parameter.code}>
                            <span className={styles.parameterLabel}>{parameter.label}</span>
                            <strong>{parameter.value == null ? '—' : formatMonitoringNumber(parameter.value)}</strong>
                            <small>{parameter.unit}</small>
                          </article>
                        ))}
                      </div>
                    </section>
                  ))}
                </div>
              ) : (
                <div className={styles.emptyReading}>
                  <DashboardIcon name="info" />
                  <div>
                    <strong>Belum ada nilai sensor pada payload terbaru</strong>
                    <span>Logger terdaftar, tetapi snapshot ini belum membawa parameter yang dapat ditampilkan.</span>
                  </div>
                </div>
              )}

              {parametersError && (
                <p className={styles.metadataNotice}>
                  Metadata parameter belum tersedia; pembacaan realtime tetap ditampilkan dengan nama umum.
                </p>
              )}
              {parametersLoading && <p className={styles.metadataNotice}>Menyempurnakan nama dan satuan parameter...</p>}
            </section>

            <aside className={styles.infoPanel} aria-label="Informasi logger">
              <div className={styles.infoHeading}>
                <span className={common.panelEyebrow}>LOGGER INFO</span>
                <span className={`${common.status} ${common['status-' + monitoringStatusTone(device.status)]}`}><i />{monitoringStatusLabel(device.status)}</span>
              </div>
              <dl className={styles.infoList}>
                <InfoRow label="Payload terakhir" value={formatMonitoringDate(device.lastReceivedAt ?? device.lastSensorAt)} />
                <InfoRow label="Usia data" value={formatDeviceAge(device.ageMinutes)} />
                <InfoRow label="Interval logger" value={device.intervalMenit > 0 ? `${device.intervalMenit} menit` : 'Tidak diatur'} />
                <InfoRow label="Sumber" value={device.sourceType ?? 'Tidak diketahui'} />
                <InfoRow label="Lokasi" value={device.lokasi ?? 'Belum diatur'} />
                <InfoRow
                  label="Koordinat"
                  value={device.latitude != null && device.longitude != null ? `${device.latitude}, ${device.longitude}` : 'Belum diatur'}
                />
              </dl>
            </aside>
          </div>

        </>
      )}
    </main>
  )
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}
