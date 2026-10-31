import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { getGroup, getGroupMenuAccess, saveGroupMenuAccess } from '../api/groupApi'
import type { Group, GroupMenuAccess } from '../types/group'

type Row = GroupMenuAccess & { level: number; hasChildren: boolean }

// Susun menu jadi pohon datar; anak dari parent yang ditutup tidak ikut ditampilkan.
function flatten(items: GroupMenuAccess[], collapsed: Set<number>): Row[] {
  const byParent = new Map<number | null, GroupMenuAccess[]>()
  items.forEach(x => {
    const key = x.parentId ?? null
    byParent.set(key, [...(byParent.get(key) ?? []), x])
  })
  byParent.forEach(list => list.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name)))

  const rows: Row[] = []
  const walk = (parentId: number | null, level: number) => {
    for (const x of byParent.get(parentId) ?? []) {
      const hasChildren = byParent.has(x.menuId)
      rows.push({ ...x, level, hasChildren })
      if (hasChildren && !collapsed.has(x.menuId)) walk(x.menuId, level + 1)
    }
  }
  walk(null, 0)
  return rows
}

function parentIds(items: GroupMenuAccess[]) {
  return new Set(items.filter(x => x.parentId != null).map(x => x.parentId!))
}

// Tinggi halaman = sisa layar (dikurangi footer aplikasi), agar yang di-scroll hanya tabel (bukan halaman).
function useFitToViewport<T extends HTMLElement>(bottomGap = 28) {
  const ref = useRef<T>(null)
  const [height, setHeight] = useState<number>()

  useLayoutEffect(() => {
    const fit = () => {
      if (!ref.current) return
      const top = ref.current.getBoundingClientRect().top + window.scrollY
      const footer = document.querySelector<HTMLElement>('.app-footer')?.offsetHeight ?? 0
      setHeight(Math.max(360, window.innerHeight - top - bottomGap - footer))
    }
    fit()
    window.addEventListener('resize', fit)
    return () => window.removeEventListener('resize', fit)
  }, [bottomGap])

  return { ref, height }
}

const permissionKeys = ['canView','canRead','canCreate','canUpdate','canDelete','canApprove','canExport'] as const

export default function GroupMenuAccessPage() {
  const navigate = useNavigate()
  const { groupId } = useParams()
  const id = Number(groupId)
  const [group, setGroup] = useState<Group | null>(null)
  const [items, setItems] = useState<GroupMenuAccess[]>([])
  const [saving, setSaving] = useState(false)
  // Default: semua parent tertutup, hanya menu level atas yang tampil.
  const [collapsed, setCollapsed] = useState<Set<number>>(new Set())
  const { ref: pageRef, height } = useFitToViewport<HTMLElement>()

  useEffect(() => {
    Promise.all([getGroup(id), getGroupMenuAccess(id)]).then(([g, m]) => {
      setGroup(g)
      setItems(m)
      setCollapsed(parentIds(m))
    })
  }, [id])

  const rows = useMemo(() => flatten(items, collapsed), [items, collapsed])

  function toggle(menuId: number) {
    setCollapsed(current => {
      const next = new Set(current)
      if (next.has(menuId)) next.delete(menuId)
      else next.add(menuId)
      return next
    })
  }

  function patch(menuId: number, key: typeof permissionKeys[number] | 'all', checked: boolean) {
    setItems(current => current.map(x => {
      if (x.menuId !== menuId) return x
      if (key === 'all') return { ...x, canView: checked, canRead: checked, canCreate: checked, canUpdate: checked, canDelete: checked, canApprove: checked, canExport: checked }
      return { ...x, [key]: checked }
    }))
  }

  async function save() {
    try {
      setSaving(true)
      await saveGroupMenuAccess(id, items)
      alert('Akses menu berhasil disimpan.')
      setItems(await getGroupMenuAccess(id))
    } finally { setSaving(false) }
  }

  return (
    <section ref={pageRef} className="group-permission-page fit-viewport" style={{ height }}>
      <div className="page-header">
        <div>
          <h1>Akses Menu</h1>
          <p className="text-muted">Hak Akses: <strong>{group?.name ?? '...'}</strong></p>
        </div>
        <div className="permission-header-actions">
          <button type="button" className="secondary" onClick={() => setCollapsed(new Set())}>Buka Semua</button>
          <button type="button" className="secondary" onClick={() => setCollapsed(parentIds(items))}>Tutup Semua</button>
          <button type="button" className="secondary" onClick={() => navigate('/utility/groups')}>← Kembali</button>
        </div>
      </div>

      {group?.isSuperAdmin && <div className="permission-info">Group ini Super Administrator. Secara efektif seluruh menu dan aksi diperbolehkan.</div>}

      <div className="card permission-card">
        <div className="table-container">
          <table className="permission-table">
            <thead><tr><th>Menu</th><th>Semua</th><th>View</th><th>Read</th><th>Create</th><th>Update</th><th>Delete</th><th>Approve</th><th>Export</th></tr></thead>
            <tbody>
              {rows.map(row => {
                const all = group?.isSuperAdmin ? true : permissionKeys.every(k => row[k])
                const open = row.hasChildren && !collapsed.has(row.menuId)
                return (
                  <tr key={row.menuId} className={row.hasChildren ? 'permission-parent-row' : undefined}>
                    <td>
                      <div className="permission-menu-name" style={{ paddingLeft: `${row.level * 24}px` }}>
                        {row.hasChildren ? (
                          <button
                            type="button"
                            className={`permission-toggle${open ? ' open' : ''}`}
                            title={open ? 'Tutup' : 'Buka'}
                            aria-expanded={open}
                            onClick={() => toggle(row.menuId)}
                          >
                            ▸
                          </button>
                        ) : (
                          <span className="permission-toggle-spacer" />
                        )}
                        <span>{row.menuType === 'GROUP' ? '▦' : row.menuType === 'WIDGET' ? '◫' : '▤'}</span>
                        <div><strong>{row.name}</strong><small>{row.code}</small></div>
                      </div>
                    </td>
                    <td><input type="checkbox" checked={all} disabled={group?.isSuperAdmin} onChange={e => patch(row.menuId, 'all', e.target.checked)} /></td>
                    {permissionKeys.map(key => <td key={key}><input type="checkbox" checked={group?.isSuperAdmin ? true : row[key]} disabled={group?.isSuperAdmin} onChange={e => patch(row.menuId, key, e.target.checked)} /></td>)}
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        <div className="form-actions"><button type="button" className="secondary" onClick={() => navigate('/utility/groups')}>Batal</button><button type="button" disabled={saving || group?.isSuperAdmin} onClick={save}>{saving ? 'Menyimpan...' : 'Simpan Hak Akses'}</button></div>
      </div>
    </section>
  )
}
