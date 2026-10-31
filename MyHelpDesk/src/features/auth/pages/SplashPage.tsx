import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'

import splashImage from '../../../assets/gambar/HUT-57.png'
import './SplashPage.css'

// Lama splash tampil sebelum diarahkan ke dashboard (1 - 2 detik).
const SPLASH_DURATION_MS = 1800

export default function SplashPage() {
  const navigate = useNavigate()

  useEffect(() => {
    const timer = window.setTimeout(
      () => navigate('/', { replace: true }),
      SPLASH_DURATION_MS,
    )

    return () => window.clearTimeout(timer)
  }, [navigate])

  return (
    <div
      className="splash-screen"
      style={{ ['--splash-duration' as string]: `${SPLASH_DURATION_MS}ms` }}
    >
      <img
        className="splash-image"
        src={splashImage}
        alt="Sambu Group 57 Years Anniversary - Mengakar Kuat dan Tumbuh Bersama"
      />

      <div className="splash-progress" aria-hidden="true">
        <span />
      </div>
    </div>
  )
}
