import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent as ReactMouseEvent, type ReactNode } from 'react'
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,} from 'recharts'
import { getMonitorCurahHujan } from '../services/api'
import type { CurahHujanMonitorItem } from '../types'
import { todayStr } from '../utils/format'
import styles from './CurahHujanMonitorPage.module.css'
import { buildStationOptions, exportMonitoringCsv, exportMonitoringXlsx, prepareDownloadRows, type DownloadFormat, type DownloadInterval,} from '../utils/downloadMonitoring'
// import { useNavigate } from 'react-router-dom'
import { useNavigate, useSearchParams,} from 'react-router-dom'
import { stationUrl } from '../utils/station'
import PageNavigation from '../components/PageNavigation'

//CH
// type MonitorPoint = {
//   ts: number
//   waktu: string

//   curahHujan: number | null

//   // hasil keseluruhan jam, untuk CARD
//   intensitasHujan: number | null

//   // hasil per-record, untuk GRAFIK
//   intensitasHujanPerData: number | null

//   kelembapanTanah: number | null
//   tinggiMukaAir: number | null

//   jumlahDataPerJam: number | null
//   isGap?: boolean
// }
type SensorPoint = {
  ts: number
  waktu: string
  value: number | null
  isGap?: boolean
}

type SensorSeries = {
  sensorCode: string
  namaSensor: string
  satuan: string
  points: SensorPoint[]
}


//CH
// function buildSensorPoints(
//   rows: CurahHujanMonitorItem[],
// ): MonitorPoint[] {

//   // ==========================================
//   // STEP 1 - PARSE DATA
//   // ==========================================
//   const parsed = rows
//     .map(row => {
//       const date =
//         parseTanggal(row.Tanggal)

//       if (!date) {
//         return null
//       }

//       const nilaiCurahHujan =
//         Number(
//           row.NilaiCurahHujan ?? 0,
//         ) || 0

//       // Curah Hujan =
//       // NilaiCurahHujan × 0.7
//       const curahHujan =
//         nilaiCurahHujan *
//         FAKTOR_KOREKSI

//       const kelembapanTanah =
//         Number(
//           row.NilaiKelembapanTanah ?? 0,
//         ) || 0

//       const nilaiTinggiAir =
//         Number(
//           row.NilaiTinggiAir ?? 0,
//         ) || 0

//       // TMAK =
//       // 30 - NilaiTinggiAir
//       const tinggiMukaAir =
//         30 - nilaiTinggiAir

//       // Jam kalender
//       // contoh:
//       // 2026-09-12-07
//       const hourKey =
//         `${date.getFullYear()}-` +
//         `${String(
//           date.getMonth() + 1,
//         ).padStart(2, '0')}-` +
//         `${String(
//           date.getDate(),
//         ).padStart(2, '0')}-` +
//         `${String(
//           date.getHours(),
//         ).padStart(2, '0')}`

//       return {
//         ts:
//           date.getTime(),

//         waktu:
//           `${String(
//             date.getHours(),
//           ).padStart(2, '0')}:` +
//           `${String(
//             date.getMinutes(),
//           ).padStart(2, '0')}`,

//         hourKey,

//         curahHujan:
//           Number(
//             curahHujan.toFixed(4),
//           ),

//         kelembapanTanah:
//           Number(
//             kelembapanTanah.toFixed(2),
//           ),

//         tinggiMukaAir:
//           Number(
//             tinggiMukaAir.toFixed(2),
//           ),
//       }
//     })
//     .filter(
//       (
//         item,
//       ): item is NonNullable<
//         typeof item
//       > =>
//         item !== null,
//     )

//   // ==========================================
//   // STEP 2 - KELOMPOKKAN PER JAM KALENDER
//   // ==========================================
//   const hourly = new Map<
//     string,
//     {
//       totalCurah: number
//       jumlahData: number
//     }
//   >()

//   for (
//     const item of parsed
//   ) {
//     const existing =
//       hourly.get(
//         item.hourKey,
//       ) ?? {
//         totalCurah: 0,
//         jumlahData: 0,
//       }

//     existing.totalCurah +=
//       item.curahHujan

//     existing.jumlahData += 1

//     hourly.set(
//       item.hourKey,
//       existing,
//     )
//   }

//   // ==========================================
//   // STEP 3 - HITUNG INTENSITAS
//   // ==========================================
//   const result: MonitorPoint[] =
//     parsed.map(item => {

//       const group =
//         hourly.get(
//           item.hourKey,
//         )

//       const jumlahData =
//         group?.jumlahData ?? 0

//       const totalCurah =
//         group?.totalCurah ?? 0

//       // ======================================
//       // CARD INTENSITAS
//       //
//       // SUM Curah Hujan dalam jam / n
//       // ======================================
//       const intensitasHujan =
//         jumlahData > 0
//           ? totalCurah /
//             jumlahData
//           : 0

//       // ======================================
//       // GRAFIK INTENSITAS PER DATA
//       //
//       // Curah Hujan record / n
//       // ======================================
//       const intensitasHujanPerData =
//         jumlahData > 0
//           ? item.curahHujan /
//             jumlahData
//           : 0

//       return {
//         ts:
//           item.ts,

//         waktu:
//           item.waktu,

//         curahHujan:
//           item.curahHujan,

//         // CARD
//         intensitasHujan:
//           Number(
//             intensitasHujan.toFixed(
//               4,
//             ),
//           ),

//         // GRAFIK
//         intensitasHujanPerData:
//           Number(
//             intensitasHujanPerData.toFixed(
//               4,
//             ),
//           ),

//         kelembapanTanah:
//           item.kelembapanTanah,

//         tinggiMukaAir:
//           item.tinggiMukaAir,

//         jumlahDataPerJam:
//           jumlahData,
//       }
//     })

//   // ==========================================
//   // STEP 4 - URUTKAN BERDASARKAN WAKTU
//   // ==========================================
//   return result.sort(
//     (a, b) =>
//       a.ts - b.ts,
//   )
// }

//BR
function parseProcessedAt(
  value?: string | null,
): Date | null {

  if (!value) {
    return null
  }

  const normalized =
    value.includes('T')
      ? value
      : value.replace(
          ' ',
          'T',
        )

  const date =
    new Date(normalized)

  if (
    Number.isNaN(
      date.getTime(),
    )
  ) {
    return null
  }

  return date
}

function formatTime(
  date: Date,
): string {

  return (
    `${String(
      date.getHours(),
    ).padStart(2, '0')}:` +
    `${String(
      date.getMinutes(),
    ).padStart(2, '0')}`
  )
}

function buildSensorSeries(
  rows: CurahHujanMonitorItem[],
): SensorSeries[] {

  const map =
    new Map<
      string,
      SensorSeries
    >()

  for (const row of rows) {

    const sensorCode =
      String(
        row.SensorCode ?? '',
      ).trim()

    if (!sensorCode) {
      continue
    }

    const date =
      parseProcessedAt(
        row.ProcessedAt,
      )

    if (!date) {
      continue
    }

    /*
     * PENTING:
     *
     * NilaiHasil langsung dari VIEW.
     * Tidak ada rumus lagi.
     */
    const value =
      Number(
        row.NilaiHasil,
      )

    if (
      !Number.isFinite(
        value,
      )
    ) {
      continue
    }

    const namaSensor =
      String(
        row.NamaSensor ?? '',
      ).trim() ||
      sensorCode

    const satuan =
      String(
        row.Satuan ??
        row.OutputUnit ??
        '',
      ).trim()

    let series =
      map.get(sensorCode)

    if (!series) {

      series = {
        sensorCode,
        namaSensor,
        satuan,
        points: [],
      }

      map.set(
        sensorCode,
        series,
      )
    }

    series.points.push({
      ts:
        date.getTime(),

      waktu:
        formatTime(date),

      value,

      isGap:
        false,
    })
  }

  /*
   * Urutkan timestamp masing-masing sensor.
   */
  for (
    const series of
      map.values()
  ) {

    series.points.sort(
      (a, b) =>
        a.ts - b.ts,
    )
  }

  return Array.from(
    map.values(),
  )
}
//CH
//   function buildChartWithGaps(
//   points: MonitorPoint[],
// ): MonitorPoint[] {
//   if (points.length === 0) {
//     return []
//   }

//   const sorted = [...points].sort(
//     (a, b) => a.ts - b.ts,
//   )

//   const result: MonitorPoint[] = []

//   for (
//     let i = 0;
//     i < sorted.length;
//     i += 1
//   ) {
//     const current = sorted[i]

//     if (i > 0) {
//       const previous = sorted[i - 1]

//       const delta =
//         current.ts - previous.ts
//       if (
//         delta >
//         MAX_INTERVAL_MS
//       ) {
//         const gapTs =
//           previous.ts +
//           MAX_INTERVAL_MS

//         const gapDate =
//           new Date(gapTs)

//         result.push({
//           ts: gapTs,

//           waktu:
//             formatTime(gapDate),

//           // Curah Hujan
//           curahHujan:
//             null,

//           // Intensitas untuk CARD
//           intensitasHujan:
//             null,

//           // Intensitas per data
//           // untuk GRAFIK
//           intensitasHujanPerData:
//             null,

//           // Kelembaban Tanah
//           kelembapanTanah:
//             null,

//           // TMAK
//           tinggiMukaAir:
//             null,

//           // jumlah data dalam jam
//           jumlahDataPerJam:
//             null,

//           isGap:
//             true,
//         })
//       }
//     }

//     // data asli tetap dimasukkan
//     result.push(current)
//   }

//   return result
// }

/**
 * Label sumbu X tetap 2 jam sekali:
 * 00, 02, 04, ..., 22.
 *
 * Data chart TIDAK dibulatkan ke 2 jam.
 * Titik 10:05 tetap berada di posisi 10:05,
 * titik 11:10 tetap berada di posisi 11:10.
 */

const MAX_INTERVAL_MS =
  5 * 60 * 1000

function buildChartWithGaps(
  points: SensorPoint[],
): SensorPoint[] {

  if (
    points.length === 0
  ) {
    return []
  }

  const sorted =
    [...points].sort(
      (a, b) =>
        a.ts - b.ts,
    )

  const result:
    SensorPoint[] = []

  for (
    let i = 0;
    i < sorted.length;
    i += 1
  ) {

    const current =
      sorted[i]

    if (i > 0) {

      const previous =
        sorted[i - 1]

      const delta =
        current.ts -
        previous.ts

      if (
        delta >
        MAX_INTERVAL_MS
      ) {

        const gapTs =
          previous.ts +
          MAX_INTERVAL_MS

        result.push({
          ts:
            gapTs,

          waktu:
            formatTime(
              new Date(
                gapTs,
              ),
            ),

          value:
            null,

          isGap:
            true,
        })
      }
    }

    result.push(
      current,
    )
  }

  return result
}
function buildTwoHourTicks(
  tanggal: string,
): number[] {

  const start =
    new Date(
      `${tanggal}T00:00:00`,
    )

  return Array.from(
    { length: 12 },
    (_, index) => {

      const d =
        new Date(start)

      d.setHours(
        index * 2,
        0,
        0,
        0,
      )

      return d.getTime()
    },
  )
}

function formatHourTick(
  ts: number,
): string {
  const date = new Date(ts)

  return String(
    date.getHours(),
  ).padStart(2, '0')
}

function formatTooltipDate(
  ts: number,
): string {
  const d = new Date(ts)

  if (Number.isNaN(d.getTime())) {
    return '-'
  }

  const year = d.getFullYear()
  const month = String(
    d.getMonth() + 1,
  ).padStart(2, '0')
  const day = String(
    d.getDate(),
  ).padStart(2, '0')
  const hour = String(
    d.getHours(),
  ).padStart(2, '0')
  const minute = String(
    d.getMinutes(),
  ).padStart(2, '0')

  return (
    `${year}-${month}-${day} ` +
    `${hour}:${minute}`
  )
}

interface ChartCardProps {
  title: string
  unit: string
  color: string
  latestValue?: string
  children: ReactNode
}

function ChartCard({
  title,
  unit,
  color,
  latestValue,
  children,
}: ChartCardProps) {
  return (
    <div className={styles.chartCard}>
      <div className={styles.chartHeader}>
        <span
          className={styles.chartTitle}
          style={{ color }}
        >
          {title} ({unit})
        </span>

        {latestValue && (
          <span className={styles.chartLatest}>
            {latestValue}
          </span>
        )}
      </div>

      {children}
    </div>
  )
}

// function handleInfoMouseDown(
//   e: ReactMouseEvent<HTMLDivElement>
// ) {
//   // Jangan mulai drag kalau yang diklik tombol
//   if ((e.target as HTMLElement).closest('button')) {
//     return
//   }

//   e.preventDefault()

//   dragInfoRef.current = {
//     startX: e.clientX,
//     startY: e.clientY,
//     initialX: infoPosition.x,
//     initialY: infoPosition.y,
//   }

//   function handleMouseMove(event: MouseEvent) {
//     const deltaX =
//       event.clientX - dragInfoRef.current.startX

//     const deltaY =
//       event.clientY - dragInfoRef.current.startY

//     setInfoPosition({
//       x: dragInfoRef.current.initialX + deltaX,
//       y: dragInfoRef.current.initialY + deltaY,
//     })
//   }

//   function handleMouseUp() {
//     document.removeEventListener(
//       'mousemove',
//       handleMouseMove
//     )

//     document.removeEventListener(
//       'mouseup',
//       handleMouseUp
//     )
//   }

//   document.addEventListener(
//     'mousemove',
//     handleMouseMove
//   )

//   document.addEventListener(
//     'mouseup',
//     handleMouseUp
//   )
// }

function InfoSection({
  title,
  description,
  rows,
}: {
  title: string
  description: string
  rows: [string, string][]
}) {
  return (
    <section>
      <h3
        style={{
          margin: '0 0 4px',
          fontSize: 15,
          color: '#18784f',
        }}
      >
        {title}
      </h3>

      <div
        style={{
          marginBottom: 10,
          fontSize: 12,
          color: '#708079',
        }}
      >
        {description}
      </div>

      <div
        style={{
          border: '1px solid #e4ebe7',
          borderRadius: 8,
          overflow: 'hidden',
        }}
      >
        {rows.map(([label, value], index) => (
          <div
            key={label}
            style={{
              display: 'grid',
              gridTemplateColumns: '1fr 180px',
              padding: '9px 12px',
              borderTop:
                index === 0
                  ? 'none'
                  : '1px solid #edf1ef',
            }}
          >
            <span>{label}</span>

            <strong
              style={{
                textAlign: 'right',
                color: '#273b32',
              }}
            >
              {value}
            </strong>
          </div>
        ))}
      </div>
    </section>
  )
}

type SensorKey =
  | 'intensitasHujan'
  | 'intensitasHujanPerData'
  | 'curahHujan'
  | 'kelembapanTanah'
  | 'tinggiMukaAir'
//CH
// interface SensorChartProps {
//   data: MonitorPoint[]
//   ticks: number[]

//   minTs: number
//   maxTs: number

//   dataKey: SensorKey

//   name: string
//   unit: string
//   stroke: string

//   domain?: [
//     number | 'auto',
//     number | 'auto',
//   ]
// }

// function SensorChart({
//   data,
//   ticks,
//   minTs,
//   maxTs,
//   dataKey,
//   name,
//   unit,
//   stroke,
//   domain,
// }: SensorChartProps) {

//   return (
//     <ResponsiveContainer
//       width="100%"
//       height={220}
//     >
//       <LineChart
//         data={data}
//         margin={{
//           top: 8,
//           right: 12,
//           left: -8,
//           bottom: 4,
//         }}
//       >
//         <CartesianGrid
//           strokeDasharray="3 3"
//           vertical={false}
//           stroke="#eceff1"
//         />

//         <XAxis
//           dataKey="ts"
//           type="number"
//           scale="time"

//           domain={[
//             minTs,
//             maxTs,
//           ]}

//           ticks={ticks}

//           tickFormatter={
//             formatHourTick
//           }

//           interval={0}

//           tick={{
//             fontSize: 10,
//             fill: '#666',
//           }}

//           tickLine={false}

//           axisLine={{
//             stroke: '#777',
//           }}
//         />

//         <YAxis
//           tick={{
//             fontSize: 10,
//             fill: '#666',
//           }}

//           domain={domain}

//           width={48}

//           tickLine={false}

//           axisLine={{
//             stroke: '#777',
//           }}
//         />

//         <Tooltip
//           labelFormatter={label =>
//             formatTooltipDate(
//               Number(label),
//             )
//           }

//           formatter={value => {

//             if (
//               value === null ||
//               value === undefined
//             ) {
//               return [
//                 '-',
//                 name,
//               ]
//             }

//             const n =
//               Number(value)

//             return [
//               `${n.toFixed(2)} ${unit}`,
//               name,
//             ]
//           }}
//         />

//         <Line
//           type="linear"

//           dataKey={dataKey}

//           name={name}

//           stroke={stroke}

//           strokeWidth={2}

//           // ======================
//           // TIDAK ADA BULAT-BULAT
//           // ======================
//           dot={false}

//           // hanya muncul saat hover
//           activeDot={{
//             r: 4,
//           }}

//           // ======================
//           // NULL = GARIS TERPUTUS
//           // ======================
//           connectNulls={false}

//           isAnimationActive={false}
//         />
//       </LineChart>
//     </ResponsiveContainer>
//   )
// }

  //BR
  interface SensorChartProps {
  data: SensorPoint[]
  ticks: number[]

  minTs: number
  maxTs: number

  name: string
  unit: string
  stroke: string
}
function SensorChart({
  data,
  ticks,
  minTs,
  maxTs,
  name,
  unit,
  stroke,
}: SensorChartProps) {

  return (
    <ResponsiveContainer
      width="100%"
      height={220}
    >

      <LineChart
        data={data}
        margin={{
          top: 8,
          right: 12,
          left: -8,
          bottom: 4,
        }}
      >

        <CartesianGrid
          strokeDasharray="3 3"
          vertical={false}
          stroke="#eceff1"
        />

        <XAxis
          dataKey="ts"
          type="number"
          scale="time"
          domain={[
            minTs,
            maxTs,
          ]}
          ticks={ticks}
          tickFormatter={
            formatHourTick
          }
          interval={0}
          tick={{
            fontSize: 10,
            fill: '#666',
          }}
          tickLine={false}
          axisLine={{
            stroke: '#777',
          }}
        />

        <YAxis
          tick={{
            fontSize: 10,
            fill: '#666',
          }}
          width={48}
          tickLine={false}
          axisLine={{
            stroke: '#777',
          }}
          domain={[
            'auto',
            'auto',
          ]}
        />

        <Tooltip
          labelFormatter={label =>
            formatTooltipDate(
              Number(label),
            )
          }

          formatter={value => {

            if (
              value === null ||
              value === undefined
            ) {
              return [
                '-',
                name,
              ]
            }

            const n =
              Number(value)

            return [
              `${n.toFixed(2)} ${unit}`,
              name,
            ]
          }}
        />

        <Line
          type="linear"

          dataKey="value"

          name={name}

          stroke={stroke}

          strokeWidth={2}

          dot={false}

          activeDot={{
            r: 4,
          }}

          connectNulls={false}

          isAnimationActive={false}
        />

      </LineChart>

    </ResponsiveContainer>
  )
}

export default function CurahHujanMonitorPage() {
  // const navigate = useNavigate()

  // const [searchParams] =
  //   useSearchParams()

  // const stationId =
  //   searchParams.get('station')

  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const stationId = searchParams.get('station') ?? '1'
  const [tanggal, setTanggal] = useState(
    todayStr(),
  )

  const [loading, setLoading] =
    useState(false)

  const [error, setError] =
    useState('')

  const [lastUpdate, setLastUpdate] =
    useState('')

  const [autoRefresh, setAutoRefresh] =
    useState(false)

  const [sensorData, setSensorData] =
    useState<CurahHujanMonitorItem[]>([])

    //state download data
    const [
        downloadOpen,
        setDownloadOpen,
      ] = useState(false)

      const [
        downloadInterval,
        setDownloadInterval,
      ] = useState<
        DownloadInterval | ''
      >('')

      const [
        downloadStation,
        setDownloadStation,
      ] = useState('')

      const [
        downloadFormat,
        setDownloadFormat,
      ] = useState<
        DownloadFormat | ''
      >('')

      const [
        downloadStart,
        setDownloadStart,
      ] = useState(tanggal)

      const [
        downloadEnd,
        setDownloadEnd,
      ] = useState(tanggal)

      const [
        downloadLoading,
        setDownloadLoading,
      ] = useState(false)

      const [
        downloadError,
        setDownloadError,
      ] = useState('')

      const stationOptions =
        useMemo(
          () =>
            buildStationOptions(
              sensorData,
            ),
          [sensorData],
        )
      const fetchData =
      useCallback(
        async () => {
          try {
        setLoading(true)
        setError('')
        const result = await getMonitorCurahHujan(tanggal, tanggal)

        const filteredResult = result.filter(
          row =>
            String(row.LoggerID ?? '') ===
            String(stationId),
        )

        setSensorData(filteredResult)

        setLastUpdate(
          new Date()
            .toLocaleString(
              'id-ID',
            ),
        )
      } catch (err) {
        console.error(
          'Gagal memuat monitoring sensor:',
          err,
        )

        setSensorData([])

        setError(
          'Gagal memuat data monitoring.',
        )
      } finally {
        setLoading(false)
      }
    },
    [
      tanggal,
      stationId,
    ],
  )


  useEffect(() => {
    fetchData()
  }, [fetchData])

  useEffect(() => {
    if (!autoRefresh) {
      return
    }

    const id = window.setInterval(
      fetchData,
      5 * 60 * 1000,
    )

    return () => {
      window.clearInterval(id)
    }
  }, [autoRefresh, fetchData])

  // ==========================================
  // DOWNLOAD DATA
  // ==========================================
  const handleOpenDownload = () => {
    setDownloadStart(tanggal)
    setDownloadEnd(tanggal)
    setDownloadInterval('5m')
    setDownloadFormat('xlsx')
    setDownloadStation('')
    setDownloadError('')
    setDownloadOpen(true)
  }

  const handleCloseDownload = () => {
    if (downloadLoading) {
      return
    }

    setDownloadOpen(false)
    setDownloadError('')
  }

  const [showInfo, setShowInfo] = useState(false)

  const [infoPosition, setInfoPosition] = useState({
    x: 0,
    y: 0,
  })

  const dragInfoRef = useRef({
    startX: 0,
    startY: 0,
    initialX: 0,
    initialY: 0,
  })

  const handleInfoMouseDown = (
  e: ReactMouseEvent<HTMLDivElement>
) => {
  // Jangan drag ketika tombol close diklik
  if ((e.target as HTMLElement).closest('button')) {
    return
  }

  e.preventDefault()

  dragInfoRef.current = {
    startX: e.clientX,
    startY: e.clientY,
    initialX: infoPosition.x,
    initialY: infoPosition.y,
  }

  const handleMouseMove = (event: MouseEvent) => {
    const deltaX =
      event.clientX -
      dragInfoRef.current.startX

    const deltaY =
      event.clientY -
      dragInfoRef.current.startY

    setInfoPosition({
      x:
        dragInfoRef.current.initialX +
        deltaX,

      y:
        dragInfoRef.current.initialY +
        deltaY,
    })
  }

  const handleMouseUp = () => {
    document.removeEventListener(
      'mousemove',
      handleMouseMove
    )

    document.removeEventListener(
      'mouseup',
      handleMouseUp
    )
  }

  document.addEventListener(
    'mousemove',
    handleMouseMove
  )

  document.addEventListener(
    'mouseup',
    handleMouseUp
  )
}

  const handleDownloadData = async () => {
    setDownloadError('')

    if (!downloadInterval) {
      setDownloadError('Silakan pilih interval data.')
      return
    }

    if (!downloadStation) {
      setDownloadError('Silakan pilih stasiun.')
      return
    }

    if (!downloadFormat) {
      setDownloadError('Silakan pilih format data.')
      return
    }

    if (!downloadStart || !downloadEnd) {
      setDownloadError(
        'Tanggal mulai dan tanggal selesai wajib diisi.',
      )
      return
    }

    if (downloadEnd < downloadStart) {
      setDownloadError(
        'Tanggal selesai tidak boleh lebih kecil dari tanggal mulai.',
      )
      return
    }

    setDownloadLoading(true)

    try {
      const rows = await getMonitorCurahHujan(
        downloadStart,
        downloadEnd,
      )

      const exportRows = prepareDownloadRows(
        rows,
        downloadInterval,
        downloadStation,
      )

      if (exportRows.length === 0) {
        setDownloadError(
          'Tidak ada data pada stasiun dan periode yang dipilih.',
        )
        return
      }

      const intervalLabel: Record<
        DownloadInterval,
        string
      > = {
        '5m': '5menit',
        hour: 'perjam',
        day: 'harian',
        month: 'bulanan',
        year: 'tahunan',
      }

      const filename =
        `ecs-monitoring_${downloadStart}_` +
        `${downloadEnd}_` +
        `${intervalLabel[downloadInterval]}`

      if (downloadFormat === 'csv') {
        exportMonitoringCsv(
          exportRows,
          filename,
        )
      } else {
        exportMonitoringXlsx(
          exportRows,
          filename,
        )
      }

      setDownloadOpen(false)
    } catch (err) {
      console.error(
        'Gagal download data:',
        err,
      )

      setDownloadError(
        'Gagal mengambil data untuk download.',
      )
    } finally {
      setDownloadLoading(false)
    }
  }

  // ==========================================
  // DATA SENSOR DINAMIS BERDASARKAN LOGGER
  // ==========================================
  const sensorSeries = useMemo(
    () => buildSensorSeries(sensorData),
    [sensorData],
  )

  const chartSeries = useMemo(
    () =>
      sensorSeries.map(sensor => ({
        ...sensor,
        points: buildChartWithGaps(sensor.points),
      })),
    [sensorSeries],
  )

  const loggerInfo = useMemo(
    () =>
      sensorData.find(
        row => String(row.NamaLogger ?? '').trim() !== '',
      ),
    [sensorData],
  )

  const namaLogger = String(
    loggerInfo?.NamaLogger ?? `Logger ${stationId}`,
  )

  const dayStartTs = useMemo(
    () => new Date(`${tanggal}T00:00:00`).getTime(),
    [tanggal],
  )

  const dayEndTs = useMemo(
    () => new Date(`${tanggal}T23:59:59`).getTime(),
    [tanggal],
  )

  const xTicks = useMemo(
    () => buildTwoHourTicks(tanggal),
    [tanggal],
  )

  const chartColors = [
    '#1565c0',
    '#2e7d32',
    '#6a1b9a',
    '#ef6c00',
    '#00838f',
    '#c62828',
  ]

  return (
    <div className={styles.page}>
      {/* HEADER */}
       {/* <PageNavigation
            backTo={`/ecs/monitor-curahhujan?station=${encodeURIComponent(stationId)}`}
            backLabel={`Monitor Stasiun ${stationId}`}
          />*/}
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <span
            className={`${styles.dot} ${
              loading
                ? styles.dotLoading
                : styles.dotOnline
            }`}
          />
          <div>
            {/*<div className={styles.headerTitle}>
             RSUP Perkebunan :{' '}
              <strong>
                {sensorInfo?.TitikIpal ?? '-'}
              </strong>

              {' - '}

              <strong>
                {sensorInfo?.NamaSensor ?? '-'}
              </strong>

            </div>*/}
            <div className={styles.headerTitle}>
              {namaLogger}
            </div>
            <div className={styles.headerSub}>
              Logger ID {stationId}
              {' • '}
              {sensorSeries.length} sensor aktif
            </div>

            <div className={styles.headerSub}>
              {lastUpdate
                ? `Update: ${lastUpdate} | Data aktual sensor`
                : 'Memuat data...'}
            </div>
          </div>
        </div>
          {/* button */}

       <div
          className={
            styles.headerRight
          }
        >
         {/* <label
            className={
              styles.autoLabel
            }
          >
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={e =>
                setAutoRefresh(
                  e.target.checked,
                )
              }
            />

            Auto Refresh (5 mnt)
          </label>*/}
          {/*     <PageNavigation
            backTo={`/ecs/monitor-curahhujan?station=${encodeURIComponent(stationId)}`}
            backLabel={`Monitor Stasiun ${stationId}`}
          />*/}

          <div
            style={{
              display: 'flex',
              gap: 8,
              flexWrap: 'wrap',
              justifyContent: 'flex-end',
            }}
          >
            {/*<PageNavigation
              backTo="/ecs/stasiun-monitoring"
              backLabel="Kembali"
            />*/}
           <button
              type="button"
              className={styles.btnDownload}
              onClick={() => {
                setInfoPosition({
                  x: 0,
                  y: 0,
                })

                setShowInfo(true)
              }}
            >
              ⓘ Informasi
            </button>
            <button
              type="button"
              className={styles.btnDownload}
              // onClick={() =>
              //   navigate('/ecs/data-historis')
              // }
              onClick={() =>
              navigate(stationUrl('/ecs/data-historis', stationId))}
             >
              📈 Data Historis
            </button>

            <button
              type="button"
              className={styles.btnDownload}
              // onClick={() =>
              //   navigate(
              //     '/ecs/kalender-curah-hujan',
              //   )
              // }
              onClick={() =>
            navigate(stationUrl('/ecs/kalender-curah-hujan', stationId))}
            >
              🗓 Kalender Curah Hujan
            </button>

            <button
              type="button"
              className={styles.btnDownload}
              onClick={
                handleOpenDownload
              }
            >
              ↓ Download Data
            </button>
          </div>
        </div>
      </div>

        {/* MODAL DOWNLOAD DATA */}
        {downloadOpen && (
          <div
            className={styles.downloadOverlay}
            onMouseDown={e => {
              if (e.target === e.currentTarget) {
                handleCloseDownload()
              }
            }}
          >
            <div className={styles.downloadModal}>
              <div className={styles.downloadModalHeader}>
                <div>
                  <h2>Download Data</h2>
                  <p>Permintaan Data Sensor</p>
                </div>

                <button
                  type="button"
                  className={styles.downloadClose}
                  onClick={handleCloseDownload}
                  aria-label="Tutup"
                >
                  ×
                </button>
              </div>

              <div className={styles.downloadSectionTitle}>
                Permintaan Data
              </div>

              <div className={styles.downloadGrid}>
                {/* INTERVAL */}
                <div className={styles.downloadField}>
                  <label>Interval</label>

                  <select
                    value={downloadInterval}
                    onChange={e => {
                      const value = e.currentTarget.value

                      if (
                        value === '' ||
                        value === '5m' ||
                        value === 'hour' ||
                        value === 'day' ||
                        value === 'month' ||
                        value === 'year'
                      ) {
                        setDownloadInterval(value)
                      }
                    }}
                  >
                    <option value="">
                      Pilih Interval
                    </option>
                    <option value="5m">
                      Per 5 Menit
                    </option>
                    <option value="hour">
                      Per Jam
                    </option>
                    <option value="day">
                      Harian
                    </option>
                    <option value="month">
                      Bulanan
                    </option>
                    <option value="year">
                      Tahunan
                    </option>
                  </select>
                </div>

                {/* STASIUN */}
                <div className={styles.downloadField}>
                  <label>Stasiun</label>

                  <select
                    value={downloadStation}
                    onChange={e =>
                      setDownloadStation(
                        e.currentTarget.value,
                      )
                    }
                  >
                    <option value="">
                      Pilih Stasiun
                    </option>

                    {stationOptions.map(station => (
                      <option
                        key={station.value}
                        value={station.value}
                      >
                        {station.label}
                      </option>
                    ))}
                  </select>
                </div>

                {/* FORMAT DATA */}
                <div className={styles.downloadField}>
                  <label>Format Data</label>

                  <select
                    value={downloadFormat}
                    onChange={e => {
                      const value = e.currentTarget.value

                      if (
                        value === '' ||
                        value === 'csv' ||
                        value === 'xlsx'
                      ) {
                        setDownloadFormat(value)
                      }
                    }}
                  >
                    <option value="">
                      Pilih Format
                    </option>
                    <option value="csv">
                      CSV
                    </option>
                    <option value="xlsx">
                      XLSX
                    </option>
                  </select>
                </div>

                {/* TANGGAL MULAI */}
                <div className={styles.downloadField}>
                  <label>Tanggal Mulai</label>

                  <input
                    type="date"
                    value={downloadStart}
                    onChange={e =>
                      setDownloadStart(
                        e.currentTarget.value,
                      )
                    }
                  />
                </div>

                {/* TANGGAL SELESAI */}
                <div className={styles.downloadField}>
                  <label>Tanggal Selesai</label>

                  <input
                    type="date"
                    value={downloadEnd}
                    onChange={e =>
                      setDownloadEnd(
                        e.currentTarget.value,
                      )
                    }
                  />
                </div>

                {/* DATA TERSEDIA */}
                <div className={styles.downloadField}>
                  <label>Data tersedia:</label>

                  <div className={styles.dataAvailable}>
                    {!downloadStation
                      ? 'Pilih Stasiun Terlebih Dahulu'
                      : downloadStart && downloadEnd
                        ? `${downloadStart} s.d. ${downloadEnd}`
                        : 'Pilih tanggal mulai dan selesai'}
                  </div>
                </div>
              </div>

              {downloadError && (
                <div className={styles.downloadError}>
                  {downloadError}
                </div>
              )}

              <div className={styles.downloadActions}>
                <button
                  type="button"
                  className={styles.btnCancelDownload}
                  disabled={downloadLoading}
                  onClick={handleCloseDownload}
                >
                  Batal
                </button>

                <button
                  type="button"
                  className={styles.btnSubmitDownload}
                  disabled={downloadLoading}
                  onClick={handleDownloadData}
                >
                  {downloadLoading
                    ? 'Menyiapkan...'
                    : '↓ Download'}
                </button>
              </div>
            </div>
          </div>
        )}
        {/* END MODAL DOWNLOAD DATA */}

        {/*Modal Informasi*/}
          {showInfo && (
  <div
    onClick={() => setShowInfo(false)}
    style={{
      position: 'fixed',
      inset: 0,
      zIndex: 9999,

      background: 'rgba(0, 0, 0, 0.4)',

      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',

      padding: 20,
      overflow: 'hidden',
    }}
  >
    <div
      onClick={e => e.stopPropagation()}
      style={{
        width: 'min(760px, calc(100vw - 40px))',
        maxHeight: '85vh',

        background: '#fff',
        borderRadius: 14,

        boxShadow:
          '0 20px 60px rgba(0,0,0,.25)',

        overflow: 'hidden',

        transform: `translate(
          ${infoPosition.x}px,
          ${infoPosition.y}px
        )`,
      }}
    >

      {/* =========================
          HEADER - AREA DRAG
          ========================= */}

      <div
        onMouseDown={handleInfoMouseDown}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',

          padding: '18px 22px',

          borderBottom:
            '1px solid #e5ebe8',

          cursor: 'move',

          userSelect: 'none',

          background: '#fff',
        }}
      >
        <div>
          <div
            style={{
              fontSize: 18,
              fontWeight: 800,
              color: '#10231b',
            }}
          >
            Informasi
          </div>

          <div
            style={{
              marginTop: 4,
              fontSize: 12,
              color: '#708079',
            }}
          >
            Referensi interpretasi data Curah hujan, TMAK, dan kelembapan Tanah
          </div>
        </div>


        {/* CLOSE */}

        <button
          type="button"
          onClick={() => setShowInfo(false)}
          style={{
            width: 34,
            height: 34,

            border: 0,
            borderRadius: 8,

            background: '#f3f6f4',

            fontSize: 22,
            lineHeight: 1,

            cursor: 'pointer',

            color: '#708079',
          }}
        >
          ×
        </button>
      </div>


      {/* =========================
          CONTENT
          ========================= */}

      <div
        style={{
          padding: 22,

          display: 'grid',
          gap: 22,

          maxHeight: 'calc(85vh - 82px)',

          overflowY: 'auto',
        }}
      >

        {/* PER JAM */}

        <InfoSection
          title="Intensitas Hujan per Jam"
          description="Jumlah curah hujan dalam periode satu jam."
          rows={[
            [
              'Hujan Sangat Ringan',
              '< 1 mm/jam',
            ],
            [
              'Hujan Ringan',
              '1 – 5 mm/jam',
            ],
            [
              'Hujan Sedang',
              '5 – 10 mm/jam',
            ],
            [
              'Hujan Lebat',
              '10 – 20 mm/jam',
            ],
            [
              'Hujan Sangat Lebat',
              '> 20 mm/jam',
            ],
          ]}
        />

        <InfoSection
          title="Tinggi Air Kanal (TMAK)"
          description="Informasi tinggi muka air kanal."
          rows={[
            [
              ' Ambang batas siaga',
              '– 20 cm',
            ],
            [
              'Normal',
              '40 cm',
            ],
            [
              'Mendekati Banjir',
              '0,5 – 20 cm',
            ],
            // [
            //   'Banjir',
            //   '0,5 – 20 cm',
            // ],
            // [
            //   'Banjir Bandang',
            //   '0,5 – 20 cm',
            // ],
          ]}
        />

       {/* <InfoSection
          title="Kelembaban Tanah"
          // description="Akumulasi curah hujan selama satu bulan."
          rows={[
            [
              'Kategori Terlalu Kering',
              '40% – 50%',
            ],
            [
              'Kategori Normal',
              ' 55% – 65%',
            ],
            [
              'Basah',
              '85% – 100%',
            ],
          ]}
        />
*/}

        {/* FOOTER */}

        <div
          style={{
            paddingTop: 14,

            borderTop:
              '1px solid #e5ebe8',

            fontSize: 11,

            color: '#7a8982',
          }}
        >
          Referensi klasifikasi: BMKG -- Curah Hujan
        </div>

      </div>
    </div>
  </div>
)}

        {/*End Modal Informasi*/}

      {/* FILTER */}
      <div className={styles.filterBar}>
        <div className={styles.filterField}>
         {/* <label>Tanggal</label>

          <input
            type="date"
            value={tanggal}
            onChange={e =>
              setTanggal(e.target.value)
            }
          />*/}
        </div>

        {/*<button
          className={styles.btnRefresh}
          onClick={fetchData}
          disabled={loading}
        >
          {loading ? (
            <span
              className={styles.spinner}
            />
          ) : (
            '↺'
          )}

          {loading
            ? 'Memuat...'
            : 'Perbarui'}
        </button>*/}

        <span className={styles.filterInfo}>
          Nilai sensor menggunakan NilaiHasil dari VwLoggerTest
          {' · '}
          Data dikelompokkan berdasarkan SensorCode
        </span>
      </div>

      {error && (
        <div className={styles.empty}>
          {error}
        </div>
      )}
      {/*CH*/}
      {/* STAT CARDS */}
     {/* <div className={styles.statRow}>
        <div className={styles.statCard}>
          <div className={styles.statLabel}>
            Intensitas Hujan
          </div>

          <div
            className={styles.statValue}
            style={{ color: '#2e7d32' }}
          >
            {latestIntensitasPoint?.intensitasHujanPerData != null
              ? latestIntensitasPoint.intensitasHujanPerData.toFixed(4)
              : '--'}

            <span className={styles.statUnit}>
              {' '}mm
            </span>
          </div>

          <div className={styles.statSub}>
            {latestIntensitasPoint
              ? `n = ${latestIntensitasPoint.jumlahDataPerJam} pada ${latestIntensitasPoint.waktu}`
              : '-'}
          </div>
        </div>

        <div className={styles.statCard}>
          <div className={styles.statLabel}>
            Curah Hujan
          </div>

          <div
            className={styles.statValue}
            style={{ color: '#1e88e5' }}
          >
            {latest
              ? latest.curahHujan?.toFixed(2)
              : '--'}

            <span className={styles.statUnit}>
              {' '}mm
            </span>
          </div>

          <div className={styles.statSub}>
            Akumulasi: {akumulasiCH} mm
          </div>
        </div>*/}
        {/*<div className={styles.statCard}>
          <div className={styles.statLabel}>
            Kelembaban Tanah
          </div>

          <div
            className={styles.statValue}
            style={{ color: '#00acc1' }}
          >
            {latest
              ? latest.kelembapanTanah?.toFixed(1)
              : '--'}

            <span className={styles.statUnit}>
              {' '}%
            </span>
          </div>

          <div className={styles.statSub}>
            {latest?.waktu ?? '-'}
          </div>
        </div>*/}
       {/* <div
          className={styles.statCard}
          style={{ display: 'none' }}
        >
          <div className={styles.statLabel}>
            Kelembaban Tanah
          </div>

          <div
            className={styles.statValue}
            style={{ color: '#00acc1' }}
          >
            {latest
              ? latest.kelembapanTanah?.toFixed(1)
              : '--'}

            <span className={styles.statUnit}>
              {' '}%
            </span>
          </div>

          <div className={styles.statSub}>
            {latest?.waktu ?? '-'}
          </div>
        </div>

        <div className={styles.statCard}>
          <div className={styles.statLabel}>
            Tinggi Muka Air Kanal
          </div>

          <div
            className={styles.statValue}
            style={{ color: '#6a1b9a' }}
          >
            {latest
              ? latest.tinggiMukaAir?.toFixed(1)
              : '--'}

            <span className={styles.statUnit}>
              {' '}cm
            </span>
          </div>

          <div className={styles.statSub}>
            {latest?.waktu ?? '-'}
          </div>
        </div>
      </div>*/}

{/*BR*/}
<div className={styles.statRow}>

  {chartSeries.map(
    sensor => {

      const validPoints =
        sensor.points.filter(
          point =>
            !point.isGap &&
            point.value !== null,
        )

      const latest =
        validPoints[
          validPoints.length - 1
        ]

      return (
        <div
          key={
            sensor.sensorCode
          }
          className={
            styles.statCard
          }
        >

          <div
            className={
              styles.statLabel
            }
          >
            {sensor.namaSensor}
          </div>

          <div
            className={
              styles.statValue
            }
          >
            {latest?.value != null
              ? latest.value.toFixed(2)
              : '--'}

            <span
              className={
                styles.statUnit
              }
            >
              {' '}
              {sensor.satuan}
            </span>
          </div>

          <div
            className={
              styles.statSub
            }
          >
            {latest?.waktu ?? '-'}
          </div>

        </div>
      )
    },
  )}

</div>
{/* ============================ */}
{/* EMPAT GRAFIK                 */}
{/* ============================ */}

{/*<div className={styles.chartGrid}>*/}

  {/* ======================== */}
  {/* 1. INTENSITAS HUJAN      */}
  {/* ======================== */}
{/*CH*/}
 {/* <ChartCard
  title="Intensitas Hujan"
  unit="mm"
  color="#2e7d32"

  // nilai terakhir PER DATA
  latestValue={
    latestIntensitasPoint?.intensitasHujanPerData != null
      ? `${latestIntensitasPoint.intensitasHujanPerData.toFixed(4)} mm`
      : undefined
  }
>
  <SensorChart
    data={chartData}
    ticks={xTicks}

    minTs={dayStartTs}
    maxTs={dayEndTs}
    dataKey="intensitasHujanPerData"

    name="Intensitas Hujan"

    unit="mm"

    stroke="#43a047"

    domain={[
      0,
      'auto',
    ]}
  />
</ChartCard>

  <ChartCard
    title="Curah Hujan"
    unit="mm"
    color="#1e88e5"

    latestValue={
      latest
        ? `${latest.curahHujan?.toFixed(2)} mm`
        : undefined
    }
  >
    <SensorChart
      data={chartData}

      ticks={xTicks}

      minTs={dayStartTs}
      maxTs={dayEndTs}

      dataKey="curahHujan"

      name="Curah Hujan"

      unit="mm"

      stroke="#1e88e5"

      domain={[
        0,
        'auto',
      ]}
    />
  </ChartCard>*/}


  {/* ======================== */}
  {/* 3. KELEMBABAN TANAH      */}
  {/* ======================== */}
{/*<div style={{ display: 'none' }}>
  <ChartCard
    title="Kelembaban Tanah"
    unit="%"
    color="#00acc1"

    latestValue={
      latest
        ? `${latest.kelembapanTanah?.toFixed(1)}%`
        : undefined
    }
  >
    <SensorChart
      data={chartData}

      ticks={xTicks}

      minTs={dayStartTs}
      maxTs={dayEndTs}

      dataKey="kelembapanTanah"

      name="Kelembaban Tanah"

      unit="%"

      stroke="#00acc1"

      domain={[
        0,
        100,
      ]}
    />
  </ChartCard>
</div>*/}

  <div style={{ display: 'none' }}>
 {/* <ChartCard
    title="Kelembaban Tanah"
    unit="%"
    color="#00acc1"
    latestValue={
      latest
        ? `${latest.kelembapanTanah?.toFixed(1)}%`
        : undefined
    }
  >
    <SensorChart
      data={chartData}
      ticks={xTicks}
      minTs={dayStartTs}
      maxTs={dayEndTs}
      dataKey="kelembapanTanah"
      name="Kelembaban Tanah"
      unit="%"
      stroke="#00acc1"
      domain={[0, 100]}
    />
  </ChartCard>
</div>*/}


  {/*<ChartCard
    title="Tinggi Muka Air Kanal"
    unit="cm"
    color="#6a1b9a"

    latestValue={
      latest
        ? `${latest.tinggiMukaAir?.toFixed(1)} cm`
        : undefined
    }
  >*/}
{/*    <SensorChart
      data={chartData}

      ticks={xTicks}

      minTs={dayStartTs}
      maxTs={dayEndTs}

      dataKey="tinggiMukaAir"

      name="Tinggi Muka Air Kanal"

      unit="cm"

      stroke="#6a1b9a"
      domain={[
        'auto',
        'auto',
      ]}
    />
  </ChartCard>*/}

{/*</div>

      {!loading &&
        !error &&
        sensorData.length === 0 && (
          <div className={styles.empty}>
            <span>🌧</span>
            <p>
              Tidak ada data untuk tanggal yang dipilih
            </p>
          </div>
        )}
*/}
      {/*<div className={styles.footer}>
        Data mengikuti timestamp asli database
        {' · '}
        Gap &gt; 5 menit = grafik diputus
        {' · '}
        Sumbu X per 2 jam
        {' · '}
        Curah Hujan = NilaiCurahHujan × 0.7
        {' · '}
        Intensitas = Curah Hujan ÷ n data/jam
        {' · '}
        TMAK = 30 - NilaiTinggiAir
      </div>
    </div>
  )*/}

    <div className={styles.chartGrid}>

  {chartSeries.map(
    (sensor, index) => {

      const validPoints =
        sensor.points.filter(
          point =>
            !point.isGap &&
            point.value !== null,
        )

      const latest =
        validPoints[
          validPoints.length - 1
        ]

      const color =
        chartColors[index % chartColors.length]

      return (
        <ChartCard
          key={
            sensor.sensorCode
          }

          title={
            sensor.namaSensor
          }

          unit={
            sensor.satuan
          }

          color={color}

          latestValue={
            latest?.value != null
              ? `${latest.value.toFixed(2)} ${sensor.satuan}`
              : undefined
          }
        >

          <SensorChart
            data={
              sensor.points
            }

            ticks={
              xTicks
            }

            minTs={
              dayStartTs
            }

            maxTs={
              dayEndTs
            }

            name={
              sensor.namaSensor
            }

            unit={
              sensor.satuan
            }

            stroke={color}
          />

        </ChartCard>
      )
    },
  )}

</div>

      {!loading && !error && sensorData.length === 0 && (
        <div className={styles.empty}>
          <span>—</span>
          <p>Tidak ada data untuk tanggal yang dipilih</p>
        </div>
      )}

      <div className={styles.footer}>
        Data mengikuti ProcessedAt dari database
        {' · '}
        Gap &gt; 5 menit = grafik diputus
        {' · '}
        Sumbu X per 2 jam
        {' · '}
        Nilai grafik = NilaiHasil dari VwLoggerTest
      </div>
    </div>
    </div>
  )
}
