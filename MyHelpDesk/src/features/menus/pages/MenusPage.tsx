import { useEffect, useMemo, useState } from 'react'
import { createMenu, disableMenu, getMenus, updateMenu } from '../api/menuApi'
import MenuForm from '../components/MenuForm'
import MenuTree from '../components/MenuTree'
import type { MenuItem, SaveMenuRequest } from '../types/menu'

const initialForm: SaveMenuRequest = { parentId: null, code: '', name: '', route: '', icon: '', fullName: '', description: '', color: '', menuType: 'MENU', sortOrder: 0, active: true }

export default function MenusPage() {
  const [menus, setMenus] = useState<MenuItem[]>([])
  const [form, setForm] = useState<SaveMenuRequest>(initialForm)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [search, setSearch] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)

  async function load() {
    try { setLoading(true); setMenus(await getMenus()) }
    finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const activeMenus = useMemo(() => menus.filter(menu => menu.active), [menus])

  function handleAdd() { setEditingId(null); setForm({ ...initialForm }); setShowForm(true) }
  function handleEdit(menu: MenuItem) {
    setEditingId(menu.id)
    setForm({ parentId: menu.parentId ?? null, code: menu.code, name: menu.name, route: menu.route ?? '', icon: menu.icon ?? '', fullName: menu.fullName ?? '', description: menu.description ?? '', color: menu.color ?? '', menuType: menu.menuType, sortOrder: menu.sortOrder, active: menu.active })
    setShowForm(true)
  }
  function handleCancel() { setEditingId(null); setForm({ ...initialForm }); setShowForm(false) }

  async function handleSave() {
    try {
      setSaving(true)
      // Route GROUP tetap disimpan (contoh: /layanan/rna, /sensus) agar halaman grup tidak hilang saat diedit.
      const payload = { ...form, route: form.route?.trim() ? form.route : null }
      if (editingId !== null) await updateMenu(editingId, payload)
      else await createMenu(payload)
      handleCancel(); await load()
    } finally { setSaving(false) }
  }

  async function handleDisable(menu: MenuItem) {
    if (!window.confirm(`Nonaktifkan menu "${menu.name}"?`)) return
    await disableMenu(menu.id)
    if (editingId === menu.id) handleCancel()
    await load()
  }

  return (
    <section className="menu-management-page">
      <div className="page-header"><div><h1>Manajemen Menu</h1><p className="text-muted">Kelola struktur menu, parent, route aplikasi, icon dan urutan.</p></div>{!showForm && <button type="button" onClick={handleAdd}>+ Tambah Menu</button>}</div>
      {loading ? <div className="card">Memuat data menu...</div> : (
        <div className="menu-management-layout">
          <div className="menu-list-panel">
            <div className="menu-panel-header"><div><h3>Daftar Menu</h3><span>{activeMenus.length} menu aktif</span></div></div>
            <div className="menu-search"><input value={search} onChange={e => setSearch(e.target.value)} placeholder="Cari nama, kode atau route..." /></div>
            <MenuTree menus={activeMenus} selectedId={editingId} search={search} onSelect={handleEdit} onDisable={handleDisable} />
          </div>
          <div className="menu-editor-panel">
            {showForm ? <MenuForm menus={activeMenus} editingId={editingId} form={form} saving={saving} onChange={setForm} onSave={handleSave} onCancel={handleCancel} /> : (
              <div className="menu-welcome"><div className="menu-welcome-icon">☰</div><h2>Manajemen Menu</h2><p>Pilih menu di sebelah kiri untuk mengubahnya, atau klik Tambah Menu untuk membuat menu baru.</p><button type="button" onClick={handleAdd}>+ Tambah Menu</button></div>
            )}
          </div>
        </div>
      )}
    </section>
  )
}
