import { useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { MenuItem, MenuType, SaveMenuRequest } from '../types/menu'
import { DashboardIcon, ICON_NAMES, pickIconName } from '../../dashboard/components/DashboardIcon'
import { CARD_COLORS } from '../../layanan/layananConfig'
import '../../layanan/layanan.css'

type Props = {
  menus: MenuItem[]
  editingId: number | null
  form: SaveMenuRequest
  saving: boolean
  onChange: (value: SaveMenuRequest) => void
  onSave: () => Promise<void>
  onCancel: () => void
}

function toSlug(value: string) {
  return value
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

function normalizeRoute(value: string) {
  const trimmed = value.trim()

  if (!trimmed) {
    return ''
  }

  const withLeadingSlash = trimmed.startsWith('/') ? trimmed : `/${trimmed}`
  const normalized = withLeadingSlash.replace(/\/{2,}/g, '/')

  return normalized.length > 1 && normalized.endsWith('/')
    ? normalized.slice(0, -1)
    : normalized
}

export default function MenuForm({
  menus,
  editingId,
  form,
  saving,
  onChange,
  onSave,
  onCancel,
}: Props) {
  const parentSelectRef = useRef<HTMLDivElement | null>(null)
  const [parentOpen, setParentOpen] = useState(false)
  const [parentSearch, setParentSearch] = useState('')
  const [routeEditedManually, setRouteEditedManually] = useState(false)

  const menuById = useMemo(
    () => new Map(menus.map(menu => [menu.id, menu])),
    [menus],
  )

  const selectedParent = form.parentId ? menuById.get(form.parentId) : undefined

  function isDescendant(candidateId: number, ancestorId: number) {
    let current = menuById.get(candidateId)
    const visited = new Set<number>()

    while (current?.parentId) {
      if (visited.has(current.id)) {
        return false
      }

      visited.add(current.id)

      if (current.parentId === ancestorId) {
        return true
      }

      current = menuById.get(current.parentId)
    }

    return false
  }

  const parentOptions = useMemo(
    () =>
      menus.filter(menu => {
        if (!menu.active || menu.id === editingId) {
          return false
        }

        if (editingId !== null && isDescendant(menu.id, editingId)) {
          return false
        }

        return true
      }),
    [menus, editingId, menuById],
  )

  const filteredParentOptions = useMemo(() => {
    const keyword = parentSearch.trim().toLowerCase()

    if (!keyword) {
      return parentOptions
    }

    return parentOptions.filter(menu =>
      `${menu.code} ${menu.name} ${menu.route ?? ''}`
        .toLowerCase()
        .includes(keyword),
    )
  }, [parentOptions, parentSearch])

  function buildParentPath(parentId: number | null | undefined) {
    if (!parentId) {
      return ''
    }

    const segments: string[] = []
    const visited = new Set<number>()
    let current = menuById.get(parentId)

    while (current) {
      if (visited.has(current.id)) {
        break
      }

      visited.add(current.id)

      if (current.route?.trim()) {
        return normalizeRoute(
          `${current.route}/${segments.reverse().join('/')}`,
        )
      }

      const segment = toSlug(current.name)

      if (segment) {
        segments.push(segment)
      }

      current = current.parentId ? menuById.get(current.parentId) : undefined
    }

    return segments.length > 0
      ? normalizeRoute(`/${segments.reverse().join('/')}`)
      : ''
  }

  function buildAutomaticRoute(
    name: string,
    parentId: number | null | undefined,
  ) {
    const childSegment = toSlug(name)

    if (!childSegment) {
      return ''
    }

    const parentPath = buildParentPath(parentId)

    return normalizeRoute(`${parentPath}/${childSegment}`)
  }

  function getNextSortOrder(parentId: number | null) {
    if (parentId === null) {
      const rootMenus = menus.filter(
        menu => menu.parentId == null && menu.id !== editingId,
      )

      if (rootMenus.length === 0) {
        return 1
      }

      return Math.max(...rootMenus.map(menu => menu.sortOrder)) + 1
    }

    const parent = menuById.get(parentId)

    if (!parent) {
      return form.sortOrder
    }

    const children = menus.filter(
      menu => menu.parentId === parentId && menu.id !== editingId,
    )

    if (children.length === 0) {
      return parent.sortOrder + 1
    }

    return Math.max(
      parent.sortOrder,
      ...children.map(menu => menu.sortOrder),
    ) + 1
  }

  function updateForm(patch: Partial<SaveMenuRequest>) {
    onChange({
      ...form,
      ...patch,
    })
  }

  function handleNameChange(name: string) {
    const patch: Partial<SaveMenuRequest> = { name }

    if (!routeEditedManually && form.menuType !== 'WIDGET') {
      patch.route = buildAutomaticRoute(name, form.parentId)
    }

    updateForm(patch)
  }

  function handleParentSelect(parentId: number | null) {
    const patch: Partial<SaveMenuRequest> = {
      parentId,
      sortOrder: getNextSortOrder(parentId),
    }

    if (!routeEditedManually && form.menuType !== 'WIDGET') {
      patch.route = buildAutomaticRoute(form.name, parentId)
    }

    updateForm(patch)
    setParentSearch('')
    setParentOpen(false)
  }

  function handleMenuTypeChange(menuType: MenuType) {
    // Widget portal tidak punya route (backend juga menyimpannya kosong).
    if (menuType === 'WIDGET') {
      updateForm({ menuType, route: '' })
      return
    }

    updateForm({
      menuType,
      route: routeEditedManually
        ? form.route
        : buildAutomaticRoute(form.name, form.parentId),
    })
  }

  function handleRouteChange(route: string) {
    setRouteEditedManually(true)
    updateForm({ route: normalizeRoute(route) })
  }

  // Buat ulang route dari Parent + Nama (hanya bila memang ingin mengganti alamat halaman).
  function handleRegenerateRoute() {
    if (
      editingId !== null &&
      !window.confirm('Route baru hanya berfungsi jika halaman untuk route tersebut sudah ada. Lanjutkan?')
    ) {
      return
    }

    setRouteEditedManually(false)
    updateForm({ route: buildAutomaticRoute(form.name, form.parentId) })
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    await onSave()
  }

  useEffect(() => {
    setParentOpen(false)
    setParentSearch('')
    // Tambah menu: route mengikuti Parent Menu + Nama Menu secara otomatis.
    // Edit menu yang sudah punya route: route DIKUNCI, karena route menentukan halaman yang dibuka.
    // Jadi memindahkan menu ke parent lain / mengganti nama tetap membuka halaman yang sama.
    setRouteEditedManually(editingId !== null && !!form.route?.trim())
    // Reset only when switching between add/edit records.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editingId])

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (
        parentSelectRef.current &&
        !parentSelectRef.current.contains(event.target as Node)
      ) {
        setParentOpen(false)
        setParentSearch('')
      }
    }

    function handleEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setParentOpen(false)
        setParentSearch('')
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    document.addEventListener('keydown', handleEscape)

    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [])

  return (
    <form className="menu-form" onSubmit={handleSubmit}>
      <div className="menu-form-header">
        <div>
          <h2>{editingId ? 'Edit Menu' : 'Tambah Menu'}</h2>
          <p>Atur struktur menu, parent, route, icon dan urutan tampil.</p>
        </div>
      </div>

      <div className="menu-form-grid">
        
        <label>
          Tipe Menu
          <select
            value={form.menuType}
            onChange={e => handleMenuTypeChange(e.target.value as MenuType)}
          >
            <option value="MENU">Module / Halaman</option>
            <option value="GROUP">Parent / Menu</option>
            <option value="WIDGET">Widget Portal</option>
          </select>
        </label>

        <label>
          Parent Menu
          <div className="parent-menu-select" ref={parentSelectRef}>
            <button
              type="button"
              className={`parent-menu-control ${parentOpen ? 'open' : ''}`}
              onClick={() => setParentOpen(open => !open)}
              aria-expanded={parentOpen}
              style={{ color: '#0f172a', backgroundColor: '#ffffff' }}
            >
              <span
                className={
                  selectedParent
                    ? 'parent-menu-selected-value'
                    : 'parent-menu-placeholder'
                }
                style={{
                  color: selectedParent ? '#0f172a' : '#64748b',
                  backgroundColor: 'transparent',
                  fontWeight: selectedParent ? 500 : 400,
                }}
                title={
                  selectedParent
                    ? `${selectedParent.code} - ${selectedParent.name}`
                    : '-- Root Menu --'
                }
              >
                {selectedParent
                  ? `${selectedParent.code} - ${selectedParent.name}`
                  : '-- Root Menu --'}
              </span>
              <span
                className="parent-menu-arrow"
                aria-hidden="true"
                style={{ color: '#64748b' }}
              >
                {parentOpen ? '▲' : '▼'}
              </span>
            </button>

            {parentOpen && (
              <div className="parent-menu-dropdown">
                <div className="parent-menu-search-wrap">
                  <input
                    autoFocus
                    type="text"
                    value={parentSearch}
                    onChange={e => setParentSearch(e.target.value)}
                    placeholder="Cari nama, kode atau route parent..."
                  />
                </div>

                <div className="parent-menu-options">
                  <button
                    type="button"
                    className={`parent-menu-option ${
                      form.parentId == null ? 'selected' : ''
                    }`}
                    onClick={() => handleParentSelect(null)}
                  >
                    <strong>-- Root Menu --</strong>
                    <small>Menu tingkat paling atas</small>
                  </button>

                  {filteredParentOptions.map(menu => (
                    <button
                      key={menu.id}
                      type="button"
                      className={`parent-menu-option ${
                        form.parentId === menu.id ? 'selected' : ''
                      }`}
                      onClick={() => handleParentSelect(menu.id)}
                    >
                      <strong>{menu.name}</strong>
                      <small>
                        {menu.code}
                        {menu.route ? ` • ${menu.route}` : ''}
                      </small>
                    </button>
                  ))}

                  {filteredParentOptions.length === 0 && (
                    <div className="parent-menu-empty">
                      Parent menu tidak ditemukan.
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </label>

        <label>
          Nama Menu
          <input
            required
            value={form.name}
            onChange={e => handleNameChange(e.target.value)}
            placeholder="Contoh: Master User"
          />
        </label>

        <label>
          Kode Menu
          <input
            required
            value={form.code}
            onChange={e =>
              updateForm({
                code: e.target.value.toUpperCase().replace(/\s+/g, '_'),
              })
            }
            placeholder="MASTER_USER"
          />
        </label>

        {form.menuType === 'WIDGET' ? (
          <label className="menu-form-full">
            Route / Path
            <input value="" disabled placeholder="Widget tidak punya route" />
            <small>Widget tampil di Portal (halaman utama). Siapa yang melihatnya diatur di Hak Akses &gt; Akses Menu (kolom View).</small>
          </label>
        ) : (
        <label className="menu-form-full">
          Route / Path
          <input
            value={form.route ?? ''}
            onChange={e => handleRouteChange(e.target.value)}
            placeholder="/help-desk/master-user"
          />
          <small>
            {routeEditedManually
              ? 'Route dikunci: memindahkan parent atau mengganti nama tetap membuka halaman yang sama. '
              : 'Route dibuat otomatis berdasarkan Parent Menu + Nama Menu. '}
            <button type="button" className="menu-route-regenerate" onClick={handleRegenerateRoute}>
              Buat ulang otomatis
            </button>
          </small>
        </label>
        )}

        <label>
          Icon
          <span className="menu-icon-field">
            <span className={`menu-icon-preview tone-${form.color || 'gray'}`}>
              <DashboardIcon name={pickIconName({ code: form.code, name: form.name, route: form.route, icon: form.icon })} />
            </span>
            <input
              list="menu-icon-options"
              value={form.icon ?? ''}
              onChange={e => updateForm({ icon: e.target.value })}
              placeholder="Kosong = otomatis"
            />
          </span>
          <datalist id="menu-icon-options">
            {ICON_NAMES.map(name => (
              <option key={name} value={name} />
            ))}
          </datalist>
        </label>

        <label>
          Warna Kartu
          <select value={form.color ?? ''} onChange={e => updateForm({ color: e.target.value })}>
            <option value="">Otomatis</option>
            {CARD_COLORS.map(color => (
              <option key={color.value} value={color.value}>
                {color.label}
              </option>
            ))}
          </select>
        </label>

        <label className="menu-form-full">
          Nama Lengkap
          <input
            value={form.fullName ?? ''}
            onChange={e => updateForm({ fullName: e.target.value })}
            placeholder="Contoh: Research & Agronomy"
          />
        </label>

        <label className="menu-form-full">
          Deskripsi
          <textarea
            rows={2}
            maxLength={500}
            value={form.description ?? ''}
            onChange={e => updateForm({ description: e.target.value })}
            placeholder="Keterangan singkat yang tampil di kartu dashboard"
          />
          <small>Nama lengkap, deskripsi, icon dan warna dipakai pada kartu divisi di dashboard.</small>
        </label>

        <label>
          Urutan Menu
          <input
            type="number"
            min={0}
            value={form.sortOrder}
            onChange={e => updateForm({ sortOrder: Number(e.target.value) })}
          />
        </label>

        <label className="menu-active-field">
          <input
            type="checkbox"
            checked={form.active}
            onChange={e => updateForm({ active: e.target.checked })}
          />
          <span>Menu Aktif</span>
        </label>
      </div>

      <div className="menu-form-actions">
        <button type="button" className="secondary" onClick={onCancel}>
          Batal
        </button>
        <button type="submit" disabled={saving}>
          {saving ? 'Menyimpan...' : editingId ? 'Update Menu' : 'Simpan Menu'}
        </button>
      </div>
    </form>
  )
}
