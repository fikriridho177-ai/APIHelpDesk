import { useEffect, useState } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

import { http } from '../../../api/http'

export type OnlineUser = {
  id: number
  userId: string
  name: string
  group: string | null
}

// URL hub = URL API tanpa akhiran "/api", contoh http://localhost:5000/hubs/presence
function getHubUrl() {
  const apiUrl = String(http.defaults.baseURL ?? '')
  return `${apiUrl.replace(/\/api\/?$/, '')}/hubs/presence`
}

// Daftar user yang sedang membuka aplikasi (realtime lewat PresenceHub.cs).
export function useOnlineUsers() {
  const [users, setUsers] = useState<OnlineUser[]>([])

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => localStorage.getItem('token') ?? '',
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('OnlineUsers', (list: OnlineUser[]) => setUsers(list))

    let disposed = false

    // Start ditunda satu tick supaya StrictMode (dev) tidak menghentikan koneksi saat negosiasi
    // (lihat useChatConnection.ts).
    const startTimer = setTimeout(() => {
      connection.start().catch(error => {
        if (!disposed) console.error('Gagal terhubung ke presence:', error)
      })
    }, 0)

    return () => {
      disposed = true
      clearTimeout(startTimer)
      void connection.stop()
    }
  }, [])

  return users
}
