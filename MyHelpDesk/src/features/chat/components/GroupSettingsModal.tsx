import { FormEvent, useEffect, useState } from 'react'

import {
  addChatMembers,
  createChatRole,
  deleteChatGroup,
  deleteChatRole,
  getChatErrorMessage,
  removeChatMember,
  searchChatUsers,
  updateChatGroup,
  updateChatMemberRole,
  updateChatRole,
} from '../api/chatApi'
import { CHAT_PERMISSION_OPTIONS, ChatPermission, hasChatPermission } from '../chatPermissions'
import type { ChatGroupDetail, ChatRole, ChatUserOption, SaveChatRoleRequest } from '../types/chat'

type Tab = 'general' | 'members' | 'roles'

type Props = {
  group: ChatGroupDetail
  currentUserId: number
  onClose: () => void
  onLeftGroup: () => void
}

const emptyRole: SaveChatRoleRequest = { name: '', color: '#2563eb', permissions: ChatPermission.SendMessages, position: 5 }

export default function GroupSettingsModal({ group, currentUserId, onClose, onLeftGroup }: Props) {
  const [tab, setTab] = useState<Tab>('general')
  const [busy, setBusy] = useState(false)

  const can = (permission: number) => hasChatPermission(group.myPermissions, permission)
  const isBelowMe = (position: number) => position < group.myPosition
  const assignableRoles = group.roles.filter(role => isBelowMe(role.position))

  // Jalankan aksi API dengan penanganan error yang seragam.
  async function run(action: () => Promise<void>, errorMessage: string) {
    try {
      setBusy(true)
      await action()
    } catch (error) {
      alert(getChatErrorMessage(error, errorMessage))
    } finally {
      setBusy(false)
    }
  }

  /* =========================================================
     TAB UMUM
  ========================================================= */

  const [name, setName] = useState(group.name)
  const [description, setDescription] = useState(group.description ?? '')

  function saveGeneral(e: FormEvent) {
    e.preventDefault()
    void run(() => updateChatGroup(group.id, name, description), 'Gagal menyimpan grup.')
  }

  function leaveGroup() {
    if (!window.confirm(`Keluar dari grup "${group.name}"?`)) return
    void run(async () => {
      await removeChatMember(group.id, currentUserId)
      onLeftGroup()
    }, 'Gagal keluar dari grup.')
  }

  function removeGroup() {
    if (!window.confirm(`Hapus grup "${group.name}"? Semua anggota tidak bisa membuka grup ini lagi.`)) return
    void run(async () => {
      await deleteChatGroup(group.id)
      onLeftGroup()
    }, 'Gagal menghapus grup.')
  }

  /* =========================================================
     TAB ANGGOTA
  ========================================================= */

  const [userSearch, setUserSearch] = useState('')
  const [userOptions, setUserOptions] = useState<ChatUserOption[]>([])
  const [selectedUserIds, setSelectedUserIds] = useState<number[]>([])
  const [newMemberRoleId, setNewMemberRoleId] = useState<number | ''>('')

  const memberIds = new Set(group.members.map(member => member.userId))

  // Cari user (debounce 300ms) saat tab anggota terbuka.
  useEffect(() => {
    if (tab !== 'members' || !can(ChatPermission.ManageMembers)) return

    const timer = window.setTimeout(() => {
      searchChatUsers(userSearch)
        .then(users => setUserOptions(users.filter(user => !memberIds.has(user.id))))
        .catch(() => setUserOptions([]))
    }, 300)

    return () => window.clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, userSearch, group.members.length])

  function toggleUser(userId: number) {
    setSelectedUserIds(current =>
      current.includes(userId) ? current.filter(id => id !== userId) : [...current, userId],
    )
  }

  function addSelectedMembers() {
    if (selectedUserIds.length === 0) return
    void run(async () => {
      await addChatMembers(group.id, selectedUserIds, newMemberRoleId || undefined)
      setSelectedUserIds([])
      setUserSearch('')
    }, 'Gagal menambah anggota.')
  }

  function kickMember(userId: number, memberName: string) {
    if (!window.confirm(`Keluarkan ${memberName} dari grup?`)) return
    void run(() => removeChatMember(group.id, userId), 'Gagal mengeluarkan anggota.')
  }

  /* =========================================================
     TAB ROLE
  ========================================================= */

  const [editingRoleId, setEditingRoleId] = useState<number | 'new' | null>(null)
  const [roleForm, setRoleForm] = useState<SaveChatRoleRequest>(emptyRole)

  // Permission yang boleh dicentang: hanya yang dimiliki sendiri (kecuali owner / Administrator).
  const canGrantAll = group.isOwner || (group.myPermissions & ChatPermission.Administrator) !== 0
  const canGrant = (permission: number) => canGrantAll || (group.myPermissions & permission) !== 0

  function startEditRole(role: ChatRole | null) {
    setEditingRoleId(role ? role.id : 'new')
    setRoleForm(
      role
        ? { name: role.name, color: role.color, permissions: role.permissions, position: role.position }
        : { ...emptyRole, position: Math.min(5, group.myPosition - 1) },
    )
  }

  function togglePermission(permission: number) {
    setRoleForm(current => ({ ...current, permissions: current.permissions ^ permission }))
  }

  function saveRole(e: FormEvent) {
    e.preventDefault()
    void run(async () => {
      if (editingRoleId === 'new') await createChatRole(group.id, roleForm)
      else if (editingRoleId !== null) await updateChatRole(group.id, editingRoleId, roleForm)
      setEditingRoleId(null)
    }, 'Gagal menyimpan role.')
  }

  function removeRole(role: ChatRole) {
    if (!window.confirm(`Hapus role "${role.name}"? Anggotanya dipindah ke role default.`)) return
    void run(() => deleteChatRole(group.id, role.id), 'Gagal menghapus role.')
  }

  /* =========================================================
     RENDER
  ========================================================= */

  return (
    <div className="chat-modal-backdrop" onMouseDown={onClose}>
      <div className="chat-modal" onMouseDown={e => e.stopPropagation()}>
        <div className="chat-modal-header">
          <h2>Pengaturan Grup</h2>
          <button type="button" className="chat-icon-button" onClick={onClose} aria-label="Tutup">
            ✕
          </button>
        </div>

        <div className="chat-tabs">
          {([
            ['general', 'Umum'],
            ['members', `Anggota (${group.members.length})`],
            ['roles', `Role (${group.roles.length})`],
          ] as [Tab, string][]).map(([key, label]) => (
            <button
              key={key}
              type="button"
              className={tab === key ? 'chat-tab active' : 'chat-tab'}
              onClick={() => setTab(key)}
            >
              {label}
            </button>
          ))}
        </div>

        <div className="chat-modal-body">
          {/* ---------- UMUM ---------- */}
          {tab === 'general' && (
            <form className="chat-form" onSubmit={saveGeneral}>
              <label>
                Nama Grup
                <input value={name} onChange={e => setName(e.target.value)} disabled={!can(ChatPermission.ManageGroup)} />
              </label>
              <label>
                Deskripsi
                <textarea
                  rows={3}
                  value={description}
                  onChange={e => setDescription(e.target.value)}
                  disabled={!can(ChatPermission.ManageGroup)}
                />
              </label>

              <div className="chat-form-actions">
                {can(ChatPermission.ManageGroup) && (
                  <button type="submit" disabled={busy}>
                    Simpan
                  </button>
                )}
              </div>

              <div className="chat-danger-zone">
                {group.isOwner ? (
                  <button type="button" className="chat-danger-button" onClick={removeGroup} disabled={busy}>
                    Hapus Grup
                  </button>
                ) : (
                  <button type="button" className="chat-danger-button" onClick={leaveGroup} disabled={busy}>
                    Keluar dari Grup
                  </button>
                )}
              </div>
            </form>
          )}

          {/* ---------- ANGGOTA ---------- */}
          {tab === 'members' && (
            <div className="chat-members-tab">
              {can(ChatPermission.ManageMembers) && (
                <div className="chat-add-members">
                  <strong>Tambah Anggota</strong>
                  <input
                    value={userSearch}
                    onChange={e => setUserSearch(e.target.value)}
                    placeholder="Cari nama atau user ID..."
                  />

                  <div className="chat-user-options">
                    {userOptions.map(user => (
                      <label key={user.id} className="chat-user-option">
                        <input
                          type="checkbox"
                          checked={selectedUserIds.includes(user.id)}
                          onChange={() => toggleUser(user.id)}
                        />
                        <span>
                          {user.name} <small>@{user.userId}</small>
                        </span>
                      </label>
                    ))}
                    {userOptions.length === 0 && <p className="chat-muted">Tidak ada user yang bisa ditambahkan.</p>}
                  </div>

                  <div className="chat-add-members-actions">
                    <select value={newMemberRoleId} onChange={e => setNewMemberRoleId(e.target.value ? Number(e.target.value) : '')}>
                      <option value="">Role default</option>
                      {assignableRoles.map(role => (
                        <option key={role.id} value={role.id}>
                          {role.name}
                        </option>
                      ))}
                    </select>
                    <button type="button" onClick={addSelectedMembers} disabled={busy || selectedUserIds.length === 0}>
                      Tambah {selectedUserIds.length > 0 ? `(${selectedUserIds.length})` : ''}
                    </button>
                  </div>
                </div>
              )}

              <div className="chat-member-table">
                {group.members.map(member => {
                  const role = group.roles.find(r => r.id === member.roleId)
                  const manageable =
                    can(ChatPermission.ManageMembers) && !member.isOwner && role != null && isBelowMe(role.position)

                  return (
                    <div key={member.userId} className="chat-member-row">
                      <span className={member.online ? 'chat-presence online' : 'chat-presence'} />
                      <span className="chat-member-row-name">
                        {member.name} {member.isOwner && <span title="Owner">👑</span>}
                        <small>@{member.userName}</small>
                      </span>

                      {manageable ? (
                        <>
                          <select
                            value={member.roleId}
                            disabled={busy}
                            onChange={e =>
                              void run(
                                () => updateChatMemberRole(group.id, member.userId, Number(e.target.value)),
                                'Gagal mengubah role.',
                              )
                            }
                          >
                            {assignableRoles.map(r => (
                              <option key={r.id} value={r.id}>
                                {r.name}
                              </option>
                            ))}
                          </select>
                          <button
                            type="button"
                            className="chat-icon-button danger"
                            title="Keluarkan"
                            onClick={() => kickMember(member.userId, member.name)}
                          >
                            ✕
                          </button>
                        </>
                      ) : (
                        <span className="chat-role-chip" style={{ color: role?.color }}>
                          {role?.name}
                        </span>
                      )}
                    </div>
                  )
                })}
              </div>
            </div>
          )}

          {/* ---------- ROLE ---------- */}
          {tab === 'roles' && (
            <div className="chat-roles-tab">
              <div className="chat-role-list">
                {group.roles.map(role => (
                  <div key={role.id} className="chat-role-row">
                    <span className="chat-role-dot" style={{ background: role.color }} />
                    <span className="chat-role-row-name">
                      {role.name}
                      {role.isDefault && <small> (default)</small>}
                      <small>
                        posisi {role.position} · {role.memberCount} anggota
                      </small>
                    </span>

                    {can(ChatPermission.ManageRoles) && isBelowMe(role.position) && (
                      <>
                        <button type="button" className="chat-link-button" onClick={() => startEditRole(role)}>
                          Ubah
                        </button>
                        {!role.isDefault && (
                          <button type="button" className="chat-link-button danger" onClick={() => removeRole(role)}>
                            Hapus
                          </button>
                        )}
                      </>
                    )}
                  </div>
                ))}
              </div>

              {can(ChatPermission.ManageRoles) && editingRoleId === null && (
                <button type="button" onClick={() => startEditRole(null)}>
                  + Role Baru
                </button>
              )}

              {editingRoleId !== null && (
                <form className="chat-form chat-role-form" onSubmit={saveRole}>
                  <strong>{editingRoleId === 'new' ? 'Role Baru' : 'Ubah Role'}</strong>

                  <div className="chat-role-form-row">
                    <label>
                      Nama
                      <input required value={roleForm.name} onChange={e => setRoleForm({ ...roleForm, name: e.target.value })} />
                    </label>
                    <label>
                      Warna
                      <input type="color" value={roleForm.color} onChange={e => setRoleForm({ ...roleForm, color: e.target.value })} />
                    </label>
                    <label>
                      Posisi
                      <input
                        type="number"
                        min={0}
                        max={group.isOwner ? undefined : group.myPosition - 1}
                        value={roleForm.position}
                        onChange={e => setRoleForm({ ...roleForm, position: Number(e.target.value) })}
                      />
                    </label>
                  </div>
                  <small className="chat-muted">
                    Posisi makin besar = makin tinggi. Role hanya bisa mengatur anggota/role dengan posisi di bawahnya.
                  </small>

                  <div className="chat-permission-list">
                    {CHAT_PERMISSION_OPTIONS.map(option => (
                      <label key={option.value} className="chat-permission-option">
                        <input
                          type="checkbox"
                          checked={(roleForm.permissions & option.value) !== 0}
                          disabled={!canGrant(option.value)}
                          onChange={() => togglePermission(option.value)}
                        />
                        <span>
                          {option.label}
                          <small>{option.hint}</small>
                        </span>
                      </label>
                    ))}
                  </div>

                  <div className="chat-form-actions">
                    <button type="button" className="secondary" onClick={() => setEditingRoleId(null)}>
                      Batal
                    </button>
                    <button type="submit" disabled={busy}>
                      Simpan Role
                    </button>
                  </div>
                </form>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
