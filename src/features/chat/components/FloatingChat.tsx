import { useEffect, useState } from 'react'

import { getMyMenuPermission } from '../../security/api/securityApi'
import { OPEN_LIVE_CHAT_EVENT } from '../openLiveChat'
import LiveChatPage from '../pages/LiveChatPage'
import './FloatingChat.css'

// Tombol chat melayang di pojok kanan bawah + jendela chat (kecil / layar penuh).
// Hanya tampil untuk user yang punya akses View menu LIVE_CHAT.
export default function FloatingChat() {
  const [allowed, setAllowed] = useState(false)
  const [open, setOpen] = useState(false)
  const [expanded, setExpanded] = useState(false)
  const [unread, setUnread] = useState(0)

  useEffect(() => {
    getMyMenuPermission('LIVE_CHAT')
      .then(permission => setAllowed(permission.canView))
      .catch(() => setAllowed(false)) // menu belum ada / tidak punya akses
  }, [])

  // Dibuka dari tombol dashboard / URL /live-chat (lihat openLiveChat.ts).
  useEffect(() => {
    const handleOpen = () => setOpen(true)
    window.addEventListener(OPEN_LIVE_CHAT_EVENT, handleOpen)
    return () => window.removeEventListener(OPEN_LIVE_CHAT_EVENT, handleOpen)
  }, [])

  // Esc keluar dari layar penuh.
  useEffect(() => {
    if (!expanded) return

    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !document.querySelector('.chat-modal')) setExpanded(false)
    }

    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [expanded])

  function close() {
    setOpen(false)
    setExpanded(false)
  }

  if (!allowed) return null

  const isFullscreen = open && expanded

  return (
    <div className={isFullscreen ? 'floating-chat fullscreen' : 'floating-chat'}>
      {isFullscreen && <div className="floating-chat-backdrop" onClick={() => setExpanded(false)} />}

      {/* Tetap di-mount saat tertutup supaya pesan masuk & badge tetap realtime */}
      <div className={expanded ? 'floating-chat-window expanded' : 'floating-chat-window'} hidden={!open}>
        <LiveChatPage
          variant="widget"
          visible={open}
          expanded={expanded}
          onToggleExpand={() => setExpanded(value => !value)}
          onUnreadChange={setUnread}
          onClose={close}
        />
      </div>

      {!isFullscreen && (
        <button
          type="button"
          className={open ? 'floating-chat-button open' : 'floating-chat-button'}
          onClick={() => (open ? close() : setOpen(true))}
          title={open ? 'Tutup Live Chat' : 'Buka Live Chat'}
          aria-label={open ? 'Tutup Live Chat' : 'Buka Live Chat'}
        >
          {open ? (
            <span className="floating-chat-close">✕</span>
          ) : (
            <svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M21 12a8 8 0 0 1-11.6 7.1L4 21l1.9-5.4A8 8 0 1 1 21 12z M8 11h.01 M12 11h.01 M16 11h.01" />
            </svg>
          )}

          {!open && unread > 0 && <span className="floating-chat-badge">{unread > 99 ? '99+' : unread}</span>}
        </button>
      )}
    </div>
  )
}
