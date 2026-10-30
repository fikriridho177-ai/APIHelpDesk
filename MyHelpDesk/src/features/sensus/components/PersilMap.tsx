import { useMemo, useRef, useState, type MouseEvent } from 'react'

import type { Persil } from '../types/sensus'

// Peta sebaran sederhana (tanpa library peta): Longitude -> X, Latitude -> Y, skala sama agar bentuk lahan tidak gepeng.
const W = 640
const H = 380
const PAD = 36
const MAX_POINTS = 6000 // titik lebih banyak diambil sampelnya agar browser tetap ringan
const HIT_RADIUS = 14 // px di layar; area hover lebih besar dari titik

type Point = { x: number; y: number; persil: Persil }

export default function PersilMap({ rows }: { rows: Persil[] }) {
  const svgRef = useRef<SVGSVGElement | null>(null)
  const [hover, setHover] = useState<Point | null>(null)

  const { points, ticksX, ticksY, sampled, total } = useMemo(() => {
    const withCoord = rows.filter(r => r.latitude != null && r.longitude != null)
    const step = Math.max(1, Math.ceil(withCoord.length / MAX_POINTS))
    const used = step === 1 ? withCoord : withCoord.filter((_, i) => i % step === 0)

    if (used.length === 0) return { points: [] as Point[], ticksX: [], ticksY: [], sampled: false, total: 0 }

    const lons = used.map(r => r.longitude!)
    const lats = used.map(r => r.latitude!)
    let minX = Math.min(...lons), maxX = Math.max(...lons)
    let minY = Math.min(...lats), maxY = Math.max(...lats)

    // Satu titik saja / semua titik sama: beri rentang kecil.
    if (maxX - minX < 1e-6) { minX -= 0.005; maxX += 0.005 }
    if (maxY - minY < 1e-6) { minY -= 0.005; maxY += 0.005 }

    const scale = Math.min((W - PAD * 2) / (maxX - minX), (H - PAD * 2) / (maxY - minY))
    const offsetX = (W - (maxX - minX) * scale) / 2
    const offsetY = (H - (maxY - minY) * scale) / 2
    const toX = (lon: number) => offsetX + (lon - minX) * scale
    const toY = (lat: number) => H - offsetY - (lat - minY) * scale

    const ticks = (min: number, max: number) => Array.from({ length: 4 }, (_, i) => min + ((max - min) * (i + 0.5)) / 4)

    return {
      points: used.map(persil => ({ x: toX(persil.longitude!), y: toY(persil.latitude!), persil })),
      ticksX: ticks(minX, maxX).map(v => ({ v, pos: toX(v) })),
      ticksY: ticks(minY, maxY).map(v => ({ v, pos: toY(v) })),
      sampled: step > 1,
      total: withCoord.length,
    }
  }, [rows])

  function handleMove(event: MouseEvent<SVGSVGElement>) {
    const svg = svgRef.current
    if (!svg || points.length === 0) return

    const rect = svg.getBoundingClientRect()
    const ratio = W / rect.width
    const mx = (event.clientX - rect.left) * ratio
    const my = (event.clientY - rect.top) * ratio
    const limit = (HIT_RADIUS * ratio) ** 2

    let best: Point | null = null
    let bestDist = limit
    for (const p of points) {
      const d = (p.x - mx) ** 2 + (p.y - my) ** 2
      if (d < bestDist) {
        bestDist = d
        best = p
      }
    }
    setHover(best)
  }

  function openHovered() {
    if (!hover) return
    window.open(`https://www.google.com/maps?q=${hover.persil.latitude},${hover.persil.longitude}`, '_blank', 'noreferrer')
  }

  if (points.length === 0) {
    return (
      <div className="persil-map-empty">
        <p>Tidak ada persil dengan koordinat untuk filter ini.</p>
      </div>
    )
  }

  return (
    <div className="persil-map">
      <svg
        ref={svgRef}
        viewBox={`0 0 ${W} ${H}`}
        role="img"
        aria-label={`Peta sebaran ${total} persil berkoordinat`}
        onMouseMove={handleMove}
        onMouseLeave={() => setHover(null)}
        onClick={openHovered}
        style={{ cursor: hover ? 'pointer' : 'default' }}
      >
        {/* Grid & label sumbu (redup) */}
        {ticksX.map(t => (
          <g key={`x${t.v}`}>
            <line x1={t.pos} x2={t.pos} y1={PAD / 2} y2={H - PAD / 2} className="persil-grid" />
            <text x={t.pos} y={H - 6} textAnchor="middle" className="persil-axis">
              {t.v.toFixed(3)}°
            </text>
          </g>
        ))}
        {ticksY.map(t => (
          <g key={`y${t.v}`}>
            <line x1={PAD / 2} x2={W - PAD / 2} y1={t.pos} y2={t.pos} className="persil-grid" />
            <text x={4} y={t.pos - 4} className="persil-axis">
              {t.v.toFixed(3)}°
            </text>
          </g>
        ))}

        {points.map((p, i) => (
          <circle key={i} cx={p.x} cy={p.y} r={4} className="persil-dot" />
        ))}

        {hover && <circle cx={hover.x} cy={hover.y} r={7} className="persil-dot-hover" />}
      </svg>

      {hover && (
        <div
          className="persil-tooltip"
          style={{
            left: `${(hover.x / W) * 100}%`,
            top: `${(hover.y / H) * 100}%`,
            transform: `translate(${hover.x > W * 0.65 ? 'calc(-100% - 12px)' : '12px'}, -50%)`,
          }}
        >
          <strong>{hover.persil.kode || '-'}</strong>
          <span>Wilayah {hover.persil.wilayah || '-'}{hover.persil.afdeling ? ` · Afdeling ${hover.persil.afdeling}` : ''}</span>
          <span>Persil ID {hover.persil.persilId || '-'}</span>
          <span className="persil-tooltip-coord">
            {hover.persil.latitude?.toFixed(6)}, {hover.persil.longitude?.toFixed(6)}
          </span>
          <em>Klik untuk buka Google Maps</em>
        </div>
      )}

      <p className="persil-map-note">
        {total.toLocaleString('id-ID')} persil berkoordinat
        {sampled && ` · ditampilkan ${points.length.toLocaleString('id-ID')} titik (sampel) agar tetap ringan`}
      </p>
    </div>
  )
}
