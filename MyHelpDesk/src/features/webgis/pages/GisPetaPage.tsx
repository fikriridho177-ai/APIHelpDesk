import { NoAccess } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import MapWorkspace from '../components/MapWorkspace'

export const GIS_PETA_MENU = 'GIS_PETA'

// Menu Peta GIS (Monitoring): Map Workspace satu halaman penuh, misalnya untuk tim RNA.
// Tim GIS membukanya dari tab "Map Workspace" di panel divisi GIS.
export default function GisPetaPage() {
  const { permission, loaded } = useMenuPermission(GIS_PETA_MENU)
  if (!permission?.canRead) return <NoAccess loaded={loaded} />
  return <MapWorkspace />
}
