import { useEffect, useMemo, useState } from 'react'

import { getPKBPayrollDepartments } from '../../groups/api/groupApi'
import { buildServiceTree, getDivisions, getLayananServices } from '../api/layananApi'
import type { LynService, LynServiceNode } from '../types/layanan'

// Daftar divisi & sub-layanan yang boleh dibuka user.
export function useLayananServices() {
  const [services, setServices] = useState<LynService[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getLayananServices()
      .then(setServices)
      .catch(error => console.error('Gagal mengambil daftar layanan:', error))
      .finally(() => setLoading(false))
  }, [])

  const tree = useMemo(() => buildServiceTree(services), [services])
  const divisions = useMemo(() => getDivisions(tree), [tree])

  // Lookup node berdasarkan route (route diatur di Manajemen Menu).
  const byRoute = useMemo(() => {
    const map = new Map<string, LynServiceNode>()
    const walk = (nodes: LynServiceNode[]) =>
      nodes.forEach(node => {
        if (node.route) map.set(normalizeRoute(node.route), node)
        walk(node.children)
      })
    walk(tree)
    return map
  }, [tree])

  const byId = useMemo(() => {
    const map = new Map<number, LynServiceNode>()
    const walk = (nodes: LynServiceNode[]) =>
      nodes.forEach(node => {
        map.set(node.id, node)
        walk(node.children)
      })
    walk(tree)
    return map
  }, [tree])

  return { services, tree, divisions, byRoute, byId, loading }
}

// Nama departemen PKBPayroll (DeptID -> nama), hanya untuk tampilan.
export function useDepartmentNames() {
  const [names, setNames] = useState<Map<string, string>>(new Map())

  useEffect(() => {
    getPKBPayrollDepartments()
      .then(list => setNames(new Map(list.map(d => [d.id, d.shortName ? `${d.name} (${d.shortName})` : d.name]))))
      .catch(() => setNames(new Map()))
  }, [])

  return names
}

export function getCurrentUserId() {
  try {
    return Number(JSON.parse(localStorage.getItem('auth') ?? '{}').id) || 0
  } catch {
    return 0
  }
}

export function normalizeRoute(route: string) {
  const decoded = decodeURIComponent(route.trim()).toLowerCase()
  return decoded.length > 1 ? decoded.replace(/\/+$/, '') : decoded
}
