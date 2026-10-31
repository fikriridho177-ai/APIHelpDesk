import { FormEvent, useEffect, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { getLayananErrorMessage, getLayananRequest, runLayananAction } from '../api/layananApi'
import { ACTION_CONFIG, LOG_LABEL, STATUS_CONFIG, STATUS_STEPS, formatDate, formatDateTime, getStepIndex } from '../layananConfig'
import type { LynAction, LynRequestDetail } from '../types/layanan'
import { PriorityBadge, StatusBadge } from './Badges'

type Props = {
  requestId: number
  departmentNames: Map<string, string>
  onClose: () => void
  onChanged: () => void
}

// Urutan tombol aksi (alur flowchart lebih dulu, aksi negatif di akhir).
const ACTION_ORDER: LynAction[] = ['ACCEPT', 'SCHEDULE', 'PROCESS', 'NEED_INFO', 'FINISH', 'REJECT', 'CANCEL', 'COMMENT']

export default function RequestDetailModal({ requestId, departmentNames, onClose, onChanged }: Props) {
  const [detail, setDetail] = useState<LynRequestDetail | null>(null)
  const [activeAction, setActiveAction] = useState<LynAction | null>(null)
  const [note, setNote] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [assignee, setAssignee] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    getLayananRequest(requestId)
      .then(setDetail)
      .catch(error => {
        alert(getLayananErrorMessage(error, 'Gagal membuka pengajuan.'))
        onClose()
      })
  }, [requestId])

  function chooseAction(action: LynAction) {
    setActiveAction(current => (current === action ? null : action))
    setNote('')
    if (action === 'SCHEDULE' && detail) {
      setScheduledAt(detail.summary.scheduledAt ? toLocalInput(detail.summary.scheduledAt) : '')
      setAssignee(detail.assignee ?? '')
    }
  }

  async function submitAction(e: FormEvent) {
    e.preventDefault()
    if (!activeAction) return

    const config = ACTION_CONFIG[activeAction]
    if ((activeAction === 'CANCEL' || activeAction === 'REJECT') && !window.confirm(`${config.label} pengajuan ini?`)) return

    try {
      setBusy(true)
      const updated = await runLayananAction(requestId, {
        action: activeAction,
        note: note || undefined,
        scheduledAt: config.schedule && scheduledAt ? new Date(scheduledAt).toISOString() : undefined,
        assignee: config.schedule ? assignee || undefined : undefined,
      })
      setDetail(updated)
      setActiveAction(null)
      setNote('')
      onChanged()
    } catch (error) {
      alert(getLayananErrorMessage(error, 'Gagal menyimpan aksi.'))
    } finally {
      setBusy(false)
    }
  }

  const summary = detail?.summary
  const stepIndex = summary ? getStepIndex(summary.status) : -1
  const isStopped = summary && ['DITOLAK', 'DIBATALKAN'].includes(summary.status)
  const actions = detail ? ACTION_ORDER.filter(action => detail.allowedActions.includes(action)) : []
  const activeConfig = activeAction ? ACTION_CONFIG[activeAction] : null

  return (
    <div className="lyn-modal-backdrop" onMouseDown={onClose}>
      <div className="lyn-modal" onMouseDown={e => e.stopPropagation()}>
        {!detail || !summary ? (
          <div className="lyn-empty">Memuat detail...</div>
        ) : (
          <>
            {/* HEADER */}
            <div className="lyn-modal-header">
              <div>
                <span className="lyn-request-no">{summary.requestNo}</span>
                <h2>{summary.title}</h2>
                <p>
                  {summary.divisionName ? `${summary.divisionName} · ` : ''}
                  {summary.serviceName}
                </p>
              </div>
              <div className="lyn-modal-header-side">
                {summary.queueNo != null && <span className="lyn-queue">Antrian ke-{summary.queueNo}</span>}
                <StatusBadge status={summary.status} />
                <button type="button" className="lyn-icon-button" onClick={onClose} aria-label="Tutup">
                  ✕
                </button>
              </div>
            </div>

            <div className="lyn-modal-body">
              {/* PROGRESS ALUR */}
              {isStopped ? (
                <div className={`lyn-stopped tone-${STATUS_CONFIG[summary.status].tone}`}>
                  Pengajuan {STATUS_CONFIG[summary.status].label.toLowerCase()}.
                </div>
              ) : (
                <ol className="lyn-stepper">
                  {STATUS_STEPS.map((step, index) => (
                    <li
                      key={step.status}
                      className={index < stepIndex ? 'done' : index === stepIndex ? 'current' : ''}
                    >
                      <span className="lyn-step-dot">{index < stepIndex ? '✓' : index + 1}</span>
                      <span className="lyn-step-label">
                        {index === stepIndex && summary.status === 'MENUNGGU_INFO' ? 'Menunggu Info' : step.label}
                      </span>
                    </li>
                  ))}
                </ol>
              )}

              {/* INFORMASI */}
              <h4 className="lyn-info-title">Data Pemohon</h4>
              <div className="lyn-info-grid">
                <InfoItem label="Nama Pemohon" value={`${summary.requesterName} (@${detail.requesterUserLogin})`} />
                <InfoItem label="Jabatan" value={detail.requesterPosition || '-'} />
                <InfoItem
                  label="Departemen"
                  value={
                    detail.requesterDepartment ||
                    (summary.requesterDeptId ? departmentNames.get(summary.requesterDeptId) ?? summary.requesterDeptId : '-')
                  }
                />
                <InfoItem label="Perusahaan" value={detail.requesterCompanies || '-'} />
                <InfoItem
                  label="Nomor HP/Whatsapp"
                  value={detail.requesterPhone ? <a href={waLink(detail.requesterPhone)} target="_blank" rel="noreferrer">{detail.requesterPhone}</a> : '-'}
                />
                <InfoItem
                  label="Email"
                  value={detail.requesterEmail ? <a href={`mailto:${detail.requesterEmail}`}>{detail.requesterEmail}</a> : '-'}
                />
              </div>

              <h4 className="lyn-info-title">Permintaan</h4>
              <div className="lyn-info-grid">
                <InfoItem label="Jenis Permintaan" value={detail.otherType ? `${summary.serviceName}: ${detail.otherType}` : summary.serviceName} />
                <InfoItem
                  label="Nomor Antrian"
                  value={summary.queueNo != null ? `Ke-${summary.queueNo} di divisi ${summary.divisionName ?? ''}` : '- (sudah diproses / selesai)'}
                />
                <InfoItem label="Skala Prioritas" value={<PriorityBadge priority={summary.priority} />} />
                <InfoItem label="Lokasi Pekerjaan" value={detail.location || '-'} />
                <InfoItem label="Tanggal Deadline" value={formatDate(detail.neededDate)} />
                <InfoItem label="Diajukan" value={formatDateTime(summary.createdAt)} />
                <InfoItem label="Jadwal" value={formatDateTime(summary.scheduledAt)} />
                <InfoItem label="Petugas" value={detail.assignee || '-'} />
              </div>

              <div className="lyn-description">
                <h4>Deskripsi Permintaan</h4>
                <p>{detail.description}</p>
              </div>

              {/* AKSI */}
              {actions.length > 0 && (
                <div className="lyn-actions">
                  <h4>Tindakan</h4>
                  <div className="lyn-action-buttons">
                    {actions.map(action => (
                      <button
                        key={action}
                        type="button"
                        className={`lyn-button ${ACTION_CONFIG[action].tone}${activeAction === action ? ' active' : ''}`}
                        onClick={() => chooseAction(action)}
                      >
                        {ACTION_CONFIG[action].label}
                      </button>
                    ))}
                  </div>

                  {activeAction && activeConfig && (
                    <form className="lyn-action-form" onSubmit={submitAction}>
                      <p className="lyn-muted">{activeConfig.hint}</p>

                      {activeConfig.schedule && (
                        <div className="lyn-form-grid">
                          <label className="lyn-field">
                            <span>Tanggal & jam pelaksanaan *</span>
                            <input type="datetime-local" required value={scheduledAt} onChange={e => setScheduledAt(e.target.value)} />
                          </label>
                          <label className="lyn-field">
                            <span>Petugas / tim</span>
                            <input value={assignee} maxLength={150} onChange={e => setAssignee(e.target.value)} placeholder="Opsional" />
                          </label>
                        </div>
                      )}

                      <label className="lyn-field">
                        <span>Catatan{activeConfig.noteRequired ? ' *' : ''}</span>
                        <textarea rows={3} required={activeConfig.noteRequired} value={note} onChange={e => setNote(e.target.value)} />
                      </label>

                      <div className="lyn-form-actions">
                        <button type="button" className="lyn-button ghost" onClick={() => setActiveAction(null)}>
                          Batal
                        </button>
                        <button type="submit" className={`lyn-button ${activeConfig.tone}`} disabled={busy}>
                          {busy ? 'Menyimpan...' : activeConfig.label}
                        </button>
                      </div>
                    </form>
                  )}
                </div>
              )}

              {/* RIWAYAT */}
              <div className="lyn-timeline-wrap">
                <h4>Riwayat</h4>
                <ul className="lyn-timeline">
                  {[...detail.logs].reverse().map(log => (
                    <li key={log.id}>
                      <span className={`lyn-timeline-dot tone-${log.toStatus ? STATUS_CONFIG[log.toStatus]?.tone ?? 'gray' : 'gray'}`}>
                        {log.action === 'COMMENT' ? <DashboardIcon name="chat" /> : null}
                      </span>
                      <div>
                        <strong>{LOG_LABEL[log.action] ?? log.action}</strong>
                        <small>
                          {log.userName} · {formatDateTime(log.createdAt)}
                        </small>
                        {log.note && <p>{log.note}</p>}
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  )
}

function InfoItem({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="lyn-info-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

// Nomor HP -> tautan WhatsApp (08xx -> 628xx).
function waLink(phone: string) {
  const digits = phone.replace(/\D/g, '')
  return `https://wa.me/${digits.startsWith('0') ? '62' + digits.slice(1) : digits}`
}

// ISO -> nilai untuk <input type="datetime-local"> (waktu lokal).
function toLocalInput(iso: string) {
  const date = new Date(iso)
  const offset = date.getTimezoneOffset() * 60000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}
