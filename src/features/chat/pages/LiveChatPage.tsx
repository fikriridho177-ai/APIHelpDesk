import { FormEvent, KeyboardEvent, useEffect, useMemo, useRef, useState } from 'react'

import {
  createChatGroup,
  deleteChatMessage,
  editChatMessage,
  getChatErrorMessage,
  getChatGroup,
  getChatMessages,
  getMyChatGroups,
  markChatRead,
  sendChatMessage,
} from '../api/chatApi'
import { ChatPermission, hasChatPermission } from '../chatPermissions'
import GroupSettingsModal from '../components/GroupSettingsModal'
import { useChatConnection } from '../hooks/useChatConnection'
import type { ChatGroupDetail, ChatGroupSummary, ChatMessage } from '../types/chat'
import { getMyMenuPermission, type CurrentMenuPermission } from '../../security/api/securityApi'
import './LiveChatPage.css'

const TYPING_TIMEOUT_MS = 3000
const TYPING_SEND_INTERVAL_MS = 2000

/* =========================================================
   HELPER
========================================================= */

function getCurrentUserId() {
  try {
    return Number(JSON.parse(localStorage.getItem('auth') ?? '{}').id) || 0
  } catch {
    return 0
  }
}

function formatTime(value: string) {
  return new Date(value).toLocaleTimeString('id-ID', { hour: '2-digit', minute: '2-digit' })
}

function formatDay(value: string) {
  return new Date(value).toLocaleDateString('id-ID', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
}

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map(part => part[0]!.toUpperCase())
    .join('')
}

/* =========================================================
   HALAMAN
========================================================= */

type Props = {
  // 'page' = halaman penuh 3 kolom, 'widget' = jendela kecil di pojok kanan bawah.
  variant?: 'page' | 'widget'
  // Khusus widget: false saat jendela ditutup (pesan tidak dianggap terbaca).
  visible?: boolean
  // Khusus widget: true = jendela diperbesar layar penuh (tampil 3 kolom).
  expanded?: boolean
  onToggleExpand?: () => void
  onUnreadChange?: (total: number) => void
  onClose?: () => void
}

export default function LiveChatPage({
  variant = 'page',
  visible = true,
  expanded = false,
  onToggleExpand,
  onUnreadChange,
  onClose,
}: Props) {
  const isWidget = variant === 'widget'
  // Widget kecil hanya menampilkan satu panel: daftar grup ATAU percakapan.
  const isCompact = isWidget && !expanded
  const currentUserId = useMemo(getCurrentUserId, [])

  const [menuPermission, setMenuPermission] = useState<CurrentMenuPermission | null>(null)
  const [groups, setGroups] = useState<ChatGroupSummary[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [detail, setDetail] = useState<ChatGroupDetail | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [hasOlder, setHasOlder] = useState(false)
  const [onlineIds, setOnlineIds] = useState<Set<number>>(new Set())
  const [typingUsers, setTypingUsers] = useState<Record<number, { name: string; until: number }>>({})

  const [draft, setDraft] = useState('')
  const [editingMessageId, setEditingMessageId] = useState<number | null>(null)
  const [editDraft, setEditDraft] = useState('')
  const [showSettings, setShowSettings] = useState(false)
  const [showCreate, setShowCreate] = useState(false)
  const [newGroupName, setNewGroupName] = useState('')
  const [newGroupDescription, setNewGroupDescription] = useState('')

  // Grup yang sedang benar-benar terlihat di layar (null jika widget tertutup).
  const selectedIdRef = useRef<number | null>(null)
  selectedIdRef.current = visible ? selectedId : null

  const messagesEndRef = useRef<HTMLDivElement | null>(null)
  const lastTypingSentRef = useRef(0)

  /* ---------- Load data ---------- */

  async function loadGroups() {
    setGroups(await getMyChatGroups())
  }

  async function loadDetail(groupId: number) {
    const data = await getChatGroup(groupId)
    setDetail(data)
    setOnlineIds(current => {
      const next = new Set(current)
      data.members.forEach(member => (member.online ? next.add(member.userId) : next.delete(member.userId)))
      return next
    })
  }

  async function loadMessages(groupId: number) {
    const data = await getChatMessages(groupId)
    setMessages(data)
    setHasOlder(data.length >= 50)
    await markAsRead(groupId, data)
  }

  async function markAsRead(groupId: number, list: ChatMessage[]) {
    const lastId = list[list.length - 1]?.id
    if (!lastId) return

    await markChatRead(groupId, lastId)
    setGroups(current => current.map(group => (group.id === groupId ? { ...group, unreadCount: 0 } : group)))
  }

  async function loadOlderMessages() {
    if (!selectedId || messages.length === 0) return

    const older = await getChatMessages(selectedId, messages[0].id)
    setMessages(current => [...older, ...current])
    setHasOlder(older.length >= 50)
  }

  useEffect(() => {
    getMyMenuPermission('LIVE_CHAT')
      .then(permission => {
        setMenuPermission(permission)
        if (permission.canView) void loadGroups()
      })
      .catch(error => console.error('Gagal mengambil permission LIVE_CHAT:', error))
  }, [])

  // Ganti grup yang dibuka.
  useEffect(() => {
    setDetail(null)
    setMessages([])
    setTypingUsers({})
    setEditingMessageId(null)
    setDraft('')

    if (selectedId) {
      Promise.all([loadDetail(selectedId), loadMessages(selectedId)]).catch(error =>
        alert(getChatErrorMessage(error, 'Gagal membuka grup.')),
      )
    }
  }, [selectedId])

  // Scroll otomatis ke pesan terbaru.
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ block: 'end' })
  }, [messages.length, selectedId, visible])

  // Widget dibuka kembali -> pesan di grup yang sedang dibuka dianggap terbaca.
  useEffect(() => {
    if (visible && selectedId && messages.length > 0) void markAsRead(selectedId, messages)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible])

  // Laporkan total pesan belum dibaca (untuk badge tombol melayang).
  useEffect(() => {
    onUnreadChange?.(groups.reduce((total, group) => total + group.unreadCount, 0))
  }, [groups, onUnreadChange])

  // Bersihkan indikator mengetik yang sudah kedaluwarsa.
  useEffect(() => {
    const timer = window.setInterval(() => {
      const now = Date.now()
      setTypingUsers(current => {
        const active = Object.entries(current).filter(([, value]) => value.until > now)
        return active.length === Object.keys(current).length ? current : Object.fromEntries(active)
      })
    }, 1000)

    return () => window.clearInterval(timer)
  }, [])

  /* ---------- Event realtime ---------- */

  function replaceMessage(updated: ChatMessage) {
    setMessages(current => current.map(message => (message.id === updated.id ? updated : message)))
  }

  const { connected, sendTyping } = useChatConnection({
    onMessageCreated: message => {
      const isOpen = message.groupId === selectedIdRef.current
      const isMine = message.senderUserId === currentUserId

      if (isOpen) {
        setMessages(current => (current.some(m => m.id === message.id) ? current : [...current, message]))
        setTypingUsers(current => {
          const { [message.senderUserId]: _removed, ...rest } = current
          return rest
        })
        if (!isMine) void markChatRead(message.groupId, message.id)
      }

      setGroups(current =>
        current
          .map(group =>
            group.id === message.groupId
              ? {
                  ...group,
                  lastMessage: message.content,
                  lastMessageAt: message.createdAt,
                  unreadCount: isOpen || isMine ? group.unreadCount : group.unreadCount + 1,
                }
              : group,
          )
          .sort((a, b) => (b.lastMessageAt ?? '').localeCompare(a.lastMessageAt ?? '')),
      )
    },
    onMessageUpdated: replaceMessage,
    onMessageDeleted: replaceMessage,
    onGroupChanged: groupId => {
      void loadGroups()
      if (groupId === selectedIdRef.current) void loadDetail(groupId)
    },
    onAddedToGroup: () => void loadGroups(),
    onRemovedFromGroup: groupId => {
      void loadGroups()
      if (groupId === selectedIdRef.current) {
        setSelectedId(null)
        setShowSettings(false)
        alert('Anda tidak lagi menjadi anggota grup ini.')
      }
    },
    onPresenceChanged: (userId, online) =>
      setOnlineIds(current => {
        const next = new Set(current)
        if (online) next.add(userId)
        else next.delete(userId)
        return next
      }),
    onTyping: (groupId, userId, name) => {
      if (groupId !== selectedIdRef.current) return
      setTypingUsers(current => ({ ...current, [userId]: { name, until: Date.now() + TYPING_TIMEOUT_MS } }))
    },
    onReconnected: () => {
      void loadGroups()
      if (selectedIdRef.current) {
        void loadDetail(selectedIdRef.current)
        void loadMessages(selectedIdRef.current)
      }
    },
  })

  /* ---------- Aksi ---------- */

  const can = (permission: number) => (detail ? hasChatPermission(detail.myPermissions, permission) : false)

  async function handleSend(e?: FormEvent) {
    e?.preventDefault()
    if (!selectedId || !draft.trim()) return

    const content = draft
    setDraft('')

    try {
      const message = await sendChatMessage(selectedId, content)
      setMessages(current => (current.some(m => m.id === message.id) ? current : [...current, message]))
    } catch (error) {
      setDraft(content)
      alert(getChatErrorMessage(error, 'Gagal mengirim pesan.'))
    }
  }

  function handleDraftKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    // Enter = kirim, Shift+Enter = baris baru.
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void handleSend()
    }
  }

  function handleDraftChange(value: string) {
    setDraft(value)

    const now = Date.now()
    if (selectedId && value && now - lastTypingSentRef.current > TYPING_SEND_INTERVAL_MS) {
      lastTypingSentRef.current = now
      sendTyping(selectedId)
    }
  }

  async function saveEdit(messageId: number) {
    if (!editDraft.trim()) return

    try {
      replaceMessage(await editChatMessage(messageId, editDraft))
      setEditingMessageId(null)
    } catch (error) {
      alert(getChatErrorMessage(error, 'Gagal mengedit pesan.'))
    }
  }

  async function removeMessage(message: ChatMessage) {
    if (!window.confirm('Hapus pesan ini?')) return

    try {
      await deleteChatMessage(message.id)
    } catch (error) {
      alert(getChatErrorMessage(error, 'Gagal menghapus pesan.'))
    }
  }

  async function handleCreateGroup(e: FormEvent) {
    e.preventDefault()

    try {
      const group = await createChatGroup(newGroupName, newGroupDescription)
      setShowCreate(false)
      setNewGroupName('')
      setNewGroupDescription('')
      await loadGroups()
      setSelectedId(group.id)
    } catch (error) {
      alert(getChatErrorMessage(error, 'Gagal membuat grup.'))
    }
  }

  /* ---------- Data turunan ---------- */

  // Anggota dikelompokkan per role (posisi tertinggi di atas), seperti Discord.
  const membersByRole = useMemo(() => {
    if (!detail) return []

    return detail.roles
      .map(role => ({
        role,
        members: detail.members
          .filter(member => member.roleId === role.id)
          .map(member => ({ ...member, online: onlineIds.has(member.userId) }))
          .sort((a, b) => Number(b.online) - Number(a.online) || a.name.localeCompare(b.name)),
      }))
      .filter(section => section.members.length > 0)
  }, [detail, onlineIds])

  const roleColorByUser = useMemo(() => {
    const map = new Map<number, string>()
    detail?.members.forEach(member => {
      const color = detail.roles.find(role => role.id === member.roleId)?.color
      if (color) map.set(member.userId, color)
    })
    return map
  }, [detail])

  const typingNames = Object.values(typingUsers).map(user => user.name)
  const selectedSummary = groups.find(group => group.id === selectedId)

  /* ---------- Render ---------- */

  // Tombol perbesar / perkecil & tutup untuk jendela chat melayang.
  const windowControls = (
    <div className="chat-window-controls">
      <button
        type="button"
        className="chat-icon-button"
        title={expanded ? 'Perkecil' : 'Layar penuh'}
        aria-label={expanded ? 'Perkecil' : 'Layar penuh'}
        onClick={onToggleExpand}
      >
        <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d={expanded ? 'M4 14h6v6 M20 10h-6V4 M14 10l7-7 M3 21l7-7' : 'M15 3h6v6 M9 21H3v-6 M21 3l-7 7 M3 21l7-7'} />
        </svg>
      </button>
      <button type="button" className="chat-icon-button" title="Tutup" aria-label="Tutup" onClick={onClose}>
        ✕
      </button>
    </div>
  )

  if (menuPermission && !menuPermission.canView) {
    return <div className="permission-info">Anda tidak memiliki akses ke menu Live Chat.</div>
  }

  return (
    <section className={['live-chat', isWidget && 'widget', isCompact && 'compact', selectedId && 'has-selection'].filter(Boolean).join(' ')}>
      {/* DAFTAR GRUP */}
      <aside className="chat-sidebar">
        <div className="chat-sidebar-header">
          <div>
            <h2>Live Chat</h2>
            <span className={connected ? 'chat-status online' : 'chat-status'}>{connected ? 'Terhubung' : 'Menghubungkan...'}</span>
          </div>
          {isWidget && windowControls}
        </div>

        {menuPermission?.canCreate && (
          <button type="button" className="chat-create-button" onClick={() => setShowCreate(true)}>
            + Grup Baru
          </button>
        )}

        <div className="chat-group-list">
          {groups.map(group => (
            <button
              key={group.id}
              type="button"
              className={group.id === selectedId ? 'chat-group-item active' : 'chat-group-item'}
              onClick={() => setSelectedId(group.id)}
            >
              <span className="chat-avatar">{initials(group.name)}</span>
              <span className="chat-group-text">
                <strong>{group.name}</strong>
                <small>{group.lastMessage || `${group.memberCount} anggota`}</small>
              </span>
              {group.unreadCount > 0 && (
                <span className="chat-unread-badge">{group.unreadCount > 99 ? '99+' : group.unreadCount}</span>
              )}
            </button>
          ))}

          {groups.length === 0 && (
            <p className="chat-muted chat-empty-list">
              Anda belum tergabung di grup mana pun.
              {menuPermission?.canCreate ? ' Buat grup baru untuk memulai.' : ' Minta admin grup untuk menambahkan Anda.'}
            </p>
          )}
        </div>
      </aside>

      {/* PERCAKAPAN */}
      <div className="chat-main">
        {!selectedId ? (
          <div className="chat-placeholder">
            <div className="chat-placeholder-icon">💬</div>
            <h3>Pilih grup untuk mulai chat</h3>
            <p>Pesan dikirim dan diterima secara langsung (realtime).</p>
          </div>
        ) : (
          <>
            <header className="chat-main-header">
              {/* Widget hanya menampilkan satu panel, jadi perlu tombol kembali ke daftar grup */}
              {isCompact && (
                <button type="button" className="chat-icon-button" title="Kembali" onClick={() => setSelectedId(null)}>
                  ←
                </button>
              )}
              <div className="chat-main-title">
                <h3># {detail?.name ?? selectedSummary?.name}</h3>
                {detail?.description && <small>{detail.description}</small>}
              </div>
              <button type="button" className="chat-icon-button" title="Pengaturan grup" onClick={() => setShowSettings(true)}>
                ⚙
              </button>
              {isCompact && windowControls}
            </header>

            <div className="chat-messages">
              {hasOlder && (
                <button type="button" className="chat-load-older" onClick={() => void loadOlderMessages()}>
                  Muat pesan sebelumnya
                </button>
              )}

              {messages.map((message, index) => {
                const previous = messages[index - 1]
                const isMine = message.senderUserId === currentUserId
                const newDay = !previous || formatDay(previous.createdAt) !== formatDay(message.createdAt)
                // Pesan beruntun dari pengirim yang sama (< 5 menit) digabung tanpa header.
                const grouped =
                  !newDay &&
                  previous?.senderUserId === message.senderUserId &&
                  new Date(message.createdAt).getTime() - new Date(previous.createdAt).getTime() < 5 * 60 * 1000

                const canDelete = !message.isDeleted && (isMine || can(ChatPermission.ManageMessages))
                const canEdit = !message.isDeleted && isMine

                return (
                  <div key={message.id}>
                    {newDay && <div className="chat-day-divider">{formatDay(message.createdAt)}</div>}

                    <div className={grouped ? 'chat-message grouped' : 'chat-message'}>
                      {!grouped ? (
                        <span className="chat-avatar small" style={{ background: roleColorByUser.get(message.senderUserId) }}>
                          {initials(message.senderName)}
                        </span>
                      ) : (
                        <span className="chat-message-time-gutter">{formatTime(message.createdAt)}</span>
                      )}

                      <div className="chat-message-body">
                        {!grouped && (
                          <div className="chat-message-meta">
                            <strong style={{ color: roleColorByUser.get(message.senderUserId) }}>{message.senderName}</strong>
                            <small>{formatTime(message.createdAt)}</small>
                          </div>
                        )}

                        {editingMessageId === message.id ? (
                          <div className="chat-edit-box">
                            <textarea
                              autoFocus
                              rows={2}
                              value={editDraft}
                              onChange={e => setEditDraft(e.target.value)}
                              onKeyDown={e => {
                                if (e.key === 'Enter' && !e.shiftKey) {
                                  e.preventDefault()
                                  void saveEdit(message.id)
                                }
                                if (e.key === 'Escape') setEditingMessageId(null)
                              }}
                            />
                            <small className="chat-muted">Enter untuk simpan · Esc untuk batal</small>
                          </div>
                        ) : message.isDeleted ? (
                          <p className="chat-message-deleted">Pesan telah dihapus</p>
                        ) : (
                          <p className="chat-message-content">
                            {message.content}
                            {message.editedAt && <small className="chat-edited"> (diedit)</small>}
                          </p>
                        )}
                      </div>

                      {(canEdit || canDelete) && editingMessageId !== message.id && (
                        <div className="chat-message-actions">
                          {canEdit && (
                            <button
                              type="button"
                              title="Edit"
                              onClick={() => {
                                setEditingMessageId(message.id)
                                setEditDraft(message.content)
                              }}
                            >
                              ✎
                            </button>
                          )}
                          {canDelete && (
                            <button type="button" title="Hapus" onClick={() => void removeMessage(message)}>
                              🗑
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                )
              })}

              {messages.length === 0 && detail && <p className="chat-muted chat-no-messages">Belum ada pesan. Mulai percakapan!</p>}
              <div ref={messagesEndRef} />
            </div>

            <div className="chat-typing">
              {typingNames.length > 0 &&
                `${typingNames.slice(0, 3).join(', ')}${typingNames.length > 3 ? ' dan lainnya' : ''} sedang mengetik...`}
            </div>

            {can(ChatPermission.SendMessages) ? (
              <form className="chat-composer" onSubmit={handleSend}>
                <textarea
                  rows={1}
                  value={draft}
                  onChange={e => handleDraftChange(e.target.value)}
                  onKeyDown={handleDraftKeyDown}
                  placeholder={`Kirim pesan ke #${detail?.name ?? ''}`}
                  maxLength={4000}
                />
                <button type="submit" className="chat-send-button" disabled={!draft.trim()} title="Kirim (Enter)" aria-label="Kirim">
                  <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M22 2L11 13 M22 2l-7 20-4-9-9-4 20-7z" />
                  </svg>
                </button>
              </form>
            ) : (
              detail && <div className="chat-readonly">Anda tidak memiliki permission untuk mengirim pesan di grup ini.</div>
            )}
          </>
        )}
      </div>

      {/* ANGGOTA (di widget: lihat lewat Pengaturan > Anggota) */}
      {!isCompact && selectedId && detail && (
        <aside className="chat-members">
          {membersByRole.map(({ role, members }) => (
            <div key={role.id} className="chat-member-section">
              <h4>
                {role.name} — {members.length}
              </h4>
              {members.map(member => (
                <div key={member.userId} className={member.online ? 'chat-member' : 'chat-member offline'}>
                  <span className="chat-avatar small" style={{ background: role.color }}>
                    {initials(member.name)}
                    <span className={member.online ? 'chat-presence online' : 'chat-presence'} />
                  </span>
                  <span className="chat-member-name" style={{ color: role.color }}>
                    {member.name}
                    {member.isOwner && <span title="Owner"> 👑</span>}
                  </span>
                </div>
              ))}
            </div>
          ))}
        </aside>
      )}

      {/* MODAL */}
      {showSettings && detail && (
        <GroupSettingsModal
          group={{ ...detail, members: detail.members.map(m => ({ ...m, online: onlineIds.has(m.userId) })) }}
          currentUserId={currentUserId}
          onClose={() => setShowSettings(false)}
          onLeftGroup={() => {
            setShowSettings(false)
            setSelectedId(null)
            void loadGroups()
          }}
        />
      )}

      {showCreate && (
        <div className="chat-modal-backdrop" onMouseDown={() => setShowCreate(false)}>
          <form className="chat-modal chat-form" onMouseDown={e => e.stopPropagation()} onSubmit={handleCreateGroup}>
            <div className="chat-modal-header">
              <h2>Buat Grup Baru</h2>
            </div>
            <div className="chat-modal-body">
              <label>
                Nama Grup
                <input required autoFocus value={newGroupName} onChange={e => setNewGroupName(e.target.value)} maxLength={100} />
              </label>
              <label>
                Deskripsi
                <textarea rows={3} value={newGroupDescription} onChange={e => setNewGroupDescription(e.target.value)} maxLength={500} />
              </label>
              <small className="chat-muted">Anda menjadi owner grup. Role "Admin" dan "Member" dibuat otomatis.</small>
              <div className="chat-form-actions">
                <button type="button" className="secondary" onClick={() => setShowCreate(false)}>
                  Batal
                </button>
                <button type="submit">Buat Grup</button>
              </div>
            </div>
          </form>
        </div>
      )}
    </section>
  )
}
