import type { FormEvent } from 'react'
import type { SaveGroupRequest } from '../types/group'

type Props = {
  value: SaveGroupRequest
  editing: boolean
  saving: boolean
  onChange: (value: SaveGroupRequest) => void
  onSave: () => Promise<void>
  onCancel: () => void
}

export default function GroupForm({ value, editing, saving, onChange, onSave, onCancel }: Props) {
  async function submit(e: FormEvent) {
    e.preventDefault()
    await onSave()
  }

  return (
    <form className="card access-group-form" onSubmit={submit}>
      <div className="access-form-heading">
        <div>
          <h2>{editing ? 'Edit Hak Akses' : 'Tambah Hak Akses'}</h2>
          <p>Kelola identitas group. Permission menu dan scope data diatur lewat tombol aksi pada daftar.</p>
        </div>
      </div>

      <div className="access-form-grid">
        <label>
          Kode Group
          <input
            required
            value={value.code}
            onChange={e => onChange({ ...value, code: e.target.value.toUpperCase().replace(/\s+/g, '_') })}
            placeholder="Administrator"
          />
        </label>

        <label>
          Nama Group
          <input
            required
            value={value.name}
            onChange={e => onChange({ ...value, name: e.target.value })}
            placeholder="Administrator"
          />
        </label>

        <label className="access-form-full">
          Deskripsi
          <textarea
            rows={3}
            value={value.description ?? ''}
            onChange={e => onChange({ ...value, description: e.target.value })}
            placeholder="Keterangan singkat hak akses"
          />
        </label>

        <label className="access-checkbox-field">
          <input
            type="checkbox"
            checked={value.isSuperAdmin}
            onChange={e => onChange({ ...value, isSuperAdmin: e.target.checked })}
          />
          <span>Super Administrator</span>
        </label>

        <label className="access-checkbox-field">
          <input
            type="checkbox"
            checked={value.active}
            onChange={e => onChange({ ...value, active: e.target.checked })}
          />
          <span>Aktif</span>
        </label>
      </div>

      <div className="form-actions">
        <button type="button" className="secondary" onClick={onCancel}>Batal</button>
        <button type="submit" disabled={saving}>{saving ? 'Menyimpan...' : editing ? 'Update' : 'Simpan'}</button>
      </div>
    </form>
  )
}
