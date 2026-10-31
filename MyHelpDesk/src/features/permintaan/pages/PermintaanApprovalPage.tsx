import { useCallback, useEffect, useMemo, useState } from 'react'

import { getLayananErrorMessage, getLayananRequests, runLayananAction } from '../../layanan/api/layananApi'
import { PriorityBadge } from '../../layanan/components/Badges'
import RequestDetailModal from '../../layanan/components/RequestDetailModal'
import { useDepartmentNames } from '../../layanan/hooks/useLayanan'
import { formatDateTime } from '../../layanan/layananConfig'
import type { LynRequestItem } from '../../layanan/types/layanan'
import { NoAccess, SensusHero, StatTiles } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { PMT_MENU } from '../permintaanShared'
import '../../layanan/layanan.css'
import '../../sensus/sensus.css'

// /permintaan/approval : tiket baru yang menunggu approval, dari semua divisi yang boleh di-approve user
// (hak Approve di menu divisi + Akses Departemen). Urut nomor antrian.
export default function PermintaanApprovalPage() {
  const { permission, loaded } = useMenuPermission(PMT_MENU.approval)
  const departmentNames = useDepartmentNames()

  const [requests, setRequests] = useState<LynRequestItem[]>([])
  const [loading, setLoading] = useState(true)
  const [division, setDivision] = useState('')
  const [openedId, setOpenedId] = useState<number | null>(null)
  const [rejecting, setRejecting] = useState<{ id: number; note: string } | null>(null)
  const [busyId, setBusyId] = useState<number | null>(null)

  const canRead = !!permission?.canRead

  const load = useCallback(async () => {
    const list = await getLayananRequests({ status: 'BARU' })
    setRequests(list.filter(r => r.allowedActions.includes('ACCEPT')))
  }, [])

  useEffect(() => {
    if (!canRead) return
    load()
      .catch(error => console.error('Gagal mengambil tiket approval:', error))
      .finally(() => setLoading(false))
  }, [canRead, load])

  const divisions = useMemo(() => Array.from(new Set(requests.map(r => r.divisionName ?? '-'))).sort(), [requests])
  const rows = useMemo(
    () =>
      requests
        .filter(r => !division || (r.divisionName ?? '-') === division)
        .sort((a, b) => (a.divisionName ?? '').localeCompare(b.divisionName ?? '') || (a.queueNo ?? 999999) - (b.queueNo ?? 999999)),
    [requests, division],
  )

  if (!canRead) return <NoAccess loaded={loaded} />

  async function act(id: number, action: 'ACCEPT' | 'REJECT', note?: string) {
    if (action === 'REJECT' && !note?.trim()) {
      alert('Alasan penolakan wajib diisi.')
      return
    }
    try {
      setBusyId(id)
      await runLayananAction(id, { action, note: note?.trim() || undefined })
      setRejecting(null)
      await load()
    } catch (error) {
      alert(getLayananErrorMessage(error, 'Gagal menyimpan approval.'))
    } finally {
      setBusyId(null)
    }
  }

  const urgent = requests.filter(r => r.priority === 'URGENT' || r.priority === 'TINGGI').length

  return (
    <section className="lyn-page">
      <SensusHero
        title="Approval Permintaan"
        subtitle="Tiket baru dari form pengajuan yang menunggu approval. Setelah di-approve, tiket lanjut ke penjadwalan dan proses."
      />

      <StatTiles
        items={[
          { label: 'Menunggu approval', value: loading ? '-' : requests.length, tone: 'blue' },
          { label: 'Prioritas tinggi / urgent', value: loading ? '-' : urgent, tone: 'red' },
          { label: 'Divisi', value: loading ? '-' : divisions.length, tone: 'indigo' },
        ]}
      />

      <div className="lyn-card">
        <div className="lyn-status-filter">
          <button type="button" className={division === '' ? 'lyn-chip active' : 'lyn-chip'} onClick={() => setDivision('')}>
            Semua divisi <b>{requests.length}</b>
          </button>
          {divisions.map(d => (
            <button key={d} type="button" className={division === d ? 'lyn-chip active' : 'lyn-chip'} onClick={() => setDivision(d)}>
              {d} <b>{requests.filter(r => (r.divisionName ?? '-') === d).length}</b>
            </button>
          ))}
        </div>

        {loading ? (
          <div className="lyn-empty">Memuat tiket...</div>
        ) : rows.length === 0 ? (
          <div className="lyn-empty">Tidak ada tiket yang menunggu approval.</div>
        ) : (
          <div className="table-container">
            <table className="sns-table">
              <thead>
                <tr>
                  <th className="num">Antrian</th>
                  <th>No pengajuan</th>
                  <th>Divisi</th>
                  <th>Jenis / judul</th>
                  <th>Pemohon</th>
                  <th>Prioritas</th>
                  <th>Diajukan</th>
                  <th>Aksi</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(r => (
                  <tr key={r.id}>
                    <td className="num">
                      <b>{r.queueNo ?? '-'}</b>
                    </td>
                    <td className="sns-no">{r.requestNo ?? `#${r.id}`}</td>
                    <td>{r.divisionName ?? '-'}</td>
                    <td>
                      <b>{r.serviceName}</b>
                      <br />
                      <small className="lyn-muted">{r.title}</small>
                    </td>
                    <td>{r.requesterName}</td>
                    <td>
                      <PriorityBadge priority={r.priority} />
                    </td>
                    <td>{formatDateTime(r.createdAt)}</td>
                    <td>
                      <div className="pmt-actions">
                        <button type="button" className="lyn-button ghost" onClick={() => setOpenedId(r.id)}>
                          Detail
                        </button>
                        <button type="button" className="lyn-button success" disabled={busyId === r.id} onClick={() => void act(r.id, 'ACCEPT')}>
                          Approve
                        </button>
                        {r.allowedActions.includes('REJECT') && (
                          <button
                            type="button"
                            className="lyn-button danger"
                            disabled={busyId === r.id}
                            onClick={() => setRejecting(current => (current?.id === r.id ? null : { id: r.id, note: '' }))}
                          >
                            Tolak
                          </button>
                        )}
                      </div>
                      {rejecting?.id === r.id && (
                        <div className="pmt-reason">
                          <input
                            autoFocus
                            value={rejecting.note}
                            onChange={e => setRejecting({ id: r.id, note: e.target.value })}
                            placeholder="Alasan penolakan"
                          />
                          <button type="button" className="lyn-button danger" disabled={busyId === r.id} onClick={() => void act(r.id, 'REJECT', rejecting.note)}>
                            Kirim
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {openedId !== null && (
        <RequestDetailModal
          requestId={openedId}
          departmentNames={departmentNames}
          onClose={() => setOpenedId(null)}
          onChanged={() => void load()}
        />
      )}
    </section>
  )
}
