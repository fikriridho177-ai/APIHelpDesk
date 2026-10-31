import { useState, useEffect } from 'react'
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid,
  Tooltip, ResponsiveContainer, ReferenceLine,
} from 'recharts'
import FilterBar from '../components/FilterBar'
import ChartCard from '../components/ChartCard'
import DataTable, { type Column } from '../components/DataTable'
import StatusBadge from '../components/StatusBadge'
import { useSensorData } from '../hooks/useSensorData'
import { formatDate, daysAgoStr, todayStr } from '../utils/format'
import { getStatus, THRESHOLDS } from '../types'
import type { DateRange, TmakItem } from '../types'
import styles from './SensorPage.module.css'

const COLUMNS: Column<TmakItem>[] = [
  { key: 'no',      header: '#',           render: (_, i) => i + 1 },
  { key: 'tanggal', header: 'Tanggal',     render: r => formatDate(r.Tanggal) },
  { key: 'blok',    header: 'Kode Blok',   render: r => r.KodeBlok || '-' },
  {
    key: 'nilai',
    header: 'Tinggi Air (cm)',
    render: r => <strong>{r.Nilai ?? '-'} cm</strong>,
  },
  { key: 'ket',    header: 'Keterangan',  render: r => r.ket || '-' },
  {
    key: 'status',
    header: 'Status',
    render: r => <StatusBadge status={getStatus(Number(r.Nilai) || 0, THRESHOLDS.tmak)} />,
  },
]

export default function TmakPage() {
  const [range, setRange] = useState<DateRange>({
    from: daysAgoStr(7),
    to: todayStr(),
  })
  const { data, loading, fetchTMAK } = useSensorData()

  useEffect(() => {
    fetchTMAK(range)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const chartData = data.tmak.map(r => ({
    label: formatDate(r.Tanggal),
    nilai: Number(r.Nilai) || 0,
  }))

  return (
    <div>
      <FilterBar
        range={range}
        onChange={setRange}
        onApply={() => fetchTMAK(range)}
        loading={loading}
      />

      <ChartCard
        title="Grafik Tinggi Muka Air Kanal (TMAK)"
        dot="purple"
        unit="cm"
        loading={loading}
        footer={
          <div className={styles.thresholdInfo}>
            <span style={{ color: '#f9a825' }}>⚠️ Waspada ≥ 150 cm</span>
            &ensp;|&ensp;
            <span style={{ color: '#e53935' }}>🚨 Kritis ≥ 200 cm</span>
          </div>
        }
      >
        <div style={{ height: 300 }}>
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={chartData} margin={{ top: 4, right: 8, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f4f8" />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} angle={-30} textAnchor="end" height={50} />
              <YAxis tick={{ fontSize: 11 }} unit=" cm" />
              <Tooltip formatter={(v: number) => [`${v} cm`, 'Tinggi Air']} />
              <ReferenceLine y={150} stroke="#f9a825" strokeDasharray="5 4" label={{ value: 'Waspada', fontSize: 10, fill: '#f9a825' }} />
              <ReferenceLine y={200} stroke="#e53935" strokeDasharray="5 4" label={{ value: 'Kritis',  fontSize: 10, fill: '#e53935' }} />
              <Line
                type="monotone"
                dataKey="nilai"
                stroke="#6a1b9a"
                strokeWidth={2.5}
                dot={{ r: 4, fill: '#6a1b9a' }}
                activeDot={{ r: 6 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </ChartCard>

      <div className={styles.tableCard}>
        <div className={styles.tableHeader}>
          <span>📋 Data TMAK</span>
          <span className={styles.count}>{data.tmak.length} baris</span>
        </div>
        <DataTable
          columns={COLUMNS}
          data={data.tmak}
          loading={loading}
          emptyIcon="💧"
          emptyText="Tidak ada data TMAK untuk rentang tanggal ini"
        />
      </div>
    </div>
  )
}
