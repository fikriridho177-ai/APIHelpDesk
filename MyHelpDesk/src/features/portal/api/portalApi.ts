import { http } from '../../../api/http'
import type { PortalKpi, PortalQueue } from '../types/portal'

// Tiket yang menunggu tindakan user sebagai petugas.
export async function getQueue(take = 10): Promise<PortalQueue> {
  return (await http.get<PortalQueue>('/layanan/queue', { params: { take } })).data
}

// Jumlah tiket per divisi dalam N hari terakhir.
export async function getKpi(days = 30): Promise<PortalKpi> {
  return (await http.get<PortalKpi>('/layanan/kpi', { params: { days } })).data
}
