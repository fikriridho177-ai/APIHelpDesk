import { PRIORITY_CONFIG, STATUS_CONFIG } from '../layananConfig'
import type { LynPriority, LynStatus } from '../types/layanan'

export function StatusBadge({ status }: { status: LynStatus }) {
  const config = STATUS_CONFIG[status] ?? { label: status, tone: 'gray' }
  return <span className={`lyn-badge tone-${config.tone}`}>{config.label}</span>
}

export function PriorityBadge({ priority }: { priority: LynPriority }) {
  const config = PRIORITY_CONFIG[priority] ?? { label: priority, tone: 'gray' }
  return <span className={`lyn-priority tone-${config.tone}`}>{config.label}</span>
}
