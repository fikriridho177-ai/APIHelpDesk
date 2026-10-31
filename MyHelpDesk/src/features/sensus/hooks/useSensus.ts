import { useEffect, useState } from 'react'

import { getMyMenuPermission, type CurrentMenuPermission } from '../../security/api/securityApi'
import { getJenis, getSensusWilayah } from '../api/sensusApi'
import type { SnsJenis } from '../types/sensus'

// Permission user pada menu sensus (Hak Akses > Akses Menu). null = masih dimuat.
export function useMenuPermission(menuCode: string) {
  const [permission, setPermission] = useState<CurrentMenuPermission | null>(null)
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    getMyMenuPermission(menuCode)
      .then(setPermission)
      .catch(() => setPermission(null))
      .finally(() => setLoaded(true))
  }, [menuCode])

  return { permission, loaded }
}

// Pilihan filter: wilayah (dari data sensus) & jenis sensus yang aktif.
export function useSensusLookups() {
  const [wilayah, setWilayah] = useState<string[]>([])
  const [jenis, setJenis] = useState<SnsJenis[]>([])

  useEffect(() => {
    getSensusWilayah().then(setWilayah).catch(() => setWilayah([]))
    getJenis().then(list => setJenis(list.filter(x => x.active))).catch(() => setJenis([]))
  }, [])

  return { wilayah, jenis }
}
