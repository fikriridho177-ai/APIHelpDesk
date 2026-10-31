import { useState, useEffect } from 'react'
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid,
  Tooltip, ResponsiveContainer,
} from 'recharts'
import FilterBar from '../components/FilterBar'
import ChartCard from '../components/ChartCard'
import DataTable, { type Column } from '../components/DataTable'
import StatusBadge from '../components/StatusBadge'
import { useSensorData } from '../hooks/useSensorData'
import { formatDate, daysAgoStr, todayStr } from '../utils/format'
import { getStatus, THRESHOLDS } from '../types'
import type { DateRange, KelembapanItem } from '../types'
import styles from './SensorPage.module.css'

const COLUMNS: Column<KelembapanItem>[] = [
  { key: 'no',      header: '#',           render: (_, i) => i + 1 },
  { key: 'tanggal', header: 'Tanggal',     render: r => formatDate(r.Tanggal) },
  { key: 'lokasi',  header: 'Lokasi / Sensor', render: r => r.Lokasi || r.IdSensor || '-' },
  {
    key: 'nilai',
    header: 'Kelembaban (%)',
    render: r => <strong>{r.NilaiKelembapan ?? '-'}%</strong>,
  },
  {
    key: 'status',
    header: 'Status',
    render: r => (
      <StatusBadge status={getStatus(Number(r.NilaiKelembapan) || 0, THRESHOLDS.kelembapan)} />
    ),
  },
]

export default function KelembapanPage() {
  const [range, setRange] = useState<DateRange>({
    from: daysAgoStr(7),
    to: todayStr(),
  })
  const { data, loading, fetchKel } = useSensorData()

  useEffect(() => {
    fetchKel(range)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const chartData = data.kelembapan.map(r => ({
    label: formatDate(r.Tanggal),
    nilai: Number(r.NilaiKelembapan) || 0,
  }))

  return (
    <div>
      <FilterBar
        range={range}
        onChange={setRange}
        onApply={() => fetchKel(range)}
        loading={loading}
      />

      <ChartCard
        title="Grafik Kelembaban Tanah"
        dot="orange"
        unit="%"
        loading={loading}
        footer={
          <div className={styles.thresholdInfo}>
            <span style={{ color: '#f9a825' }}>⚠️ Waspada ≤ 30%</span>
            &ensp;|&ensp;
            <span style={{ color: '#e53935' }}>🚨 Kritis ≥ 80%</span>
          </div>
        }
      >
        <div style={{ height: 300 }}>
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={chartData} margin={{ top: 4, right: 8, left: -10, bottom: 0 }}>
              <defs>
                <linearGradient id="kelGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%"  stopColor="#e65100" stopOpacity={0.18} />
                  <stop offset="95%" stopColor="#e65100" stopOpacity={0.01} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f4f8" />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} angle={-30} textAnchor="end" height={50} />
              <YAxis tick={{ fontSize: 11 }} unit="%" domain={[0, 100]} />
              <Tooltip formatter={(v: number) => [`${v}%`, 'Kelembaban']} />
              <Area
                type="monotone"
                dataKey="nilai"
                stroke="#e65100"
                strokeWidth={2.5}
                fill="url(#kelGrad)"
                dot={{ r: 4, fill: '#e65100' }}
                activeDot={{ r: 6 }}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </ChartCard>

      <div className={styles.tableCard}>
        <div className={styles.tableHeader}>
          <span>📋 Data Kelembaban Tanah</span>
          <span className={styles.count}>{data.kelembapan.length} baris</span>
        </div>
        <DataTable
          columns={COLUMNS}
          data={data.kelembapan}
          loading={loading}
          emptyIcon="🌱"
          emptyText="Tidak ada data kelembaban untuk rentang tanggal ini"
        />
      </div>
    </div>
  )
}
