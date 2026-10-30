import { useNavigate } from 'react-router-dom'

import { DashboardIcon, pickIconName } from '../../dashboard/components/DashboardIcon'
import { flattenServices } from '../api/layananApi'
import { getDivisionInfo } from '../layananConfig'
import type { LynServiceNode } from '../types/layanan'

// Kartu divisi (RNA, GIS, WAD, QC, NAT) — gaya pastel seperti dashboard.
// routeOverrides: ganti tujuan kartu per kode divisi (contoh: { LYN_WAD: '/ecs' }).
export function DivisionCards({
  divisions,
  routeOverrides,
}: {
  divisions: LynServiceNode[]
  routeOverrides?: Record<string, string>
}) {
  const navigate = useNavigate()

  return (
    <div className="lyn-division-grid">
      {divisions.map((division, index) => {
        const info = getDivisionInfo(division, index)
        // Jenis permintaan (master) + sub-layanan yang masih berupa menu (mis. halaman sensus).
        const count = division.jenisCount + flattenServices(division).length
        const route = routeOverrides?.[division.code] ?? division.route

        return (
          <button
            key={division.id}
            type="button"
            className={`lyn-division-card tone-${info.tone}`}
            onClick={() => route && navigate(route)}
          >
            <span className="lyn-division-icon">
              <DashboardIcon name={info.icon} />
            </span>
            <span className="lyn-division-text">
              <strong>{division.name}</strong>
              {info.fullName && <em>{info.fullName}</em>}
              <small>{info.description}</small>
            </span>
            <span className="lyn-division-footer">
              <span>{count} layanan</span>
              <span className="lyn-division-arrow">
                <DashboardIcon name="arrow" />
              </span>
            </span>
          </button>
        )
      })}
    </div>
  )
}

// Kartu sub-layanan di dalam divisi / grup (contoh: Sensus HPT berisi 5 sensus).
export function ServiceCards({ nodes, tone }: { nodes: LynServiceNode[]; tone: string }) {
  const navigate = useNavigate()

  return (
    <div className="lyn-service-grid">
      {nodes.map(node => {
        const isGroup = node.menuType === 'GROUP'
        const childCount = isGroup ? flattenServices(node).length : 0

        return (
          <button
            key={node.id}
            type="button"
            className={`lyn-service-card tone-${tone}`}
            onClick={() => node.route && navigate(node.route)}
          >
            <span className="lyn-service-icon">
              <DashboardIcon name={pickIconName(node)} />
            </span>
            <span className="lyn-service-text">
              <strong>{node.name}</strong>
              <small>
                {isGroup
                  ? `${childCount} sub-layanan`
                  : node.permissions.canCreate
                    ? 'Ajukan & pantau status'
                    : 'Pantau pengajuan'}
              </small>
            </span>
            <DashboardIcon name="arrow" />
          </button>
        )
      })}
    </div>
  )
}
