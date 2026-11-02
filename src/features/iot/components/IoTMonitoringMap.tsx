import { useEffect, useMemo, useRef, useState } from 'react'
import * as maplibregl from 'maplibre-gl'
import type { Map as LibreMap, Marker } from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?url'

import { basemaps } from '../../webgis/lib/gis/basemaps'
import { hasValidCoordinates, isMappableDevice, monitoringStatusLabel, monitoringStatusTone } from '../lib/monitoring'
import type { IoTMonitoringDevice } from '../types/iot'
import styles from './IoTMonitoringMap.module.css'

interface IoTMonitoringMapProps {
  devices: IoTMonitoringDevice[]
  selectedLoggerId: number | null
  onSelectLogger: (loggerId: number) => void
  onMapError?: (message: string) => void
}

export default function IoTMonitoringMap({
  devices,
  selectedLoggerId,
  onSelectLogger,
  onMapError,
}: IoTMonitoringMapProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<LibreMap | null>(null)
  const markersRef = useRef<Marker[]>([])
  const markerElementsRef = useRef<Map<number, HTMLButtonElement>>(new Map())
  const latestSelectRef = useRef(onSelectLogger)
  const latestErrorRef = useRef(onMapError)
  const fitSignatureRef = useRef<string | null>(null)
  const [ready, setReady] = useState(false)

  useEffect(() => {
    latestSelectRef.current = onSelectLogger
    latestErrorRef.current = onMapError
  }, [onMapError, onSelectLogger])

  useEffect(() => {
    if (!containerRef.current) return undefined

    let map: LibreMap
    try {
      maplibregl.setWorkerUrl(workerUrl)
      map = new maplibregl.Map({
        container: containerRef.current,
        center: [0, 0],
        zoom: 1,
        maxZoom: 18,
        maxPitch: 0,
        renderWorldCopies: false,
        style: {
          version: 8,
          sources: {},
          layers: [{ id: 'iot-canvas', type: 'background', paint: { 'background-color': '#e8eff0' } }],
        },
        attributionControl: { compact: true },
        transformRequest: url => url.startsWith('https://tile.openstreetmap.org/') || url.startsWith('https://a.tile.opentopomap.org/')
          ? { url, referrerPolicy: 'strict-origin-when-cross-origin' }
          : { url },
      })
    } catch (error) {
      console.error('Gagal membuat peta monitoring IoT:', error)
      latestErrorRef.current?.('Peta belum dapat dibuka. Aktifkan WebGL atau akselerasi perangkat keras di browser, lalu muat ulang.')
      return undefined
    }

    mapRef.current = map
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right')
    map.addControl(new maplibregl.FullscreenControl(), 'top-right')
    map.addControl(new maplibregl.ScaleControl({ maxWidth: 140, unit: 'metric' }), 'bottom-left')

    const basemap = basemaps.find(item => item.id === 'osm-topo')
    map.once('load', () => {
      if (basemap?.tiles) {
        map.addSource('iot-basemap', {
          type: 'raster',
          tiles: basemap.tiles,
          tileSize: 256,
          maxzoom: basemap.maxZoom,
          attribution: basemap.attribution,
        })
        map.addLayer({ id: 'iot-basemap', source: 'iot-basemap', type: 'raster' })
      }
      setReady(true)
    })
    map.on('error', event => {
      if (event.error?.message) latestErrorRef.current?.('Sebagian basemap belum dapat dimuat. Periksa koneksi internet atau gunakan peta tanpa basemap.')
    })

    const resizeObserver = typeof ResizeObserver === 'undefined'
      ? null
      : new ResizeObserver(() => map.resize())
    resizeObserver?.observe(containerRef.current)

    return () => {
      resizeObserver?.disconnect()
      markersRef.current.forEach(marker => marker.remove())
      markersRef.current = []
      markerElementsRef.current.clear()
      map.remove()
      mapRef.current = null
      setReady(false)
    }
  }, [])

  const mappableDevices = useMemo(
    () => devices.filter(isMappableDevice),
    [devices],
  )
  const fitSignature = useMemo(
    () => mappableDevices.map(device => `${device.loggerId}:${device.latitude}:${device.longitude}`).join('|'),
    [mappableDevices],
  )

  useEffect(() => {
    const map = mapRef.current
    if (!ready || !map) return

    markersRef.current.forEach(marker => marker.remove())
    markerElementsRef.current.clear()
    const markerElements = new Map<number, HTMLButtonElement>()
    markersRef.current = mappableDevices.map(device => {
      const element = document.createElement('button')
      const visual = document.createElement('span')
      const tone = monitoringStatusTone(device.status)
      element.type = 'button'
      element.className = `${styles.marker} ${styles[tone]}`
      element.dataset.loggerId = String(device.loggerId)
      visual.className = styles.markerVisual
      visual.setAttribute('aria-hidden', 'true')
      element.appendChild(visual)
      element.title = `${device.loggerCode ?? device.loggerId} · ${monitoringStatusLabel(device.status)}`
      element.setAttribute('aria-label', `Pilih ${device.namaLogger ?? device.loggerCode ?? 'logger'}; status ${monitoringStatusLabel(device.status)}`)
      element.addEventListener('click', event => {
        event.stopPropagation()
        latestSelectRef.current(device.loggerId)
      })
      markerElements.set(device.loggerId, element)
      return new maplibregl.Marker({ element, anchor: 'center' })
        .setLngLat([device.longitude!, device.latitude!])
        .addTo(map)
    })
    markerElementsRef.current = markerElements

    if (fitSignature && fitSignature !== fitSignatureRef.current) {
      const bounds = new maplibregl.LngLatBounds()
      mappableDevices.forEach(device => bounds.extend([device.longitude!, device.latitude!]))
      map.fitBounds(bounds, { padding: 64, maxZoom: 14, duration: 450 })
      fitSignatureRef.current = fitSignature
    }
  }, [fitSignature, mappableDevices, ready])

  useEffect(() => {
    markerElementsRef.current.forEach((element, loggerId) => {
      element.classList.toggle(styles.selected, loggerId === selectedLoggerId)
    })
  }, [ready, selectedLoggerId])

  return (
    <div className={styles.frame}>
      <div className={styles.map} ref={containerRef} aria-label="Peta monitoring perangkat IoT" data-map-ready={ready} />
      {!ready && <div className={styles.mapState}>Menyiapkan peta monitoring...</div>}
      {ready && mappableDevices.length === 0 && <div className={styles.mapState}>Belum ada koordinat valid untuk ditampilkan pada peta.</div>}
      {ready && mappableDevices.length > 0 && devices.length !== mappableDevices.length && <div className={styles.coordinateNote}>{devices.length - mappableDevices.length} perangkat tanpa koordinat valid tetap tersedia di daftar.</div>}
    </div>
  )
}
