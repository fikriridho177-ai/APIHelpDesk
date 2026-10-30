import { useEffect, useRef, useState } from 'react'
import type { Map as LeafletMap } from 'leaflet'
import { useSensorData } from '../hooks/useSensorData'
import { formatDate, daysAgoStr, todayStr } from '../utils/format'
import type { DateRange } from '../types'
import FilterBar from '../components/FilterBar'
import styles from './PetaPage.module.css'

// Leaflet import — CSS wajib diimport di main.tsx atau App.tsx
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

// Fix default marker icons yang hilang karena bundler
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl:       'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl:     'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

function makeIcon(emoji: string, color: string) {
  const html = `<div style="
    background:${color};color:#fff;border-radius:50%;
    width:30px;height:30px;display:flex;align-items:center;
    justify-content:center;font-size:14px;
    box-shadow:0 2px 8px rgba(0,0,0,0.3);
  ">${emoji}</div>`
  return L.divIcon({ className: '', html, iconSize: [30, 30], iconAnchor: [15, 15] })
}

function parseCoordinate(value: unknown): number | null {
  if (value === null || value === undefined) return null

  const raw = String(value).trim()
  if (!raw) return null

  // Tetap mendukung format decimal lama, misalnya:
  // 0.363889 / 103.491389
  const normalized = raw.replace(/,/g, '.')

  if (/^[+-]?\d+(?:\.\d+)?$/.test(normalized)) {
    const decimal = Number(normalized)
    return Number.isFinite(decimal) ? decimal : null
  }

  // Mendukung format DMS dari database, misalnya:
  // Latitude  : 0°21'50
  // Longitude : 103°29'29
  // Juga mendukung N/S/E/W bila suatu saat tersedia.
  const parts = normalized.match(/[+-]?\d+(?:\.\d+)?/g)

  if (!parts || parts.length === 0) {
    return null
  }

  const degreesRaw = Number(parts[0])
  const minutes = parts.length >= 2 ? Number(parts[1]) : 0
  const seconds = parts.length >= 3 ? Number(parts[2]) : 0

  if (
    !Number.isFinite(degreesRaw) ||
    !Number.isFinite(minutes) ||
    !Number.isFinite(seconds) ||
    minutes < 0 || minutes >= 60 ||
    seconds < 0 || seconds >= 60
  ) {
    return null
  }

  let sign = degreesRaw < 0 ? -1 : 1

  const hemisphere = normalized
    .match(/[NSEW]/i)?.[0]
    ?.toUpperCase()

  if (hemisphere === 'S' || hemisphere === 'W') {
    sign = -1
  } else if (hemisphere === 'N' || hemisphere === 'E') {
    sign = 1
  }

  const decimal =
    Math.abs(degreesRaw) +
    minutes / 60 +
    seconds / 3600

  return sign * decimal
}

function getCoordinates(row: any) {
  // Prioritas tetap sama seperti kode lama:
  // KoordinatY = Latitude, KoordinatX = Longitude.
  // Jika field Koordinat kosong, fallback ke Latitude/Longitude.
  const lat =
    parseCoordinate(row.KoordinatY) ??
    parseCoordinate(row.Latitude)

  const lng =
    parseCoordinate(row.KoordinatX) ??
    parseCoordinate(row.Longitude)

  if (lat === null || lng === null) {
    return null
  }

  // Validasi range koordinat setelah DMS dikonversi ke decimal degree.
  if (lat < -90 || lat > 90) {
    return null
  }

  if (lng < -180 || lng > 180) {
    return null
  }

  return {
    lat,
    lng,
  }
}

// Satu sensor hanya menghasilkan satu marker pada peta.
// Data yang masuk ke fungsi ini adalah hasil fetch berdasarkan filter tanggal.
function getSensorKey(row: any, sensorType: 'curahHujan' | 'tmak') {
  const sensorId =
    row.IdSensor ??
    row.SensorID ??
    row.idSensor ??
    row.id_sensor ??
    row.KodeSensor ??
    row.SeriSensor

  if (sensorId !== null && sensorId !== undefined && String(sensorId).trim() !== '') {
    return `${sensorType}:sensor:${String(sensorId).trim()}`
  }

  // Fallback jika API belum mengirim IdSensor:
  // gunakan lokasi + koordinat agar record pengukuran sensor yang sama
  // tidak membuat marker berulang.
  const coordinates = getCoordinates(row)
  if (!coordinates) return null

  const lokasi =
    row.LokasiId ??
    row.TitikIpal ??
    row.KodePersil ??
    row.KodeBlok ??
    ''

  return `${sensorType}:lokasi:${String(lokasi)}:${coordinates.lat.toFixed(8)}:${coordinates.lng.toFixed(8)}`
}

function getUniqueSensorRows(rows: any[], sensorType: 'curahHujan' | 'tmak') {
  const uniqueSensors = new Map<string, any>()

  for (const row of rows) {
    // Sensor tanpa koordinat tetap tidak ditampilkan.
    if (!getCoordinates(row)) continue

    const key = getSensorKey(row, sensorType)
    if (!key) continue

    // Ambil satu record representatif untuk setiap sensor dari hasil filter.
    // Marker tidak lagi mengikuti jumlah record pengukuran.
    if (!uniqueSensors.has(key)) {
      uniqueSensors.set(key, row)
    }
  }

  return Array.from(uniqueSensors.values())
}

export default function PetaPage() {
  const mapRef    = useRef<HTMLDivElement>(null)
  const leafletRef = useRef<LeafletMap | null>(null)
  const markersRef = useRef<L.Marker[]>([])
  const userLocationRef = useRef<L.Marker | null>(null)

  const [range, setRange] = useState<DateRange>({
    from: daysAgoStr(7),
    to: todayStr(),
  })
  const { data, loading, fetchAll } = useSensorData()

  // Init map once
  useEffect(() => {
    if (!mapRef.current || leafletRef.current) return
    // const map = L.map(mapRef.current).setView([-2.5, 115.0], 10)
      const map = L.map(mapRef.current).setView([-2.5, 115.0], 10)

    L.tileLayer(
        'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
        {
          maxZoom: 18,
          attribution:
            '© <a href="https://openstreetmap.org" target="_blank">OpenStreetMap</a>',
        },
      ).addTo(map)

      leafletRef.current = map

        if ('geolocation' in navigator) {
          navigator.geolocation.getCurrentPosition(
            position => {
              const lat = position.coords.latitude
              const lng = position.coords.longitude

              map.setView([lat, lng], 13)
            },
            error => {
              console.warn('Lokasi pengguna tidak tersedia:', error)

              // fallback jika lokasi ditolak/tidak tersedia
              map.setView([-2.5, 115.0], 10)
            },
            {
              enableHighAccuracy: true,
              timeout: 10000,
              maximumAge: 60000,
            },
          )
        }

    // L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    //   maxZoom: 18,
    //   attribution: '© <a href="https://openstreetmap.org" target="_blank">OpenStreetMap</a>',
    // }).addTo(map)
    // leafletRef.current = map

    // Initial data load
    fetchAll(range)

    return () => {
      map.remove()
      leafletRef.current = null
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Update markers when data changes
  // useEffect(() => {
  //   const map = leafletRef.current
  //   if (!map) return

  //   // Clear old markers
  //   markersRef.current.forEach(m => m.remove())
  //   markersRef.current = []

  //   const bounds: [number, number][] = []

  //   // Curah hujan markers
  //   const chIcon = makeIcon('🌧', '#1565c0')
  //   for (const r of data.curahHujan) {
  //     const lat = parseFloat(r.KoordinatY ?? '')
  //     const lng = parseFloat(r.KoordinatX ?? '')
  //     if (isNaN(lat) || isNaN(lng)) continue
  //     const total = (Number(r.Pagi) || 0) + (Number(r.Sore) || 0)
  //     const m = L.marker([lat, lng], { icon: chIcon })
  //       .bindPopup(`
  //         <b>🌧 Curah Hujan</b><br/>
  //         Persil: ${r.KodePersil || '-'}<br/>
  //         Tanggal: ${formatDate(r.Tanggal)}<br/>
  //         Pagi: ${r.Pagi ?? '-'} mm &nbsp;|&nbsp; Sore: ${r.Sore ?? '-'} mm<br/>
  //         Total: <b>${total.toFixed(1)} mm</b>
  //       `)
  //       .addTo(map)
  //     markersRef.current.push(m)
  //     bounds.push([lat, lng])
  //   }

  //   // TMAK markers
  //   const tmakIcon = makeIcon('💧', '#6a1b9a')
  //   for (const r of data.tmak) {
  //     const lat = parseFloat(r.KoordinatY ?? '')
  //     const lng = parseFloat(r.KoordinatX ?? '')
  //     if (isNaN(lat) || isNaN(lng)) continue
  //     const m = L.marker([lat, lng], { icon: tmakIcon })
  //       .bindPopup(`
  //         <b>💧 TMAK</b><br/>
  //         Blok: ${r.KodeBlok || '-'}<br/>
  //         Tanggal: ${formatDate(r.Tanggal)}<br/>
  //         Tinggi Air: <b>${r.Nilai ?? '-'} cm</b><br/>
  //         Ket: ${r.ket || '-'}
  //       `)
  //       .addTo(map)
  //     markersRef.current.push(m)
  //     bounds.push([lat, lng])
  //   }

  //   if (bounds.length > 0) {
  //     map.fitBounds(bounds as L.LatLngBoundsExpression, { padding: [40, 40] })
  //   }

  //   setTimeout(() => map.invalidateSize(), 200)
  // }, [data.curahHujan, data.tmak])

  useEffect(() => {
  const map = leafletRef.current

  if (!map) return

  // =========================================
  // HAPUS MARKER LAMA
  // =========================================

  markersRef.current.forEach(marker => {
    marker.remove()
  })

  markersRef.current = []

  const bounds: [number, number][] = []

  // =========================================
  // SENSOR CURAH HUJAN
  // =========================================

  const chIcon = makeIcon('🌧', '#1565c0')
  const curahHujanSensors = getUniqueSensorRows(data.curahHujan, 'curahHujan')

  for (const r of curahHujanSensors) {
    const coordinates = getCoordinates(r)

    if (!coordinates) {
      console.warn(
        'Sensor Curah Hujan tanpa koordinat:',
        r
      )

      continue
    }

    const { lat, lng } = coordinates

    const total =
      (Number(r.Pagi) || 0) +
      (Number(r.Sore) || 0)

    const marker = L.marker(
      [lat, lng],
      {
        icon: chIcon,
      }
    )
      // .bindPopup(`
      //   <div style="min-width:200px">

      //     <b style="font-size:14px">
      //       🌧 Sensor Curah Hujan
      //     </b>

      //     <hr/>

      //     <b>Lokasi:</b>
      //     ${r.TitikIpal ?? r.KodePersil ?? '-'}
      //     <br/>

      //     <b>Tanggal:</b>
      //     ${formatDate(r.Tanggal)}
      //     <br/>

      //     <b>Pagi:</b>
      //     ${r.Pagi ?? '-'} mm
      //     <br/>

      //     <b>Sore:</b>
      //     ${r.Sore ?? '-'} mm
      //     <br/>

      //     <b>Total:</b>
      //     ${total.toFixed(1)} mm

      //     <hr/>

      //     <small>
      //       Latitude: ${lat}<br/>
      //       Longitude: ${lng}
      //     </small>

      //   </div>
      // `)
      // .addTo(map)
     .bindPopup(`
        <div style="min-width:200px">

          <b style="font-size:14px">
            🌧 Sensor Curah Hujan
          </b>

          <hr/>

          <b>Lokasi:</b>
          ${r.TitikIpal ?? r.KodePersil ?? '-'}
          <br/>

          <b>Tanggal:</b>
          ${formatDate(r.Tanggal)}
          <br/>

          <hr/>

          <small>
            Latitude: ${lat}<br/>
            Longitude: ${lng}
          </small>

        </div>
      `)
      .addTo(map)

    markersRef.current.push(marker)

    bounds.push([
      lat,
      lng,
    ])
  }

  // =========================================
  // SENSOR TMAK
  // =========================================

  const tmakIcon = makeIcon('💧', '#6a1b9a')
  const tmakSensors = getUniqueSensorRows(data.tmak, 'tmak')

    for (const r of tmakSensors) {
      const coordinates = getCoordinates(r)

      if (!coordinates) {
        console.warn(
          'Sensor TMAK tanpa koordinat:',
          r
        )

        continue
      }

      const { lat, lng } = coordinates

      const marker = L.marker(
        [lat, lng],
        {
          icon: tmakIcon,
        }
      )
        .bindPopup(`
          <div style="min-width:200px">

            <b style="font-size:14px">
              💧 Sensor TMAK
            </b>

            <hr/>

            <b>Lokasi:</b>
            ${r.TitikIpal ?? r.KodeBlok ?? '-'}
            <br/>

            <b>Tanggal:</b>
            ${formatDate(r.Tanggal)}
            <br/>

            <b>Tinggi Air:</b>
            ${r.Nilai ?? '-'} cm
            <br/>

            <b>Keterangan:</b>
            ${r.ket ?? '-'}

            <hr/>

            <small>
              Latitude: ${lat}<br/>
              Longitude: ${lng}
            </small>

          </div>
        `)
        .addTo(map)

      markersRef.current.push(marker)

      bounds.push([
        lat,
        lng,
      ])
    }

  // =========================================
  // POSISI PETA
  // =========================================

  if (bounds.length > 0) {
    map.fitBounds(
      bounds as L.LatLngBoundsExpression,
      {
        padding: [40, 40],

        // Jangan zoom terlalu dekat kalau
        // sensor hanya 1 atau lokasinya berdekatan
        maxZoom: 15,
      }
    )
  }

  // Leaflet baca ulang ukuran container
  setTimeout(() => {
    map.invalidateSize()
  }, 200)

}, [data.curahHujan, data.tmak])

  // return (
  //   <div>
  return (
  <div className={styles.page}>
      <FilterBar
        range={range}
        onChange={setRange}
        onApply={() => fetchAll(range)}
        loading={loading}
      />

      {/* Legend */}
      <div className={styles.legend}>
        <div className={styles.legendItem}>
          <span className={styles.dot} style={{ background: '#1565c0' }} />
          {/*Sensor Curah Hujan ({data.curahHujan.filter(r => r.KoordinatX && r.KoordinatY).length} titik)*/}
          Sensor Curah Hujan ( {getUniqueSensorRows(data.curahHujan, 'curahHujan').length} titik)
        </div>
        <div className={styles.legendItem}>
          {/*<span className={styles.dot} style={{ background: '#6a1b9a' }} />*/}
          {/*Sensor TMAK ({data.tmak.filter(r => r.KoordinatX && r.KoordinatY).length} titik)*/}
          {/*Sensor TMAK ( {getUniqueSensorRows(data.tmak, 'tmak').length} titik)*/}
        </div>
        <span className={styles.hint}>Klik marker untuk detail sensor</span>
      </div>

      {/* Map */}
      <div className={styles.mapWrap}>
        {loading && (
          <div className={styles.mapOverlay}>
            <span className={styles.spinner} />
          </div>
        )}
        <div ref={mapRef} className={styles.map} />
      </div>

      <p className={styles.note}>
        ℹ️ Sensor tanpa koordinat (<code>KoordinatX</code> / <code>KoordinatY</code>) tidak ditampilkan di peta.
      </p>
    </div>
  )
}
