import { useEffect, useState } from 'react'

import { getCurrentUserId } from '../../layanan/hooks/useLayanan'
import { useIoTMapMonitoring } from '../hooks/useIoTMapMonitoring'
import { GISViewer } from './map/gis-viewer'
import { getGisStatus, type GisStatus } from '../webgisApi'
import '../webgis.css'

// Map Workspace = viewer WebGIS_PT_RSUP (layer, legenda, basemap, ukur jarak/luas, lokasi saya).
// Dipakai di menu Peta GIS dan di tab "Map Workspace" panel divisi GIS.
// Data dibaca read-only dari database WebGIS; upload layer & edit style tetap di aplikasi WebGIS.
// Pemanggil yang memastikan user punya hak Read di menu GIS_PETA.
export default function MapWorkspace() {
  const iotMonitoring = useIoTMapMonitoring()
  const [status, setStatus] = useState<GisStatus | null>(null)

  useEffect(() => {
    getGisStatus()
      .then(setStatus)
      .catch(() => setStatus({ configured: false, connected: false, rasterAvailable: false, message: 'Status Peta GIS tidak dapat dibaca dari API.' }))
  }, [])

  if (!status) return <div className="lyn-empty">Memeriksa koneksi Peta GIS...</div>

  // Selama database WebGIS belum diberikan: viewer tetap tampil (mode pratinjau) tanpa layer.
  const preview = !status.connected

  return (
    <>
        {preview && (
          <div className="webgis-preview-note">
            <b>Mode pratinjau — layer peta belum tersedia.</b>{' '}
            {status.message ??
              'Database WebGIS belum terhubung. Isi ConnectionStrings:WebGis di appsettings API, lalu restart API.'}
          </div>
        )}
        <div className="webgis-root">
          <GISViewer userId={String(getCurrentUserId())} isAdmin={false} preview={preview} iotMonitoring={iotMonitoring} />
        </div>
    </>
  )
}
