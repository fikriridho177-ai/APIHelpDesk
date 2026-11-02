import { useNavigate } from 'react-router-dom'

import { DashboardIcon, pickIconName } from '../../dashboard/components/DashboardIcon'
import { IOT_NAVIGATION_CODES } from '../../iot/iotNavigation'
import type { NavigationMenuItem } from '../../menus/types/menu'
import { WidgetFrame } from '../components/WidgetFrame'
import type { WidgetProps } from '../types/portal'

// Menu level atas yang tidak dijadikan pintasan (sudah ada di portal / punya tombol sendiri).
const EXCLUDED_CODES = new Set([
  'LAYANAN',
  'PORTAL',
  'LIVE_CHAT',
  'SNS_MOBILE',
  ...IOT_NAVIGATION_CODES,
])

const bySortOrder = (a: NavigationMenuItem, b: NavigationMenuItem) => a.sortOrder - b.sortOrder

// Route menu itu sendiri, atau route anak pertama (urut SortOrder, bertingkat).
function findTarget(menu: NavigationMenuItem, menus: NavigationMenuItem[]): string | null {
  if (menu.route) return menu.route
  for (const child of menus.filter(m => m.parentId === menu.id && m.menuType !== 'WIDGET').sort(bySortOrder)) {
    const target = findTarget(child, menus)
    if (target) return target
  }
  return null
}

// Pintasan ke aplikasi lain yang boleh diakses user (Sensus, ECS, IoT, Utility, ...).
export default function AksesCepatWidget({ menu, menus }: WidgetProps) {
  const navigate = useNavigate()

  const shortcuts = menus
    .filter(m => m.parentId == null && m.menuType !== 'WIDGET' && !EXCLUDED_CODES.has(m.code) && m.route !== '/')
    .sort(bySortOrder)
    .map(m => ({ menu: m, target: findTarget(m, menus) }))
    .filter((s): s is { menu: NavigationMenuItem; target: string } => s.target !== null)

  return (
    <WidgetFrame
      title={menu.name}
      icon={pickIconName(menu)}
      empty={shortcuts.length === 0 ? 'Tidak ada aplikasi lain.' : null}
    >
      <div className="portal-shortcuts">
        {shortcuts.map(({ menu: item, target }) => (
          <button key={item.id} type="button" className="portal-shortcut" onClick={() => navigate(target)}>
            <span className="portal-shortcut-icon">
              <DashboardIcon name={pickIconName(item)} />
            </span>
            <span>{item.name}</span>
          </button>
        ))}
      </div>
    </WidgetFrame>
  )
}
