import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'

import { getMonitorCurahHujan } from '../services/api'
import type { CurahHujanMonitorItem } from '../types'
import { buildCalendarData, getCalendarCellStyle } from '../utils/rainHistoris'
import { filterRowsBySensor, getStationMeta, stationUrl, type MonitorRowWithMeta,} from '../utils/station'
import PageNavigation from '../components/PageNavigation'

const MONTHS = [
  'Januari', 'Februari', 'Maret', 'April', 'Mei', 'Juni',
  'Juli', 'Agustus', 'September', 'Oktober', 'November', 'Desember',
]

function pad(n: number) {
  return String(n).padStart(2, '0')
}

function daysInMonth(year: number, monthIndex: number) {
  return new Date(year, monthIndex + 1, 0).getDate()
}

export default function KalenderCurahHujanPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const stationId = searchParams.get('station') ?? '1'

  const currentYear = new Date().getFullYear()
  const [year, setYear] = useState(currentYear)
  const [rows, setRows] = useState<MonitorRowWithMeta[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const filteredRows = useMemo(
    () => filterRowsBySensor(rows, stationId),
    [rows, stationId],
  )

  const stationMeta = useMemo(
    () => getStationMeta(rows, stationId),
    [rows, stationId],
  )

  const calendarMap = useMemo(
    () => buildCalendarData(
      filteredRows as CurahHujanMonitorItem[],
      year,
      stationId,
    ),
    [filteredRows, year, stationId],
  )

  useEffect(() => {
    async function loadYearData() {
      try {
        setLoading(true)
        setError('')

        const data = await getMonitorCurahHujan(
          `${year}-01-01`,
          `${year}-12-31`,
        )

        setRows(data as MonitorRowWithMeta[])
      } catch (err) {
        console.error('Gagal memuat kalender curah hujan:', err)
        setRows([])
        setError('Gagal memuat data kalender curah hujan.')
      } finally {
        setLoading(false)
      }
    }

    loadYearData()
  }, [year, stationId])

//   return (
//     <div style={{ minHeight: '100%', padding: 24, background: '#f5f8f7', boxSizing: 'border-box' }}>
//       <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', marginBottom: 22 }}>
//         {/*<button type="button" onClick={() => navigate(stationUrl('/ecs/monitor-curahhujan', stationId))}>
//           ‹ Monitor Stasiun {stationId}
//         </button>

//         <button type="button" onClick={() => navigate(stationUrl('/ecs/data-historis', stationId))}>
//           Data Historis ›
//         </button>*/}
//         <PageNavigation
//           backTo={`/ecs/monitor-curahhujan?station=${encodeURIComponent(stationId)}`}
//           backLabel={`Monitor Stasiun ${stationId}`}
//           actions={[
//             {
//               to: `/ecs/data-historis?station=${encodeURIComponent(stationId)}`,
//               label: 'Data Historis',
//               icon: '📈',
//             },
//           ]}
//         />
//       </div>

//       <div style={{ textAlign: 'center', marginBottom: 28 }}>
//         <div style={{ color: '#18784f', fontSize: 11, fontWeight: 800, letterSpacing: '.14em', marginBottom: 7 }}>
//           STASIUN {stationId}
//         </div>
//         <h1 style={{ margin: 0, fontSize: 34, color: '#10231b' }}>Kalender Curah Hujan</h1>
//         <div style={{ marginTop: 8, fontSize: 16, fontWeight: 700, color: '#315747' }}>
//           {stationMeta.namaSensor}
//         </div>
//         <div style={{ marginTop: 5, color: '#708079', fontSize: 13 }}>
//           {stationMeta.titikIpal} • IdSensor {stationId}
//           {stationMeta.kodeWilayah !== '-' ? ` • ${stationMeta.kodeWilayah}` : ''}
//         </div>
//       </div>

//       <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 12, marginBottom: 22, flexWrap: 'wrap' }}>
//         <label>
//           Tahun{' '}
//           <select
//             value={year}
//             onChange={e => setYear(Number(e.currentTarget.value))}
//             style={{ height: 38, marginLeft: 8 }}
//           >
//             {Array.from({ length: 6 }).map((_, index) => {
//               const itemYear = currentYear - index
//               return <option key={itemYear} value={itemYear}>{itemYear}</option>
//             })}
//           </select>
//         </label>

//         <div style={{ padding: '9px 13px', border: '1px solid #d9e5df', borderRadius: 7, background: '#fff', color: '#315747', fontSize: 12, fontWeight: 700 }}>
//           Stasiun {stationId} — {stationMeta.namaSensor}
//         </div>
//       </div>

//       {error && <div style={{ color: '#b91c1c', marginBottom: 16 }}>{error}</div>}

//       {loading ? (
//         <div style={{ textAlign: 'center', padding: 60 }}>Memuat data Stasiun {stationId}...</div>
//       ) : (
//         <div style={{ overflowX: 'auto', borderRadius: 10, boxShadow: '0 10px 30px rgba(20,54,42,.08)' }}>
//           <table style={{ width: '100%', minWidth: 1200, borderCollapse: 'collapse', background: '#fff' }}>
//             <thead>
//               <tr>
//                 <th style={{ border: '1px solid #d1d5db', padding: 10, minWidth: 130 }}>Bulan / Tanggal</th>
//                 {Array.from({ length: 31 }).map((_, index) => (
//                   <th key={index} style={{ border: '1px solid #d1d5db', padding: 9 }}>
//                     {pad(index + 1)}
//                   </th>
//                 ))}
//               </tr>
//             </thead>

//             <tbody>
//               {MONTHS.map((monthName, monthIndex) => (
//                 <tr key={monthName}>
//                   <td style={{ border: '1px solid #d1d5db', padding: 10, fontWeight: 700 }}>{monthName}</td>

//                   {Array.from({ length: 31 }).map((_, dayIndex) => {
//                     const day = dayIndex + 1
//                     const validDay = day <= daysInMonth(year, monthIndex)

//                     if (!validDay) {
//                       return <td key={day} style={{ border: '1px solid #d1d5db', background: '#f1f3f2' }} />
//                     }

//                     const key = `${year}-${pad(monthIndex + 1)}-${pad(day)}`
//                     const total = calendarMap.get(key)
//                     const cell = getCalendarCellStyle(total)

//                     return (
//                       <td
//                         key={day}
//                         title={
//                           total === undefined
//                             ? `Stasiun ${stationId} - Tidak ada data`
//                             : `Stasiun ${stationId} - ${key}: ${total.toFixed(2)} mm`
//                         }
//                         style={{
//                           border: '1px solid #d1d5db',
//                           padding: 9,
//                           textAlign: 'center',
//                           background: cell.background,
//                           color: cell.color,
//                           fontWeight: total !== undefined ? 700 : 400,
//                         }}
//                       >
//                         {total === undefined ? '-' : total.toFixed(1)}
//                       </td>
//                     )
//                   })}
//                 </tr>
//               ))}
//             </tbody>
//           </table>
//         </div>
//       )}

//       <div style={{ marginTop: 22, display: 'flex', gap: 16, flexWrap: 'wrap', justifyContent: 'center', fontSize: 12 }}>
//         <Legend color="#e5e7eb" text="Tidak ada data" />
//         <Legend color="#ffffff" text="< 0.5 mm" border />
//         <Legend color="#6fcf97" text="0.5 - 20 mm" />
//         <Legend color="#f2c94c" text="21 - 50 mm" />
//         <Legend color="#f2994a" text="51 - 100 mm" />
//         <Legend color="#eb5757" text="101 - 150 mm" />
//         <Legend color="#9b51e0" text="> 150 mm" />
//       </div>
//     </div>
//   )
// }

// function Legend({ color, text, border = false }: { color: string; text: string; border?: boolean }) {
//   return (
//     <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
//       <i style={{ width: 14, height: 14, display: 'inline-block', background: color, border: border ? '1px solid #d1d5db' : 'none' }} />
//       {text}
//     </span>
//   )
// }

return (
  <div
    style={{
      minHeight: '100%',
      padding: '24px',
      background: '#f4f7f6',
      boxSizing: 'border-box',
    }}
  >
    {/* ============================= */}
    {/* NAVIGATION */}
    {/* ============================= */}
    <div
      style={{
        marginBottom: 20,
      }}
    >
      <PageNavigation
        backTo={`/ecs/monitor-curahhujan?station=${encodeURIComponent(stationId)}`}
        backLabel={`Monitor Stasiun ${stationId}`}
        actions={[
          {
            to: `/ecs/data-historis?station=${encodeURIComponent(stationId)}`,
            label: 'Data Historis',
            icon: '📈',
          },
        ]}
      />
    </div>

    {/* ============================= */}
    {/* HEADER */}
    {/* ============================= */}
    <div
      style={{
        background: '#ffffff',
        border: '1px solid #e3ebe7',
        borderRadius: 16,
        padding: '24px 28px',
        marginBottom: 18,
        boxShadow: '0 4px 18px rgba(20,54,42,.05)',
      }}
    >
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 24,
          flexWrap: 'wrap',
        }}
      >
        {/* TITLE */}
        <div>
          <div
            style={{
              color: '#16865d',
              fontSize: 11,
              fontWeight: 800,
              letterSpacing: '.14em',
              marginBottom: 7,
            }}
          >
            ENVIRONMENTAL MONITORING
          </div>

          <h1
            style={{
              margin: 0,
              fontSize: 28,
              lineHeight: 1.2,
              color: '#10231b',
              fontWeight: 800,
            }}
          >
            Kalender Data Sensor
          </h1>

          <div
            style={{
              marginTop: 9,
              fontSize: 14,
              color: '#64766e',
            }}
          >
            Rekapitulasi curah hujan harian selama tahun {year}
          </div>
        </div>

        {/* STATION INFO */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 12,
            padding: '12px 16px',
            borderRadius: 12,
            background: '#f5faf8',
            border: '1px solid #dbeae3',
            minWidth: 250,
          }}
        >
          <div
            style={{
              width: 42,
              height: 42,
              borderRadius: 10,
              background: '#e1f4eb',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: 20,
            }}
          >
            🌧️
          </div>

          <div>
            <div
              style={{
                fontSize: 11,
                color: '#718078',
                fontWeight: 700,
                textTransform: 'uppercase',
                letterSpacing: '.06em',
              }}
            >
              Stasiun {stationId}
            </div>

            <div
              style={{
                marginTop: 2,
                color: '#173f31',
                fontWeight: 800,
                fontSize: 14,
              }}
            >
              {stationMeta.namaSensor}
            </div>

            <div
              style={{
                marginTop: 3,
                color: '#7b8983',
                fontSize: 11,
              }}
            >
              {stationMeta.titikIpal}
              {' • '}
              IdSensor {stationId}
              {stationMeta.kodeWilayah !== '-'
                ? ` • ${stationMeta.kodeWilayah}`
                : ''}
            </div>
          </div>
        </div>
      </div>
    </div>

    {/* ============================= */}
    {/* FILTER */}
    {/* ============================= */}
    <div
      style={{
        background: '#ffffff',
        border: '1px solid #e3ebe7',
        borderRadius: 14,
        padding: '14px 18px',
        marginBottom: 18,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 16,
        flexWrap: 'wrap',
        boxShadow: '0 3px 14px rgba(20,54,42,.04)',
      }}
    >
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 12,
        }}
      >
        <div
          style={{
            fontSize: 13,
            fontWeight: 700,
            color: '#425a50',
          }}
        >
          Tahun
        </div>

        <select
          value={year}
          onChange={e =>
            setYear(Number(e.currentTarget.value))
          }
          style={{
            height: 38,
            minWidth: 100,
            padding: '0 12px',
            border: '1px solid #cad9d2',
            borderRadius: 8,
            background: '#fff',
            color: '#173f31',
            fontSize: 13,
            fontWeight: 700,
            outline: 'none',
            cursor: 'pointer',
          }}
        >
          {Array.from({ length: 6 }).map(
            (_, index) => {
              const itemYear =
                currentYear - index

              return (
                <option
                  key={itemYear}
                  value={itemYear}
                >
                  {itemYear}
                </option>
              )
            },
          )}
        </select>
      </div>

      <div
        style={{
          padding: '8px 13px',
          borderRadius: 8,
          background: '#eef8f3',
          color: '#18784f',
          fontSize: 12,
          fontWeight: 700,
          border: '1px solid #d5ebe0',
        }}
      >
        Stasiun {stationId}
        {' — '}
        {stationMeta.namaSensor}
      </div>
    </div>

    {/* ============================= */}
    {/* ERROR */}
    {/* ============================= */}
    {error && (
      <div
        style={{
          padding: '12px 15px',
          marginBottom: 16,
          borderRadius: 10,
          background: '#fff1f2',
          border: '1px solid #fecdd3',
          color: '#b91c1c',
          fontSize: 13,
          fontWeight: 600,
        }}
      >
        {error}
      </div>
    )}

    {/* ============================= */}
    {/* CALENDAR CARD */}
    {/* ============================= */}
    <div
      style={{
        background: '#ffffff',
        border: '1px solid #e3ebe7',
        borderRadius: 16,
        overflow: 'hidden',
        boxShadow: '0 8px 28px rgba(20,54,42,.07)',
      }}
    >
      {/* CARD HEADER */}
      <div
        style={{
          padding: '16px 20px',
          borderBottom: '1px solid #e7eeea',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 12,
          flexWrap: 'wrap',
        }}
      >
        <div>
          <div
            style={{
              color: '#173f31',
              fontSize: 15,
              fontWeight: 800,
            }}
          >
            Curah Hujan Harian
          </div>

          <div
            style={{
              color: '#829088',
              fontSize: 11,
              marginTop: 3,
            }}
          >
            Nilai dalam milimeter (mm)
          </div>
        </div>

        <div
          style={{
            fontSize: 12,
            color: '#66776f',
          }}
        >
          Januari – Desember {year}
        </div>
      </div>

      {/* TABLE */}
      {loading ? (
        <div
          style={{
            textAlign: 'center',
            padding: 70,
            color: '#64766e',
            fontSize: 13,
          }}
        >
          <div
            style={{
              fontSize: 22,
              marginBottom: 8,
            }}
          >
            🌧️
          </div>

          Memuat data Stasiun {stationId}...
        </div>
      ) : (
        <div
          style={{
            overflowX: 'auto',
            width: '100%',
          }}
        >
          <table
            style={{
              width: '100%',
              minWidth: 1450,
              borderCollapse: 'separate',
              borderSpacing: 0,
              background: '#fff',
              fontSize: 11,
            }}
          >
            <thead>
              <tr>
                <th
                  style={{
                    position: 'sticky',
                    left: 0,
                    top: 0,
                    zIndex: 4,
                    minWidth: 125,
                    padding: '12px 10px',
                    textAlign: 'left',
                    background: '#f0f7f4',
                    color: '#274b3d',
                    fontWeight: 800,
                    borderRight: '1px solid #dce7e1',
                    borderBottom: '1px solid #dce7e1',
                  }}
                >
                  Bulan / Tanggal
                </th>

                {Array.from({ length: 31 }).map(
                  (_, index) => (
                    <th
                      key={index}
                      style={{
                        position: 'sticky',
                        top: 0,
                        zIndex: 2,
                        minWidth: 41,
                        height: 42,
                        padding: '0 5px',
                        textAlign: 'center',
                        background: '#f0f7f4',
                        color: '#315747',
                        fontWeight: 800,
                        borderRight: '1px solid #dce7e1',
                        borderBottom: '1px solid #dce7e1',
                      }}
                    >
                      {pad(index + 1)}
                    </th>
                  ),
                )}
              </tr>
            </thead>

            <tbody>
              {MONTHS.map(
                (monthName, monthIndex) => (
                  <tr key={monthName}>
                    {/* MONTH */}
                    <td
                      style={{
                        position: 'sticky',
                        left: 0,
                        zIndex: 3,
                        minWidth: 125,
                        padding: '11px 10px',
                        background: '#ffffff',
                        color: '#24473a',
                        fontWeight: 800,
                        borderRight: '1px solid #dce7e1',
                        borderBottom: '1px solid #e7ece9',
                        boxShadow: '3px 0 6px rgba(20,54,42,.03)',
                      }}
                    >
                      {monthName}
                    </td>

                    {Array.from({
                      length: 31,
                    }).map((_, dayIndex) => {
                      const day =
                        dayIndex + 1

                      const validDay =
                        day <=
                        daysInMonth(
                          year,
                          monthIndex,
                        )

                      if (!validDay) {
                        return (
                          <td
                            key={day}
                            style={{
                              height: 40,
                              background:
                                '#f4f6f5',
                              borderRight:
                                '1px solid #e4e9e6',
                              borderBottom:
                                '1px solid #e4e9e6',
                            }}
                          />
                        )
                      }

                      const key =
                        `${year}-${pad(
                          monthIndex + 1,
                        )}-${pad(day)}`

                      const total =
                        calendarMap.get(key)

                      const cell =
                        getCalendarCellStyle(
                          total,
                        )

                      return (
                        <td
                          key={day}
                          title={
                            total === undefined
                              ? `Stasiun ${stationId} - ${key} - Tidak ada data`
                              : `Stasiun ${stationId} - ${key}: ${total.toFixed(2)} mm`
                          }
                          style={{
                            height: 40,
                            padding: '0 5px',
                            textAlign: 'center',
                            background:
                              cell.background,
                            color:
                              cell.color,
                            fontWeight:
                              total !== undefined
                                ? 800
                                : 400,
                            borderRight:
                              '1px solid rgba(209,213,219,.8)',
                            borderBottom:
                              '1px solid rgba(209,213,219,.8)',
                            transition:
                              'transform .12s ease, box-shadow .12s ease',
                            cursor:
                              total !== undefined
                                ? 'default'
                                : 'default',
                            whiteSpace: 'nowrap',
                          }}
                        >
                          {total === undefined
                            ? '-'
                            : total.toFixed(1)}
                        </td>
                      )
                    })}
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>

    {/* ============================= */}
    {/* LEGEND */}
    {/* ============================= */}
    <div
      style={{
        marginTop: 18,
        background: '#ffffff',
        border: '1px solid #e3ebe7',
        borderRadius: 12,
        padding: '13px 16px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 18,
        flexWrap: 'wrap',
        boxShadow: '0 3px 12px rgba(20,54,42,.04)',
      }}
    >
      <span
        style={{
          fontSize: 11,
          fontWeight: 800,
          color: '#4b6258',
          marginRight: 2,
        }}
      >
        Intensitas:
      </span>

      <Legend
        color="#e5e7eb"
        text="Tidak ada data"
      />

      <Legend
        color="#ffffff"
        text="< 0.5 mm"
        border
      />

      <Legend
        color="#6fcf97"
        text="0.5 - 20 mm"
      />

      <Legend
        color="#f2c94c"
        text="21 - 50 mm"
      />

      <Legend
        color="#f2994a"
        text="51 - 100 mm"
      />

      <Legend
        color="#eb5757"
        text="101 - 150 mm"
      />

      <Legend
        color="#9b51e0"
        text="> 150 mm"
      />
    </div>
  </div>
)
}
function Legend({
  color,
  text,
  border = false,
}: {
  color: string
  text: string
  border?: boolean
}) {
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 7,
        color: '#5b6e65',
        fontSize: 11,
        fontWeight: 600,
        whiteSpace: 'nowrap',
      }}
    >
      <i
        style={{
          width: 13,
          height: 13,
          display: 'inline-block',
          background: color,
          border: border
            ? '1px solid #cfd8d3'
            : '1px solid transparent',
          borderRadius: 3,
          boxShadow: '0 1px 2px rgba(0,0,0,.05)',
        }}
      />

      {text}
    </span>
  )
}
