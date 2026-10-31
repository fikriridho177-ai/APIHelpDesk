import { useEffect, useMemo, useState } from 'react'
import type { MenuItem } from '../types/menu'

type Props = {
  menus: MenuItem[]
  selectedId: number | null
  search: string
  onSelect: (menu: MenuItem) => void
  onDisable: (menu: MenuItem) => void
}

const STORAGE_KEY = 'menuTree.expanded'

function buildTree(menus: MenuItem[]): MenuItem[] {
  const map = new Map<number, MenuItem>()
  menus.forEach(menu => map.set(menu.id, { ...menu, children: [] }))
  const roots: MenuItem[] = []
  map.forEach(menu => {
    if (menu.parentId && map.has(menu.parentId)) map.get(menu.parentId)!.children!.push(menu)
    else roots.push(menu)
  })
  const sortItems = (items: MenuItem[]) => {
    items.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
    items.forEach(x => x.children?.length && sortItems(x.children))
  }
  sortItems(roots)
  return roots
}

// Menu yang dibuka (expand) diingat per browser agar tidak perlu dibuka ulang setiap kali halaman dimuat.
function loadExpanded(): Set<number> {
  try {
    return new Set(JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]') as number[])
  } catch {
    return new Set()
  }
}

function saveExpanded(ids: Set<number>) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(Array.from(ids)))
  } catch {
    // localStorage tidak tersedia: abaikan, tree tetap berfungsi.
  }
}

export default function MenuTree({ menus, selectedId, search, onSelect, onDisable }: Props) {
  const [expanded, setExpanded] = useState<Set<number>>(loadExpanded)

  const keyword = search.trim().toLowerCase()

  // Saat mencari: tampilkan menu yang cocok beserta parent-nya, agar posisi menu tetap terlihat.
  const filteredMenus = useMemo(() => {
    if (!keyword) return menus

    const byId = new Map(menus.map(menu => [menu.id, menu]))
    const visible = new Set<number>()

    menus
      .filter(menu => `${menu.name} ${menu.code} ${menu.route ?? ''}`.toLowerCase().includes(keyword))
      .forEach(menu => {
        let current: MenuItem | undefined = menu
        while (current && !visible.has(current.id)) {
          visible.add(current.id)
          current = current.parentId ? byId.get(current.parentId) : undefined
        }
      })

    return menus.filter(menu => visible.has(menu.id))
  }, [menus, keyword])

  const tree = useMemo(() => buildTree(filteredMenus), [filteredMenus])

  const parentIds = useMemo(
    () => menus.filter(menu => menus.some(child => child.parentId === menu.id)).map(menu => menu.id),
    [menus],
  )

  // Menu yang sedang diedit: buka semua parent-nya supaya terlihat di tree.
  useEffect(() => {
    if (selectedId === null) return
    const byId = new Map(menus.map(menu => [menu.id, menu]))
    const ancestors: number[] = []
    let current = byId.get(selectedId)
    while (current?.parentId && !ancestors.includes(current.parentId)) {
      ancestors.push(current.parentId)
      current = byId.get(current.parentId)
    }
    if (ancestors.some(id => !expanded.has(id))) update(new Set([...expanded, ...ancestors]))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId, menus])

  function update(next: Set<number>) {
    setExpanded(next)
    saveExpanded(next)
  }

  function toggle(id: number) {
    const next = new Set(expanded)
    if (next.has(id)) next.delete(id)
    else next.add(id)
    update(next)
  }

  function countDescendants(menu: MenuItem): number {
    return (menu.children ?? []).reduce((total, child) => total + 1 + countDescendants(child), 0)
  }

  function renderMenu(menu: MenuItem, level = 0) {
    const hasChildren = !!menu.children?.length
    // Saat mencari, semua hasil selalu terbuka.
    const isOpen = hasChildren && (!!keyword || expanded.has(menu.id))

    return (
      <div key={menu.id} className="menu-tree-node">
        <div className={selectedId === menu.id ? 'menu-tree-card selected' : 'menu-tree-card'} style={{ marginLeft: `${level * 22}px` }}>
          {hasChildren ? (
            <button
              type="button"
              className={isOpen ? 'menu-tree-toggle open' : 'menu-tree-toggle'}
              title={isOpen ? 'Tutup' : 'Buka'}
              aria-expanded={isOpen}
              onClick={() => toggle(menu.id)}
              disabled={!!keyword}
            >
              ▸
            </button>
          ) : (
            <span className="menu-tree-toggle-spacer" />
          )}
          <button type="button" className="menu-tree-content" onClick={() => onSelect(menu)}>
            <div className="menu-tree-title-row">
              <span className="menu-tree-icon">{menu.menuType === 'GROUP' ? '▦' : menu.menuType === 'WIDGET' ? '◫' : '▤'}</span>
              <div>
                <strong>{menu.name}</strong>
                {hasChildren && !isOpen && <span className="menu-tree-count">{countDescendants(menu)} sub-menu</span>}
                <div className="menu-tree-code">{menu.code}</div>
              </div>
            </div>
            {menu.route && <div className="menu-tree-route">{menu.route}</div>}
          </button>
          <div className="menu-tree-actions">
            <button type="button" className="tree-edit-button" title="Edit menu" onClick={() => onSelect(menu)}>✎</button>
            <button type="button" className="tree-delete-button" title="Nonaktifkan menu" onClick={() => onDisable(menu)}>🗑</button>
          </div>
        </div>
        {isOpen && menu.children!.map(child => renderMenu(child, level + 1))}
      </div>
    )
  }

  return (
    <>
      <div className="menu-tree-tools">
        <button type="button" className="menu-tree-tool" onClick={() => update(new Set(parentIds))} disabled={!!keyword}>
          Buka semua
        </button>
        <button type="button" className="menu-tree-tool" onClick={() => update(new Set())} disabled={!!keyword}>
          Tutup semua
        </button>
      </div>
      <div className="menu-tree">{tree.length === 0 ? <div className="menu-empty">Menu tidak ditemukan.</div> : tree.map(menu => renderMenu(menu))}</div>
    </>
  )
}
