//CH
// import {
//   useCallback,
//   useEffect,
//   useMemo,
//   useState,
// } from 'react'
// import { useNavigate } from 'react-router-dom'

// import { getMonitorCurahHujan } from '../services/api'
// import type { CurahHujanMonitorItem } from '../types'
// import styles from './StasiunMonitoringPage.module.css'

// type MonitorRowWithMeta = CurahHujanMonitorItem & {
//   NamaSensor?: string | null
// }

// interface StationInfo {
//   idSensor: string
//   namaSensor: string
//   titikIpal: string
//   kodeWilayah: string
//   latitude: string
//   longitude: string
//   lastDataAt: string
//   hasData: boolean
// }

// function toYmd(date: Date): string {
//   const year = date.getFullYear()
//   const month = String(date.getMonth() + 1).padStart(2, '0')
//   const day = String(date.getDate()).padStart(2, '0')

//   return `${year}-${month}-${day}`
// }

// function fallbackSensorName(idSensor: string): string {
//   const numericId = Number(idSensor)

//   if (Number.isFinite(numericId)) {
//     return `LoggerClient${String(numericId).padStart(3, '0')}`
//   }

//   return `Logger ${idSensor}`
// }

// function buildDefaultStation(): StationInfo {
//   return {
//     idSensor: '1',
//     namaSensor: 'LoggerClient001',
//     titikIpal: 'Lokasi belum tersedia',
//     kodeWilayah: '-',
//     latitude: '-',
//     longitude: '-',
//     lastDataAt: '-',
//     hasData: false,
//   }
// }

// function buildStations(rows: MonitorRowWithMeta[]): StationInfo[] {
//   const map = new Map<string, StationInfo>()

//   for (const row of rows) {
//     const idSensor = String(row.IdSensor ?? '').trim()

//     if (!idSensor) {
//       continue
//     }

//     const current = map.get(idSensor)

//     const namaSensor =
//       String(row.NamaSensor ?? '').trim() ||
//       current?.namaSensor ||
//       fallbackSensorName(idSensor)

//     map.set(idSensor, {
//       idSensor,
//       namaSensor,
//       titikIpal:
//         String(row.TitikIpal ?? '').trim() ||
//         current?.titikIpal ||
//         'Lokasi belum tersedia',
//       kodeWilayah:
//         String(row.KodeWilayah ?? '').trim() ||
//         current?.kodeWilayah ||
//         '-',
//       latitude:
//         row.Latitude !== null &&
//         row.Latitude !== undefined &&
//         String(row.Latitude).trim() !== ''
//           ? String(row.Latitude)
//           : current?.latitude ?? '-',
//       longitude:
//         row.Longitude !== null &&
//         row.Longitude !== undefined &&
//         String(row.Longitude).trim() !== ''
//           ? String(row.Longitude)
//           : current?.longitude ?? '-',
//       lastDataAt:
//         String(row.Tanggal ?? current?.lastDataAt ?? '-'),
//       hasData: true,
//     })
//   }

//   const stations = Array.from(map.values())

//   if (!stations.some(station => station.idSensor === '1')) {
//     stations.unshift(buildDefaultStation())
//   }

//   return stations.sort((a, b) => {
//     const aNumber = Number(a.idSensor)
//     const bNumber = Number(b.idSensor)

//     if (Number.isFinite(aNumber) && Number.isFinite(bNumber)) {
//       return aNumber - bNumber
//     }

//     return a.idSensor.localeCompare(b.idSensor)
//   })
// }

// export default function StasiunMonitoringPage() {
//   const navigate = useNavigate()

//   const [rows, setRows] = useState<MonitorRowWithMeta[]>([])
//   const [loading, setLoading] = useState(true)
//   const [error, setError] = useState('')

//   const loadStations = useCallback(async () => {
//     setLoading(true)
//     setError('')

//     const end = new Date()
//     const start = new Date()

//     start.setDate(start.getDate() - 7)

//     try {
//       const result = await getMonitorCurahHujan(
//         toYmd(start),
//         toYmd(end),
//       )

//       setRows(result as MonitorRowWithMeta[])
//     } catch (err) {
//       console.error('Gagal memuat daftar stasiun:', err)
//       setRows([])
//       setError(
//         'Metadata stasiun belum dapat dimuat. Stasiun terdaftar tetap ditampilkan.',
//       )
//     } finally {
//       setLoading(false)
//     }
//   }, [])

//   useEffect(() => {
//     loadStations()
//   }, [loadStations])

//   const stations = useMemo(
//     () => buildStations(rows),
//     [rows],
//   )

//   const handleOpenStation = (station: StationInfo) => {
//     navigate(
//       `/ecs/monitor-curahhujan?station=${encodeURIComponent(
//         station.idSensor,
//       )}`,
//     )
//   }

//   return (
//     <div className={styles.page}>
//       <section className={styles.stationSection}>
//         <div className={styles.topActions}>
//           <button
//             type="button"
//             className={styles.backButton}
//             onClick={() => navigate('/ecs')}
//           >
//             ← Kembali
//           </button>
//         </div>

//         <div className={styles.sectionHeading}>
//           <div>
//             <div className={styles.sectionEyebrow}>
//               MONITORING NETWORK
//             </div>

//             <h1>Stasiun Monitoring</h1>

//             <p>
//               Pilih stasiun untuk membuka monitoring sensor secara detail.
//             </p>
//           </div>

//           <button
//             type="button"
//             className={styles.refreshButton}
//             onClick={loadStations}
//             disabled={loading}
//           >
//             {loading ? 'Memuat...' : '↻ Perbarui'}
//           </button>
//         </div>

//         {error && (
//           <div className={styles.notice}>
//             {error}
//           </div>
//         )}

//         <div className={styles.stationGrid}>
//           {stations.map((station, index) => (
//             <article
//               key={station.idSensor}
//               className={styles.stationCard}
//               onClick={() => handleOpenStation(station)}
//               onKeyDown={event => {
//                 if (event.key === 'Enter' || event.key === ' ') {
//                   event.preventDefault()
//                   handleOpenStation(station)
//                 }
//               }}
//               role="button"
//               tabIndex={0}
//             >
//               <div className={styles.stationTop}>
//                 <div className={styles.stationNumber}>
//                   {String(index + 1).padStart(2, '0')}
//                 </div>

//                 <div
//                   className={
//                     station.hasData
//                       ? styles.statusActive
//                       : styles.statusRegistered
//                   }
//                 >
//                   <span />
//                   {station.hasData ? 'AKTIF' : 'TERDAFTAR'}
//                 </div>
//               </div>

//               <div className={styles.stationIdentity}>
//                 <div className={styles.sensorIcon}>📡</div>

//                 <div>
//                   <div className={styles.stationSmall}>
//                     SENSOR STATION
//                   </div>

//                   <h2>{station.namaSensor}</h2>

//                   <div className={styles.sensorId}>
//                     ID Sensor <strong>{station.idSensor}</strong>
//                   </div>
//                 </div>
//               </div>

//               <div className={styles.locationBlock}>
//                 <div className={styles.locationMain}>
//                   <span>📍</span>

//                   <div>
//                     <small>Lokasi Stasiun</small>
//                     <strong>{station.titikIpal}</strong>
//                   </div>
//                 </div>

//                 <div className={styles.locationMeta}>
//                   <div>
//                     <span>Wilayah</span>
//                     <strong>{station.kodeWilayah}</strong>
//                   </div>

//                   <div>
//                     <span>Koordinat</span>
//                     <strong>
//                       {station.latitude}, {station.longitude}
//                     </strong>
//                   </div>
//                 </div>
//               </div>

//               <div className={styles.parameterRow}>
//                 <span>🌧 Curah Hujan</span>
//                 <span>💧 TMAK</span>
//                 <span>🌱 Kelembaban</span>
//               </div>

//               <div className={styles.cardFooter}>
//                 <div>
//                   <small>Data terakhir</small>
//                   <strong>{station.lastDataAt}</strong>
//                 </div>

//                 <span className={styles.openStation}>
//                   Lihat Monitoring →
//                 </span>
//               </div>
//             </article>
//           ))}
//         </div>
//       </section>
//     </div>
//   )
// }

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import { useNavigate } from 'react-router-dom'

import { getMonitorCurahHujan } from '../services/api'
import type { CurahHujanMonitorItem } from '../types'
import styles from './StasiunMonitoringPage.module.css'

type MonitorRowWithMeta = CurahHujanMonitorItem

interface StationInfo {
  loggerId: string
  namaLogger: string
  jumlahSensor: number
  namaSensor: string[]
  lastDataAt: string
  hasData: boolean
}

function toYmd(date: Date): string {
  const year = date.getFullYear()

  const month =
    String(date.getMonth() + 1)
      .padStart(2, '0')

  const day =
    String(date.getDate())
      .padStart(2, '0')

  return `${year}-${month}-${day}`
}

function parseDate(
  value?: string | null,
): number {

  if (!value) {
    return 0
  }

  const normalized =
    value.includes('T')
      ? value
      : value.replace(' ', 'T')

  const date =
    new Date(normalized)

  return Number.isNaN(date.getTime())
    ? 0
    : date.getTime()
}

function buildStations(
  rows: MonitorRowWithMeta[],
): StationInfo[] {

  const map =
    new Map<
      string,
      {
        loggerId: string
        namaLogger: string
        sensors: Map<string, string>
        lastDataAt: string
        lastTimestamp: number
      }
    >()

  for (const row of rows) {

    const loggerId =
      String(
        row.LoggerID ?? '',
      ).trim()

    if (!loggerId) {
      continue
    }

    const namaLogger =
      String(
        row.NamaLogger ?? '',
      ).trim() ||
      `Logger ${loggerId}`

    const sensorCode =
      String(
        row.SensorCode ?? '',
      ).trim()

    const namaSensor =
      String(
        row.NamaSensor ?? '',
      ).trim() ||
      sensorCode

    const processedAt =
      String(
        row.ProcessedAt ?? '',
      ).trim()

    const timestamp =
      parseDate(processedAt)

    let current =
      map.get(loggerId)

    if (!current) {

      current = {
        loggerId,
        namaLogger,
        sensors:
          new Map<string, string>(),
        lastDataAt: '-',
        lastTimestamp: 0,
      }

      map.set(
        loggerId,
        current,
      )
    }

    /*
     * Nama logger terbaru dari VIEW.
     */
    if (namaLogger) {
      current.namaLogger =
        namaLogger
    }

    /*
     * Sensor unik per Logger.
     */
    if (sensorCode) {
      current.sensors.set(
        sensorCode,
        namaSensor ||
          sensorCode,
      )
    }

    /*
     * Ambil ProcessedAt PALING BARU.
     */
    if (
      timestamp >
      current.lastTimestamp
    ) {
      current.lastTimestamp =
        timestamp

      current.lastDataAt =
        processedAt
    }
  }

  return Array.from(
    map.values(),
  )
    .map(item => ({
      loggerId:
        item.loggerId,

      namaLogger:
        item.namaLogger,

      jumlahSensor:
        item.sensors.size,

      namaSensor:
        Array.from(
          item.sensors.values(),
        ),

      lastDataAt:
        item.lastDataAt,

      hasData:
        item.lastTimestamp > 0,
    }))
    .sort((a, b) =>
      a.loggerId.localeCompare(
        b.loggerId,
        undefined,
        {
          numeric: true,
          sensitivity: 'base',
        },
      )
    )
}

export default function StasiunMonitoringPage() {

  const navigate =
    useNavigate()

  const [rows, setRows] =
    useState<
      MonitorRowWithMeta[]
    >([])

  const [loading, setLoading] =
    useState(true)

  const [error, setError] =
    useState('')

  const loadStations =
    useCallback(async () => {

      setLoading(true)
      setError('')

      const end =
        new Date()

      const start =
        new Date()

      /*
       * Ambil metadata/logger dari
       * data 7 hari terakhir.
       */
      start.setDate(
        start.getDate() - 7,
      )

      try {

        const result =
          await getMonitorCurahHujan(
            toYmd(start),
            toYmd(end),
          )

        setRows(result)

      } catch (err) {

        console.error(
          'Gagal memuat daftar logger:',
          err,
        )

        setRows([])

        setError(
          'Data logger belum dapat dimuat.',
        )

      } finally {

        setLoading(false)

      }
    }, [])

  useEffect(() => {
    loadStations()
  }, [loadStations])

  const stations =
    useMemo(
      () =>
        buildStations(rows),
      [rows],
    )

  const handleOpenStation = (
    station: StationInfo,
  ) => {

    navigate(
      `/ecs/monitor-curahhujan?station=${encodeURIComponent(
        station.loggerId,
      )}`,
    )
  }

  return (
    <div className={styles.page}>

      <section
        className={
          styles.stationSection
        }
      >

        <div
          className={
            styles.topActions
          }
        >
          <button
            type="button"
            className={
              styles.backButton
            }
            onClick={() =>
              navigate('/ecs')
            }
          >
            ← Kembali
          </button>
        </div>

        <div
          className={
            styles.sectionHeading
          }
        >

          <div>

            <div
              className={
                styles.sectionEyebrow
              }
            >
              MONITORING NETWORK
            </div>

            <h1>
              Stasiun Monitoring
            </h1>

            <p>
              Pilih logger/stasiun untuk membuka monitoring sensor secara detail.
            </p>

          </div>

          <button
            type="button"
            className={
              styles.refreshButton
            }
            onClick={
              loadStations
            }
            disabled={
              loading
            }
          >
            {loading
              ? 'Memuat...'
              : '↻ Perbarui'}
          </button>

        </div>

        {error && (
          <div
            className={
              styles.notice
            }
          >
            {error}
          </div>
        )}

        <div
          className={
            styles.stationGrid
          }
        >

          {stations.map(
            (station, index) => (

              <article
                key={
                  station.loggerId
                }
                className={
                  styles.stationCard
                }
                onClick={() =>
                  handleOpenStation(
                    station,
                  )
                }
                onKeyDown={event => {

                  if (
                    event.key ===
                      'Enter' ||
                    event.key ===
                      ' '
                  ) {
                    event.preventDefault()

                    handleOpenStation(
                      station,
                    )
                  }
                }}
                role="button"
                tabIndex={0}
              >

                <div
                  className={
                    styles.stationTop
                  }
                >

                  <div
                    className={
                      styles.stationNumber
                    }
                  >
                    {String(
                      index + 1,
                    ).padStart(
                      2,
                      '0',
                    )}
                  </div>

                  <div
                    className={
                      station.hasData
                        ? styles.statusActive
                        : styles.statusRegistered
                    }
                  >
                    <span />

                    {station.hasData
                      ? 'AKTIF'
                      : 'TERDAFTAR'}
                  </div>

                </div>

                <div
                  className={
                    styles.stationIdentity
                  }
                >

                  <div
                    className={
                      styles.sensorIcon
                    }
                  >
                    📡
                  </div>

                  <div>

                    <div
                      className={
                        styles.stationSmall
                      }
                    >
                      LOGGER STATION
                    </div>

                    <h2>
                      {
                        station.namaLogger
                      }
                    </h2>

                    <div
                      className={
                        styles.sensorId
                      }
                    >
                      Logger ID{' '}
                      <strong>
                        {
                          station.loggerId
                        }
                      </strong>
                    </div>

                  </div>

                </div>

                <div
                  className={
                    styles.locationBlock
                  }
                >

                  <div
                    className={
                      styles.locationMain
                    }
                  >

                    <span>
                      📊
                    </span>

                    <div>
                      <small>
                        Parameter Sensor
                      </small>

                      <strong>
                        {
                          station.jumlahSensor
                        }{' '}
                        Sensor
                      </strong>
                    </div>

                  </div>

                  <div
                    className={
                      styles.locationMeta
                    }
                  >

                    <div>
                      <span>
                        Logger
                      </span>

                      <strong>
                        {
                          station.namaLogger
                        }
                      </strong>
                    </div>

                    <div>
                      <span>
                        Jumlah Parameter
                      </span>

                      <strong>
                        {
                          station.jumlahSensor
                        }
                      </strong>
                    </div>

                  </div>

                </div>

                <div
                  className={
                    styles.parameterRow
                  }
                >

                  {station.namaSensor
                    .slice(0, 3)
                    .map(sensor => (
                      <span
                        key={sensor}
                      >
                        {sensor}
                      </span>
                    ))}

                  {station.namaSensor
                    .length > 3 && (
                    <span>
                      +
                      {station
                        .namaSensor
                        .length - 3}{' '}
                      lainnya
                    </span>
                  )}

                </div>

                <div
                  className={
                    styles.cardFooter
                  }
                >

                  <div>
                    <small>
                      Data terakhir
                    </small>

                    <strong>
                      {
                        station.lastDataAt
                      }
                    </strong>
                  </div>

                  <span
                    className={
                      styles.openStation
                    }
                  >
                    Lihat Monitoring →
                  </span>

                </div>

              </article>
            ),
          )}

        </div>

      </section>

    </div>
  )
}