import { useCallback, useEffect, useState, type ReactNode } from 'react'

import { DashboardIcon, type IconName } from '../../dashboard/components/DashboardIcon'
import { getLayananErrorMessage } from '../../layanan/api/layananApi'

type Props = {
  title: string
  icon: IconName
  // Elemen di kanan judul (misalnya tautan "Lihat semua").
  action?: ReactNode
  loading?: boolean
  error?: string | null
  onRetry?: () => void
  // Bila diisi, pesan ini tampil menggantikan isi widget.
  empty?: string | null
  children?: ReactNode
}

// Bingkai seragam untuk semua widget portal: judul, loading, error + coba lagi, pesan kosong.
export function WidgetFrame({ title, icon, action, loading, error, onRetry, empty, children }: Props) {
  let body: ReactNode = children

  if (loading) body = <div className="portal-widget-state">Memuat...</div>
  else if (error)
    body = (
      <div className="portal-widget-state error">
        <span>{error}</span>
        {onRetry && (
          <button type="button" className="lyn-button ghost" onClick={onRetry}>
            Coba lagi
          </button>
        )}
      </div>
    )
  else if (empty) body = <div className="portal-widget-state">{empty}</div>

  return (
    <section className="portal-widget">
      <header className="portal-widget-head">
        <span className="portal-widget-icon">
          <DashboardIcon name={icon} />
        </span>
        <h2>{title}</h2>
        {action && <div className="portal-widget-action">{action}</div>}
      </header>
      <div className="portal-widget-body">{body}</div>
    </section>
  )
}

// Memuat data satu widget. Error ditangkap per widget, jadi widget lain tetap tampil.
export function useWidgetData<T>(load: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(load, deps)

  useEffect(() => {
    let active = true
    setLoading(true)
    setError(null)

    run()
      .then(result => active && setData(result))
      .catch(err => active && setError(getLayananErrorMessage(err, 'Gagal memuat data.')))
      .finally(() => active && setLoading(false))

    return () => {
      active = false
    }
  }, [run, attempt])

  return { data, loading, error, reload: () => setAttempt(n => n + 1) }
}
