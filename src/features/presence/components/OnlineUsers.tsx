import { useState } from 'react'

import { useOnlineUsers } from '../hooks/useOnlineUsers'

// Jumlah user online di navbar; klik / arahkan kursor untuk melihat daftar namanya.
export default function OnlineUsers() {
  const users = useOnlineUsers()
  const [open, setOpen] = useState(false)

  return (
    <div
      className="online-users"
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      <button type="button" className="online-users-button" onClick={() => setOpen(value => !value)}>
        <span className="online-users-dot" />
        {users.length} online
      </button>

      {open && (
        <div className="online-users-dropdown">
          <div className="online-users-title">User online ({users.length})</div>

          {users.length === 0 && <p className="online-users-empty">Belum ada data.</p>}

          {users.map(user => (
            <div key={user.id} className="online-users-item">
              <span className="online-users-dot" />
              <div>
                <div className="online-users-name">{user.name}</div>
                <div className="online-users-group">{user.group ?? user.userId}</div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
