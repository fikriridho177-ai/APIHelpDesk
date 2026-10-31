import { useMemo, useRef,  useState, type MouseEvent as ReactMouseEvent, } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
// import { Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,} from 'recharts'
// import { Bar, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis,} from 'recharts'
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { getMonitorCurahHujan } from '../services/api'
import type { CurahHujanMonitorItem } from '../types'
import { buildHistoryPoints, type HistoryInterval } from '../utils/rainHistoris'
import { filterRowsBySensor, getStationMeta, stationUrl, type MonitorRowWithMeta,} from '../utils/station'
import PageNavigation from '../components/PageNavigation'

function ymd(date: Date) {
  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0'),
  ].join('-')
}

function daysAgo(days: number) {
  const d = new Date()
  d.setDate(d.getDate() - days)
  return ymd(d)
}

const cardStyle = {
  border: '1px solid #dde7e2',
  borderRadius: 12,
  background: '#fff',
  padding: 18,
  boxShadow: '0 8px 24px rgba(14,66,45,.06)',
} as const

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

export default function DataHistorisPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const stationId = searchParams.get('station') ?? '1'

  // const [interval, setInterval] = useState<HistoryInterval>('5m')
  // // const [dateStart, setDateStart] = useState(daysAgo(1))
  // // const [dateEnd, setDateEnd] = useState(ymd(new Date()))
  // const [dateStart, setDateStart] = useState(daysAgo(1))
  // const dateEnd = useMemo(
  //   () => getEndDate(dateStart, interval),
  //   [dateStart, interval],
  // )
  // const [rows, setRows] = useState<MonitorRowWithMeta[]>([])
  // const [loading, setLoading] = useState(false)
  // const [loaded, setLoaded] = useState(false)
  // const [error, setError] = useState('')
  const [interval, setInterval] = useState<HistoryInterval>('5m')

  const [dateStart, setDateStart] = useState(daysAgo(1))
  const [dateEnd, setDateEnd] = useState(ymd(new Date()))

  const [rows, setRows] = useState<MonitorRowWithMeta[]>([])
  const [loading, setLoading] = useState(false)
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState('')

  // ==========================================
// MODAL INFORMASI
// ==========================================

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
  // Jangan drag ketika klik tombol close
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

  const stationMeta = useMemo(
    () => getStationMeta(rows, stationId),
    [rows, stationId],
  )

  const filteredRows = useMemo(
    () => filterRowsBySensor(rows, stationId),
    [rows, stationId],
  )

  const chartData = useMemo(
    () => buildHistoryPoints(
      filteredRows as CurahHujanMonitorItem[],
      interval,
      stationId,
    ),
    [filteredRows, interval, stationId],
  )
  const maxEndDate = useMemo(
    () => getMaxEndDate(dateStart, interval),
    [dateStart, interval],
  )

 function getMaxEndDate(
    startDate: string,
    interval: HistoryInterval,
  ) {
    if (!startDate) return ''

    const date = new Date(`${startDate}T00:00:00`)

    switch (interval) {
      case '5m':
      case 'hour':
        date.setDate(date.getDate() + 1)
        break

      case 'day':
        date.setDate(date.getDate() + 30)
        break

      case 'month':
        date.setFullYear(date.getFullYear() + 1)
        break
    }

    return ymd(date)
  }

function getEndDate(startDate: string, interval: HistoryInterval) {
  if (!startDate) return ''

  const date = new Date(`${startDate}T00:00:00`)

  switch (interval) {
    case '5m':
    case 'hour':
      date.setDate(date.getDate() + 1)
      break

    case 'day':
      date.setDate(date.getDate() + 30)
      break

    case 'month':
      date.setFullYear(date.getFullYear() + 1)
      break
  }

  return ymd(date)
}

  // async function handleLoad() {
  //   try {
  //     setLoading(true)
  //     setError('')

  //     const data = await getMonitorCurahHujan(dateStart, dateEnd)
  //     setRows(data as MonitorRowWithMeta[])
  //     setLoaded(true)
  //   } catch (err) {
  //     console.error('Gagal memuat data historis:', err)
  //     setRows([])
  //     setLoaded(true)
  //     setError('Gagal memuat data historis.')
  //   } finally {
  //     setLoading(false)
  //   }
  // }
  async function handleLoad() {
    try {
      setLoading(true)
      setError('')

      console.log({
        interval,
        dateStart,
        dateEnd,
      })

      const data = await getMonitorCurahHujan( dateStart, dateEnd,)

      setRows(data as MonitorRowWithMeta[])
      setLoaded(true)
    } catch (err) {
      console.error('Gagal memuat data historis:', err)

      setRows([])
      setLoaded(true)
      setError('Gagal memuat data historis.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div style={{ minHeight: '100%', padding: 24, background: '#f5f8f7', boxSizing: 'border-box' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', marginBottom: 24 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, width: '100%',}}>
        <PageNavigation
            backTo={`/ecs/monitor-curahhujan?station=${encodeURIComponent(stationId)}`}
            backLabel={`Monitor Stasiun ${stationId}`}
            actions={[
              {
                to: `/ecs/kalender-curah-hujan?station=${encodeURIComponent(stationId)}`,
                label: 'Kalender Curah Hujan',
                icon: '▣',
              },
            ]}
          />
          
        </div>
      </div>

      {/* ==========================================
    MODAL INFORMASI
    ========================================== */}

{showInfo && (
  <div
    onMouseDown={e => {
      if (e.target === e.currentTarget) {
        setShowInfo(false)
      }
    }}
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
      style={{
        width:
          'min(760px, calc(100vw - 40px))',

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
      {/* HEADER / DRAG AREA */}

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
            Informasi Klasifikasi Curah Hujan
          </div>

          <div
            style={{
              marginTop: 4,
              fontSize: 12,
              color: '#708079',
            }}
          >
            Referensi interpretasi data curah hujan
          </div>
        </div>

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
          aria-label="Tutup"
        >
          ×
        </button>
      </div>

      {/* CONTENT */}

      <div
        style={{
          padding: 22,
          display: 'grid',
          gap: 22,

          maxHeight:
            'calc(85vh - 82px)',

          overflowY: 'auto',
        }}
      >
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
          title="Curah Hujan per 24 Jam"
          description="Akumulasi curah hujan selama periode 24 jam."
          rows={[
            [
              'Hujan Ringan',
              '0,5 – 20 mm/hari',
            ],
            [
              'Hujan Sedang',
              '20 – 50 mm/hari',
            ],
            [
              'Hujan Lebat',
              '50 – 100 mm/hari',
            ],
            [
              'Hujan Sangat Lebat',
              '100 – 150 mm/hari',
            ],
            [
              'Hujan Ekstrem',
              '> 150 mm/hari',
            ],
          ]}
        />

        <InfoSection
          title="Curah Hujan Bulanan"
          description="Akumulasi curah hujan selama satu bulan."
          rows={[
            [
              'Kategori Rendah',
              '0 – 100 mm/bulan',
            ],
            [
              'Kategori Menengah',
              '101 – 300 mm/bulan',
            ],
            [
              'Kategori Tinggi',
              '301 – 500 mm/bulan',
            ],
            [
              'Kategori Sangat Tinggi',
              '> 500 mm/bulan',
            ],
          ]}
        />

        <div
          style={{
            paddingTop: 14,
            borderTop:
              '1px solid #e5ebe8',

            fontSize: 11,
            color: '#7a8982',
          }}
        >
          Referensi klasifikasi: BMKG
        </div>
      </div>
    </div>
  </div>
)}

      <div style={{ textAlign: 'center', marginBottom: 28 }}>
        <div style={{ color: '#18784f', fontSize: 11, fontWeight: 800, letterSpacing: '.14em', marginBottom: 7 }}>
          STASIUN {stationId}
        </div>
        <h1 style={{ margin: 0, fontSize: 34, color: '#10231b' }}>Data Historis Sensor</h1>
        <div style={{ marginTop: 8, fontSize: 16, fontWeight: 700, color: '#315747' }}>
          {stationMeta.namaSensor}
        </div>
        <div style={{ marginTop: 5, color: '#708079', fontSize: 13 }}>
          {stationMeta.titikIpal} • IdSensor {stationId}
          {stationMeta.kodeWilayah !== '-' ? ` • ${stationMeta.kodeWilayah}` : ''}
        </div>
      </div>

      <div style={cardStyle}>
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(170px,.8fr) minmax(180px,1fr) minmax(180px,1fr) auto', gap: 14, alignItems: 'end' }}>
          <label>
            <div style={{ marginBottom: 6, fontWeight: 700, fontSize: 12 }}>Interval</div>
           <select
              value={interval}
              onChange={e => {
                const value = e.currentTarget.value

                if (
                  value === '5m' ||
                  value === 'hour' ||
                  value === 'day' ||
                  value === 'month' ||
                  value === 'year'
                ) {
                  setInterval(value)

                  // Set tanggal selesai sesuai range interval baru
                  setDateEnd(
                    getMaxEndDate(dateStart, value)
                  )
                }
              }}
              style={{
                width: '100%',
                height: 40,
              }}
            >
              <option value="5m">Per 5 Menit</option>
              <option value="hour">Per Jam</option>
              <option value="day">Harian</option>
              <option value="month">Bulanan</option>
            </select>
          </label>

          {/*<label>
            <div style={{ marginBottom: 6, fontWeight: 700, fontSize: 12 }}>Tanggal Mulai</div>
            <input type="date" value={dateStart} onChange={e => setDateStart(e.currentTarget.value)} style={{ width: '100%', height: 40, boxSizing: 'border-box' }} />
          </label>

          <label>
            <div style={{ marginBottom: 6, fontWeight: 700, fontSize: 12 }}>Tanggal Selesai</div>
            <input type="date" value={dateEnd} onChange={e => setDateEnd(e.currentTarget.value)} style={{ width: '100%', height: 40, boxSizing: 'border-box' }} />
          </label>*/}
         <label>
            <div
              style={{
                marginBottom: 6,
                fontWeight: 700,
                fontSize: 12,
              }}
            >
              Tanggal Mulai
            </div>

           <input
              type="date"
              value={dateStart}
              onChange={e => {
                const newStart = e.currentTarget.value

                setDateStart(newStart)

                const newMaxEnd = getMaxEndDate(newStart, interval)

                // Kalau tanggal selesai lama sudah di luar range,
                // sesuaikan ke batas maksimum.
                if (
                  dateEnd < newStart ||
                  dateEnd > newMaxEnd
                ) {
                  setDateEnd(newMaxEnd)
                }
              }}
              style={{
                width: '100%',
                height: 40,
                boxSizing: 'border-box',
              }}
            />
          </label>

          <label>
            <div
              style={{
                marginBottom: 6,
                fontWeight: 700,
                fontSize: 12,
              }}
            >
              Tanggal Selesai
            </div>

           <input
              type="date"
              value={dateEnd}
              min={dateStart}
              max={maxEndDate}
              onChange={e => {
                const selected = e.currentTarget.value

                if (selected < dateStart) {
                  setDateEnd(dateStart)
                  return
                }

                if (selected > maxEndDate) {
                  setDateEnd(maxEndDate)
                  return
                }

                setDateEnd(selected)
              }}
              style={{
                width: '100%',
                height: 40,
                boxSizing: 'border-box',
              }}
            />
          </label>

          <button
            type="button"
            onClick={handleLoad}
            disabled={loading}
            style={{ minWidth: 120, height: 40, border: 0, borderRadius: 7, background: '#0d5139', color: '#fff', fontWeight: 700, cursor: 'pointer' }}
          >
            {loading ? 'Memuat...' : 'Lihat Data'}
          </button>
        </div>
      </div>

      {error && <div style={{ marginTop: 16, color: '#b91c1c' }}>{error}</div>}

      {!loaded && (
        <div style={{ textAlign: 'center', padding: '70px 20px', color: '#718078' }}>
          Pilih interval dan range tanggal, lalu klik <strong>Lihat Data</strong>.
        </div>
      )}

      {loaded && !error && filteredRows.length === 0 && (
        <div style={{ textAlign: 'center', padding: '70px 20px', color: '#718078' }}>
          Tidak ada data untuk <strong>Stasiun {stationId}</strong> pada range tanggal tersebut.
        </div>
      )}
      <button type="button" onClick={() => { setInfoPosition({  x: 0,  y: 0,  })
          setShowInfo(true) }} style={{
          height: 38,
          padding: '0 14px',
          border: '1px solid #d8e3dd',
          borderRadius: 8,
          background: '#fff',
          color: '#315747',
          fontWeight: 700,
          cursor: 'pointer',
          whiteSpace: 'nowrap',
        }}
      >
    ⓘ Informasi
  </button>

     {/* {loaded && !error && chartData.length > 0 && (
        <div style={{ display: 'grid', gap: 16, marginTop: 18 }}>
          <div style={cardStyle}>
            <h3 style={{ margin: '0 0 14px' }}>Intensitas Hujan — Stasiun {stationId}</h3>
            <div style={{ height: 300 }}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" minTickGap={28} />
                  <YAxis />
                  <Tooltip />
                  <Bar dataKey="intensitasHujan" name="Intensitas Hujan" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div style={cardStyle}>
            <h3 style={{ margin: '0 0 14px' }}>Parameter Lingkungan — Stasiun {stationId}</h3>
            <div style={{ height: 340 }}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" minTickGap={28} />
                  <YAxis />
                  <Tooltip />
                  <Legend />
                  <Line type="linear" dataKey="curahHujan" name="Curah Hujan (mm)" stroke="#1e88e5" strokeWidth={2} dot={false} activeDot={{ r: 4 }} connectNulls={false} />
                  <Line type="linear" dataKey="kelembapanTanah" name="Kelembaban Tanah (%)" stroke="#00acc1" strokeWidth={2} dot={false} activeDot={{ r: 4 }} connectNulls={false} />
                  <Line type="linear" dataKey="tmak" name="TMAK (cm)" stroke="#8e24aa" strokeWidth={2} dot={false} activeDot={{ r: 4 }} connectNulls={false} />                
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>
      )}*/}
       {/*</div>*/}

     {loaded && !error && chartData.length > 0 && (
        <div style={{ display: 'grid', gap: 16, marginTop: 18 }}>
          <div style={cardStyle}>
            <h3 style={{ margin: '0 0 14px' }}>
              Parameter Lingkungan — Stasiun {stationId}
            </h3>

            <div style={{ height: 400 }}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" />

                  <XAxis
                    dataKey="label"
                    minTickGap={28}
                  />

                  <YAxis />

                  <Tooltip />
                  <Legend />

                  {/* Intensitas Hujan */}
                  <Line
                    type="linear"
                    dataKey="intensitasHujan"
                    name="Intensitas Hujan"
                    stroke="#43a047"
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4 }}
                    connectNulls={false}
                  />

                  {/* Curah Hujan */}
                  <Line
                    type="linear"
                    dataKey="curahHujan"
                    name="Curah Hujan (mm)"
                    stroke="#1e88e5"
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4 }}
                    connectNulls={false}
                  />

                  {/* Kelembaban Tanah */}
                  {/*<Line
                    type="linear"
                    dataKey="kelembapanTanah"
                    name="Kelembaban Tanah (%)"
                    stroke="#00acc1"
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4 }}
                    connectNulls={false}
                  />*/}

                  {/* TMAK */}
                  <Line
                    type="linear"
                    dataKey="tmak"
                    name="TMAK (cm)"
                    stroke="#8e24aa"
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4 }}
                    connectNulls={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
