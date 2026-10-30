import { PriorityBadge, StatusBadge } from '../../layanan/components/Badges'
import type { LynRequestItem } from '../../layanan/types/layanan'

// Lama menunggu sejak pengajuan dibuat.
function formatAge(createdAt: string) {
  const days = Math.floor((Date.now() - new Date(createdAt).getTime()) / 86_400_000)
  return days <= 0 ? 'Hari ini' : `${days} hari`
}

// Daftar tiket ringkas untuk widget. detailed = tampilkan layanan, pemohon, prioritas & umur (Antrian).
export function TicketList({
  items,
  detailed,
  onOpen,
}: {
  items: LynRequestItem[]
  detailed?: boolean
  onOpen: (id: number) => void
}) {
  return (
    <ul className="portal-ticket-list">
      {items.map(item => (
        <li key={item.id}>
          <button type="button" className="portal-ticket" onClick={() => onOpen(item.id)}>
            <span className="portal-ticket-main">
              <small>{item.requestNo ?? `#${item.id}`}</small>
              <strong>{item.title}</strong>
              {detailed && (
                <small>
                  {item.serviceName} · {item.requesterName}
                </small>
              )}
            </span>
            <span className="portal-ticket-meta">
              {detailed && <PriorityBadge priority={item.priority} />}
              <StatusBadge status={item.status} />
              {detailed && <small>{formatAge(item.createdAt)}</small>}
            </span>
          </button>
        </li>
      ))}
    </ul>
  )
}
