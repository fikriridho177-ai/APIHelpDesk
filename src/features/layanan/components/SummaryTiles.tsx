import { DashboardIcon, type IconName } from '../../dashboard/components/DashboardIcon'
import type { LynSummary } from '../types/layanan'

type Tile = { key: keyof LynSummary; label: string; icon: IconName; tone: string }

const TILES: Tile[] = [
  { key: 'baru', label: 'Tiket Baru', icon: 'ticket', tone: 'red' },
  { key: 'dalamProses', label: 'Dalam Proses', icon: 'clock', tone: 'amber' },
  { key: 'selesai', label: 'Selesai', icon: 'check', tone: 'green' },
  { key: 'menungguInfo', label: 'Menunggu Informasi', icon: 'info', tone: 'blue' },
]

// Ringkasan jumlah pengajuan per status (Tiket Baru, Dalam Proses, Selesai, Menunggu Informasi).
export default function SummaryTiles({ summary }: { summary: LynSummary | null }) {
  return (
    <div className="dashboard-stats">
      {TILES.map(tile => (
        <div key={tile.key} className={`dashboard-stat stat-${tile.tone}`}>
          <span className="dashboard-stat-icon">
            <DashboardIcon name={tile.icon} />
          </span>
          <div>
            <strong>{summary ? summary[tile.key] : '-'}</strong>
            <small>{tile.label}</small>
          </div>
        </div>
      ))}
    </div>
  )
}
