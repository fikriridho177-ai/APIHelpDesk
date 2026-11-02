import { useEffect, useMemo, useState } from 'react'

import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTDevices } from '../../iot/api/iotApi'
import type { IoTDevice } from '../../iot/types/iot'
import WeatherForecastPanel from '../components/WeatherForecastPanel'
import type { WeatherSensorLocation } from '../types/weather'

const MENU_CODE = 'DASHBOARD_IOT'

interface WeatherForecastPageProps {
  permissionCode?: string
}

export default function WeatherForecastPage({ permissionCode = MENU_CODE }: WeatherForecastPageProps) {
  const { permission, loaded } = useMenuPermission(permissionCode)
  const [devices, setDevices] = useState<IoTDevice[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!loaded || !permission?.canRead) return undefined
    let active = true
    setLoading(true)
    setError(null)
    getIoTDevices()
      .then(response => {
        if (active) setDevices(response.data)
      })
      .catch(loadError => {
        if (!active) return
        console.error('Gagal memuat logger untuk prakiraan cuaca:', loadError)
        setDevices([])
        setError('Daftar logger untuk prakiraan cuaca gagal dimuat.')
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => { active = false }
  }, [loaded, permission?.canRead])

  const sensorLocations = useMemo<WeatherSensorLocation[]>(() => devices.map(device => ({
    loggerId: device.loggerId,
    loggerCode: device.loggerCode,
    namaLogger: device.namaLogger,
    lokasi: device.lokasi,
    sourceType: device.sourceType,
    latitude: device.latitude,
    longitude: device.longitude,
  })), [devices])

  if (!loaded || !permission?.canRead) return <NoAccess loaded={loaded} />

  return (
    <main style={{ display: 'grid', gap: 16, padding: '18px 0 30px' }}>
      {loading && <div style={{ padding: 24, color: '#78949d' }}>Memuat daftar logger...</div>}
      {!loading && error && <div role="alert" style={{ padding: 16, color: '#9f2525' }}>{error}</div>}
      {!loading && !error && devices.length === 0 && <div style={{ padding: 24, color: '#78949d' }}>Belum ada logger yang dapat dipakai untuk prakiraan.</div>}
      {!loading && !error && devices.length > 0 && <WeatherForecastPanel sensorLocations={sensorLocations} comfortable />}
    </main>
  )
}
