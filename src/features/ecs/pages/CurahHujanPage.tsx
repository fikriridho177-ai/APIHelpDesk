import { useState, useEffect } from 'react'
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ResponsiveContainer, ReferenceLine,
} from 'recharts'
import FilterBar from '../components/FilterBar'
import ChartCard from '../components/ChartCard'
import DataTable, { type Column } from '../components/DataTable'
import StatusBadge from '../components/StatusBadge'
import { useSensorData } from '../hooks/useSensorData'
import { formatDate, daysAgoStr, todayStr } from '../utils/format'
import { getStatus, THRESHOLDS } from '../types'
import type { DateRange, CurahHujanItem } from '../types'
import styles from './SensorPage.module.css'

const COLUMNS: Column<CurahHujanItem>[] = [
  { key: 'no',        header: '#',          render: (_, i) => i + 1 },
  { key: 'tanggal',   header: 'Tanggal',    render: r => formatDate(r.Tanggal) },
  { key: 'persil',    header: 'Kode Persil',render: r => r.KodePersil || '-' },
  { key: 'pagi',      header: 'Pagi (mm)',  render: r => r.Pagi ?? '-' },
  { key: 'sore',      header: 'Sore (mm)',  render: r => r.Sore ?? '-' },
  {
    key: 'total',
    header: 'Total',
    render: r => {
      const total = (Number(r.Pagi) || 0) + (Number(r.Sore) || 0)
      return <strong>{total.toFixed(1)} mm</strong>
    },
  },
  {
    key: 'status',
    header: 'Status',
    render: r => {
      const total = (Number(r.Pagi) || 0) + (Number(r.Sore) || 0)
      return <StatusBadge status={getStatus(total, THRESHOLDS.curahHujan)} />
    },
  },
]

export default function CurahHujanPage() {
  const [range, setRange] = useState<DateRange>({
    from: daysAgoStr(7),
    to: todayStr(),
  })
  const { data, loading, fetchCH } = useSensorData()

  useEffect(() => {
    fetchCH(range)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const chartData = data.curahHujan.map(r => ({
    label: formatDate(r.Tanggal),
    pagi:  Number(r.Pagi) || 0,
    sore:  Number(r.Sore) || 0,
  }))

  return (
    <div>
      <FilterBar
        range={range}
        onChange={setRange}
        onApply={() => fetchCH(range)}
        loading={loading}
      />

      <ChartCard
        title="Grafik Curah Hujan Harian (Pagi & Sore)"
        dot="blue"
        unit="mm"
        loading={loading}
        footer={
          <div className={styles.thresholdInfo}>
            <span style={{ color: '#f9a825' }}>⚠️ Waspada ≥ 50 mm</span>
            &ensp;|&ensp;
            <span style={{ color: '#e53935' }}>🚨 Kritis ≥ 100 mm</span>
          </div>
        }
      >
        <div style={{ height: 300 }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData} margin={{ top: 4, right: 8, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f4f8" />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} angle={-30} textAnchor="end" height={50} />
              <YAxis tick={{ fontSize: 11 }} unit=" mm" />
              <Tooltip formatter={(v: number, name: string) => [`${v} mm`, name === 'pagi' ? 'Pagi' : 'Sore']} />
              <Legend formatter={v => (v === 'pagi' ? 'Pagi' : 'Sore')} />
              <ReferenceLine y={50}  stroke="#f9a825" strokeDasharray="5 4" label={{ value: 'Waspada', fontSize: 10, fill: '#f9a825' }} />
              <ReferenceLine y={100} stroke="#e53935" strokeDasharray="5 4" label={{ value: 'Kritis',  fontSize: 10, fill: '#e53935' }} />
              <Bar dataKey="pagi" fill="#1565c0" radius={[4, 4, 0, 0]} />
              <Bar dataKey="sore" fill="#90caf9" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </ChartCard>

      <div className={styles.tableCard}>
        <div className={styles.tableHeader}>
          <span>📋 Data Curah Hujan</span>
          <span className={styles.count}>{data.curahHujan.length} baris</span>
        </div>
        <DataTable
          columns={COLUMNS}
          data={data.curahHujan}
          loading={loading}
          emptyIcon="🌧"
          emptyText="Tidak ada data curah hujan untuk rentang tanggal ini"
        />
      </div>
    </div>
  )
}
