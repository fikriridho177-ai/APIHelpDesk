import { http } from '../../api/http'
import { normalizeRasterStyle } from './lib/gis/raster'
import { normalizeVectorStyle } from './lib/gis/style'
import type { MapLayer } from './lib/gis/types'

// Semua data peta lewat API .NET kita (api/gis/...), yang membaca database PostGIS WebGIS secara read-only.
// Tile diminta langsung oleh MapLibre, jadi token login ditambahkan lewat transformRequest (lihat map-canvas.tsx).
export const gisBaseUrl = `${String(http.defaults.baseURL ?? '').replace(/\/+$/, '')}/gis`

export function gisAuthHeaders(): Record<string, string> {
  const token = localStorage.getItem('token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

export type GisStatus = { configured: boolean; connected: boolean; rasterAvailable: boolean; message: string | null }

export async function getGisStatus(): Promise<GisStatus> {
  return (await http.get<GisStatus>('/gis/status')).data
}

// API mengirim style mentah dari database; dinormalkan di sini seperti mapLayerDto di WebGIS.
export async function getGisLayers(): Promise<MapLayer[]> {
  const layers = (await http.get<{ layers: MapLayer[] }>('/gis/layers')).data.layers
  return layers.map(layer =>
    layer.layerType === 'RASTER'
      ? { ...layer, style: normalizeRasterStyle(layer.style) }
      : { ...layer, layerType: 'VECTOR', style: normalizeVectorStyle(layer.style, layer.geometryType) },
  )
}
