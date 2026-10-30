import type { NavigationMenuItem } from '../../menus/types/menu'

export type IconName =
  | 'desktop'
  | 'users'
  | 'shield'
  | 'menu'
  | 'key'
  | 'building'
  | 'file'
  | 'chart'
  | 'settings'
  | 'grid'
  | 'ticket'
  | 'clock'
  | 'check'
  | 'info'
  | 'arrow'
  | 'chat'
  | 'leaf'
  | 'map'
  | 'water'
  | 'tractor'
  | 'calendar'
  | 'pin'
  | 'plus'
  | 'search'
  | 'download'
  | 'inbox'

// Path SVG (gaya garis, 24x24) untuk setiap ikon.
const ICON_PATHS: Record<IconName, string> = {
  desktop: 'M3 4h18v12H3z M8 20h8 M12 16v4',
  users:
    'M16 20v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M9 10a4 4 0 1 0 0-8 4 4 0 0 0 0 8 M22 20v-2a4 4 0 0 0-3-3.87 M16 2.13a4 4 0 0 1 0 7.75',
  shield: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z M9 12l2 2 4-4',
  menu: 'M4 6h16 M4 12h16 M4 18h10',
  key: 'M15 7a4 4 0 1 1-3.87 5H3v3h3v3h3v-3h2.13A4 4 0 0 1 15 7z',
  building: 'M4 21V5l8-3 8 3v16 M9 9h1 M14 9h1 M9 13h1 M14 13h1 M10 21v-4h4v4',
  file: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M8 13h8 M8 17h6',
  chart: 'M3 3v18h18 M7 15l4-4 3 3 5-6',
  settings:
    'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6 M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z',
  grid: 'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
  ticket: 'M3 7a2 2 0 0 0 2-2h14a2 2 0 0 0 2 2v3a2 2 0 0 0 0 4v3a2 2 0 0 0-2 2H5a2 2 0 0 0-2-2v-3a2 2 0 0 0 0-4z M13 5v14',
  clock: 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20 M12 6v6l4 2',
  check: 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20 M8 12l3 3 5-6',
  info: 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20 M12 16v-4 M12 8h.01',
  arrow: 'M5 12h14 M13 6l6 6-6 6',
  chat: 'M21 12a8 8 0 0 1-11.6 7.1L4 21l1.9-5.4A8 8 0 1 1 21 12z M8 11h.01 M12 11h.01 M16 11h.01',
  leaf: 'M11 20A7 7 0 0 1 9.8 6.1C15.5 5 17 4.5 19 2c1 2 2 4.2 2 8 0 5.5-4.8 10-10 10z M2 21c0-3 1.9-5.4 5-6',
  map: 'M9 4L3 6v14l6-2 6 2 6-2V4l-6 2-6-2z M9 4v14 M15 6v14',
  water: 'M12 2.7l5.7 5.7a8 8 0 1 1-11.4 0z',
  tractor: 'M3 4h9l1 7 M4 11V4 M8 10V4 M18 5c-.6 0-1 .4-1 1v5.6 M10 11h7 M7 21a4 4 0 1 0 0-8 4 4 0 0 0 0 8 M18 21a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5 M11 17h4.5',
  calendar: 'M3 5h18v16H3z M16 3v4 M8 3v4 M3 10h18',
  pin: 'M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0z M12 13a3 3 0 1 0 0-6 3 3 0 0 0 0 6',
  plus: 'M12 5v14 M5 12h14',
  search: 'M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16 M21 21l-4.3-4.3',
  download: 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3',
  inbox: 'M22 12h-6l-2 3h-4l-2-3H2 M5.5 5.1L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.5-6.9A2 2 0 0 0 16.8 4H7.2a2 2 0 0 0-1.7 1.1z',
}

// Kata kunci pada kode / nama / route menu -> ikon yang dipakai.
const KEYWORD_ICONS: [RegExp, IconName][] = [
  [/chat/i, 'chat'],
  [/pengajuan/i, 'inbox'],
  [/drone|peta|spasial|gis|survey|area/i, 'map'],
  [/water|air|tmat|tmak|hujan|gambut|shipon|wad/i, 'water'],
  [/alat|mekanisasi|nat\b|tractor/i, 'tractor'],
  [/sensus|hpt|rna|leaf|tanam|hama|penyakit/i, 'leaf'],
  [/check|inspeksi|audit|qc|patok/i, 'check'],
  [/user|pengguna/i, 'users'],
  [/group|role|hak.?akses/i, 'shield'],
  [/menu/i, 'menu'],
  [/security|password|keamanan/i, 'key'],
  [/company|perusahaan|dept|departemen/i, 'building'],
  [/laporan|report/i, 'file'],
  [/monitor|dashboard/i, 'chart'],
  [/setting|utility|pengaturan/i, 'settings'],
  [/desktop|aplikasi/i, 'desktop'],
]

// Daftar nama ikon yang bisa diisi di kolom Icon (Manajemen Menu).
export const ICON_NAMES = Object.keys(ICON_PATHS) as IconName[]

export function isIconName(value?: string | null): value is IconName {
  return !!value && value in ICON_PATHS
}

export function pickIconName(menu: Pick<NavigationMenuItem, 'code' | 'name' | 'route' | 'icon'>): IconName {
  // Kolom Icon di database berisi nama ikon yang valid -> langsung dipakai.
  const icon = menu.icon?.trim().toLowerCase()
  if (isIconName(icon)) return icon

  // Selain itu, tebak dari kata kunci kode / nama / route.
  const text = `${menu.icon ?? ''} ${menu.code} ${menu.name} ${menu.route ?? ''}`
  return KEYWORD_ICONS.find(([pattern]) => pattern.test(text))?.[1] ?? 'grid'
}

export function DashboardIcon({ name }: { name: IconName }) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="1em"
      height="1em"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={ICON_PATHS[name]} />
    </svg>
  )
}
