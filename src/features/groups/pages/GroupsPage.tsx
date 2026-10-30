import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  createGroup,
  disableGroup,
  getGroups,
  updateGroup,
} from '../api/groupApi'
import GroupForm from '../components/GroupForm'
import type { Group, SaveGroupRequest } from '../types/group'

const emptyForm: SaveGroupRequest = {
  code: '',
  name: '',
  description: '',
  isSuperAdmin: false,
  active: true,
}

export default function GroupsPage() {
  const navigate = useNavigate()
  const [data, setData] = useState<Group[]>([])
  const [search, setSearch] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [form, setForm] = useState<SaveGroupRequest>(emptyForm)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)

  async function load() {
    try {
      setLoading(true)
      setData(await getGroups())
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return data
    return data.filter(x => `${x.code} ${x.name} ${x.description ?? ''}`.toLowerCase().includes(q))
  }, [data, search])

  function add() {
    setEditingId(null)
    setForm({ ...emptyForm })
    setShowForm(true)
  }

  function edit(group: Group) {
    setEditingId(group.id)
    setForm({
      code: group.code,
      name: group.name,
      description: group.description ?? '',
      isSuperAdmin: group.isSuperAdmin,
      active: group.active,
    })
    setShowForm(true)
  }

  function cancel() {
    setEditingId(null)
    setForm({ ...emptyForm })
    setShowForm(false)
  }

  async function save() {
    try {
      setSaving(true)
      if (editingId !== null) await updateGroup(editingId, form)
      else await createGroup(form)
      cancel()
      await load()
    } finally {
      setSaving(false)
    }
  }

  async function disable(group: Group) {
    if (!window.confirm(`Nonaktifkan hak akses "${group.name}"?`)) return
    await disableGroup(group.id)
    await load()
  }

  const fmt = (value?: string | null) => value ? new Date(value).toLocaleString('id-ID') : '-'

  return (
    <section className="group-access-page">
      <div className="page-header">
        <div>
          <h1>Hak Akses</h1>
          <p className="text-muted">Kelola group, permission menu, perusahaan anak, departemen dan item sub kategori.</p>
        </div>
        {!showForm && <button type="button" onClick={add}>+ Tambah</button>}
      </div>

      {showForm && (
        <GroupForm
          value={form}
          editing={editingId !== null}
          saving={saving}
          onChange={setForm}
          onSave={save}
          onCancel={cancel}
        />
      )}

      <div className="card group-access-list">
        <div className="group-list-toolbar">
          <div>
            <h3>Daftar Hak Akses</h3>
            <span>{filtered.length} group</span>
          </div>
          <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Cari hak akses..." />
        </div>

        <div className="table-container">
          <table className="group-access-table">
            <thead>
              <tr>
                <th>Aksi</th>
                <th>Kode</th>
                <th>Nama Hak Akses</th>
                <th>Super Admin</th>
                <th>Aktif</th>
                <th>Dibuat Oleh</th>
                <th>Tanggal Dibuat</th>
                <th>Diperbarui Oleh</th>
                <th>Tanggal Diperbarui</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={9} className="empty-table">Memuat data...</td></tr>
              ) : filtered.length === 0 ? (
                <tr><td colSpan={9} className="empty-table">Data hak akses tidak ditemukan.</td></tr>
              ) : filtered.map(group => (
                <tr key={group.id} className={!group.active ? 'row-inactive' : ''}>
                  <td>
                    <div className="group-action-buttons">
                      <button className="access-action menu" type="button" title="Akses Menu" onClick={() => navigate(`/utility/groups/${group.id}/menus`)}>☰</button>
                      <button className="access-action company" type="button" title="Akses Perusahaan Anak" onClick={() => navigate(`/utility/groups/${group.id}/companies`)}>▣</button>
                      <button className="access-action department" type="button" title="Akses Departemen" onClick={() => navigate(`/utility/groups/${group.id}/departments`)}>▦</button>
                      <button className="access-action item" type="button" title="Akses Item Sub Kategori" onClick={() => navigate(`/utility/groups/${group.id}/item-sub-categories`)}>◆</button>
                      <button className="access-action edit" type="button" title="Edit" onClick={() => edit(group)}>✎</button>
                      {group.active && <button className="access-action danger" type="button" title="Nonaktifkan" onClick={() => disable(group)}>🗑</button>}
                    </div>
                  </td>
                  <td><span className="code-badge">{group.code}</span></td>
                  <td>
                    <strong>{group.name}</strong>
                    {group.description && <div className="table-subtext">{group.description}</div>}
                  </td>
                  <td>{group.isSuperAdmin ? <span className="status-badge yes">Ya</span> : 'Tidak'}</td>
                  <td><span className={group.active ? 'status-badge yes' : 'status-badge no'}>{group.active ? 'Ya' : 'Tidak'}</span></td>
                  <td>{group.createdBy ?? '-'}</td>
                  <td>{fmt(group.createdAt)}</td>
                  <td>{group.updatedBy ?? '-'}</td>
                  <td>{fmt(group.updatedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  )
}
