import { useEffect, useRef, useState } from 'react'
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'

import { http } from '../../../api/http'
import type { ChatMessage } from '../types/chat'

// Event dari server (lihat ChatHub.cs).
export type ChatHubHandlers = {
  onMessageCreated?: (message: ChatMessage) => void
  onMessageUpdated?: (message: ChatMessage) => void
  onMessageDeleted?: (message: ChatMessage) => void
  onGroupChanged?: (groupId: number) => void
  onAddedToGroup?: (groupId: number) => void
  onRemovedFromGroup?: (groupId: number) => void
  onPresenceChanged?: (userId: number, online: boolean) => void
  onTyping?: (groupId: number, userId: number, name: string) => void
  onReconnected?: () => void
}

// URL hub = URL API tanpa akhiran "/api", contoh http://localhost:5000/hubs/chat
function getHubUrl() {
  const apiUrl = String(http.defaults.baseURL ?? '')
  return `${apiUrl.replace(/\/api\/?$/, '')}/hubs/chat`
}

export function useChatConnection(handlers: ChatHubHandlers) {
  const [connected, setConnected] = useState(false)
  const connectionRef = useRef<HubConnection | null>(null)

  // Simpan handler terbaru supaya koneksi tidak perlu dibuat ulang setiap render.
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => localStorage.getItem('token') ?? '',
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    const on = <K extends keyof ChatHubHandlers>(event: string, key: K) =>
      connection.on(event, (...args: any[]) => (handlersRef.current[key] as any)?.(...args))

    on('MessageCreated', 'onMessageCreated')
    on('MessageUpdated', 'onMessageUpdated')
    on('MessageDeleted', 'onMessageDeleted')
    on('GroupChanged', 'onGroupChanged')
    on('AddedToGroup', 'onAddedToGroup')
    on('RemovedFromGroup', 'onRemovedFromGroup')
    on('PresenceChanged', 'onPresenceChanged')
    on('Typing', 'onTyping')

    connection.onreconnecting(() => setConnected(false))
    connection.onreconnected(() => {
      setConnected(true)
      handlersRef.current.onReconnected?.()
    })
    connection.onclose(() => setConnected(false))

    connectionRef.current = connection
    let disposed = false

    // Start ditunda satu tick: di React StrictMode (dev) effect dijalankan 2x (mount -> cleanup -> mount).
    // Tanpa jeda, koneksi pertama langsung dihentikan saat negosiasi dan muncul error
    // "The connection was stopped during negotiation". Dengan jeda, koneksi pertama tidak pernah dimulai.
    const startTimer = setTimeout(() => {
      connection
        .start()
        .then(() => setConnected(true))
        .catch(error => {
          if (!disposed) console.error('Gagal terhubung ke live chat:', error)
        })
    }, 0)

    return () => {
      disposed = true
      clearTimeout(startTimer)
      connectionRef.current = null
      void connection.stop()
    }
  }, [])

  function sendTyping(groupId: number) {
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('Typing', groupId).catch(() => undefined)
    }
  }

  return { connected, sendTyping }
}
