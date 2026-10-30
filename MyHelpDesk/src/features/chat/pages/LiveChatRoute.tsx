import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'

import { openLiveChat } from '../openLiveChat'

// URL /live-chat (dari menu navbar / kartu dashboard) tidak lagi membuka halaman penuh:
// jendela chat melayang dibuka, lalu user dikembalikan ke halaman sebelumnya.
export default function LiveChatRoute() {
  const navigate = useNavigate()

  useEffect(() => {
    openLiveChat()

    const hasPreviousPage = window.history.state?.idx > 0
    if (hasPreviousPage) navigate(-1)
    else navigate('/', { replace: true })
  }, [navigate])

  return null
}
