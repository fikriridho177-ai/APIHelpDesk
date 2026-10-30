import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { deleteJenis, getJenisList, getLayananErrorMessage, saveJenis } from '../../layanan/api/layananApi'
import type { LynJenis, LynJenisList, SaveLynJenisPayload } from '../../layanan/types/layanan'
import { NoAccess, SensusHero } from '../../sensus/components/SensusParts'
import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { PMT_MENU } from '../permintaanShared'
import '../../layanan/layanan.css'
import '../../sensus/sensus.css'

const EMPTY: SaveLynJenisPayload = { divisionMenuId: 0, code: '', name: '', description: '', isOther: false, sortOrder: 0, active: true }

// /permintaan/master-jenis : pilihan "Jenis Permintaan" di form pengajuan, dikelompokkan per divisi.
// Hak: Read = lihat, Create / Update / Delete = kelola (hanya divisi yang user bisa proses: Update / Approve di menu divisi).
export default function MasterJenisPermintaanPage() {
  const { permission, loaded } = useMenuPermission(PMT_MENU.masterJenis)

  const [data, setData] = useState<LynJenisList>({ divisions: [], items: [] })
  const [division, setDivision] = useState('')
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<{ id: number | null; form: SaveLynJenisPayload } | null>(null)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const canRead = !!permission?.canRead

  const load = useCallback(async () => setData(await getJenisList({ all: true })), [])

  useEffect(() => {
    if (canRead) load().catch(err => console.error('Gagal memuat jenis permintaan:', err))
  }, [canRead, load])

  const manageable = data.divisions.filter(d => d.canManage)
  const canManage = (divisionMenuId: number) => manageable.some(d => d.id === divisionMenuId)

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase()
    return data.items.filter(
      j =>
        (!division || j.divisionCode === division) &&
        (!q || `${j.name} ${j.code} ${j.description ?? ''}`.toLowerCase().includes(q)),
    )
  }, [data.items, division, search])

  if (!canRead) return <NoAccess loaded={loaded} />

  function openNew() {
    const defaultDivision = manageable.find(d => d.code === division) ?? manageable[0]
    const lastOrder = Math.max(0, ...data.items.filter(j => j.divisionMenuId === defaultDivision?.id).map(j => j.sortOrder))
    setError('')
    setEditing({ id: null, form: { ...EMPTY, divisionMenuId: defaultDivision?.id ?? 0, sortOrder: lastOrder + 1 } })
  }

  function openEdit(j: LynJenis) {
    setError('')
    setEditing({
      id: j.id,
      form: {
        divisionMenuId: j.divisionMenuId,
        code: j.code,
        name: j.name,
        description: j.description ?? '',
        isOther: j.isOther,
        sortOrder: j.sortOrder,
        active: j.active,
      },
    })
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!editing) return
    setSaving(true)
    setError('')
    try {
      await saveJenis(editing.id, { ...editing.form, code: editing.form.code?.trim() || undefined })
      setEditing(null)
      await load()
    } catch (err) {
      setError(getLayananErrorMessage(err, 'Gagal menyimpan jenis permintaan.'))
    } finally {
      setSaving(false)
    }
  }

  async function remove(j: LynJenis) {
    const text = j.usedCount > 0
      ? `Jenis ${j.name} sudah dipakai ${j.usedCount} tiket, jadi hanya dinonaktifkan. Lanjutkan?`
      : `Hapus jenis ${j.name}?`
    if (!confirm(text)) return
    try {
      const result = await deleteJenis(j.id)
      if (result.deactivated) alert(result.message)
      await load()
    } catch (err) {
      alert(getLayananErrorMessage(err, 'Gagal menghapus jenis permintaan.'))
    }
  }

  const set = (patch: Partial<SaveLynJenisPayload>) => setEditing(s => (s ? { ...s, form: { ...s.form, ...patch } } : s))

  return (
    <section className="lyn-page">
      <SensusHero
        title="Master Jenis Permintaan"
        subtitle="Daftar jenis permintaan per divisi. Jenis yang aktif tampil sebagai pilihan Jenis Permintaan di form pengajuan divisi tersebut."
      />

      <div className="lyn-card">
        <div className="sns-toolbar">
          <div className="lyn-status-filter">
            <button type="button" className={division === '' ? 'lyn-chip active' : 'lyn-chip'} onClick={() => setDivision('')}>
              Semua divisi <b>{data.items.length}</b>
            </button>
            {data.divisions.map(d => (
              <button key={d.id} type="button" className={division === d.code ? 'lyn-chip active' : 'lyn-chip'} onClick={() => setDivision(d.code)}>
                {d.name} <b>{data.items.filter(j => j.divisionMenuId === d.id).length}</b>
              </button>
            ))}
          </div>

          <div className="sns-toolbar-right">
            <span className="lyn-search">
              <DashboardIcon name="search" />
              <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Cari jenis..." />
            </span>
            {permission?.canCreate && manageable.length > 0 && (
              <button type="button" className="lyn-button primary" onClick={openNew}>
                <DashboardIcon name="plus" /> Tambah jenis
              </button>
            )}
          </div>
        </div>

        <div className="table-container">
          <table className="sns-table">
            <thead>
              <tr>
                <th>Divisi</th>
                <th className="num">Urutan</th>
                <th>Nama jenis</th>
                <th>Kode</th>
                <th>Keterangan</th>
                <th>Isian sendiri</th>
                <th className="num">Dipakai</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 && (
                <tr>
                  <td colSpan={9} className="lyn-muted">
                    {data.items.length === 0 ? 'Belum ada jenis permintaan.' : 'Jenis tidak ditemukan.'}
                  </td>
                </tr>
              )}
              {rows.map(j => (
                <tr key={j.id} className={j.active ? '' : 'pmt-inactive'}>
                  <td>{j.divisionName}</td>
                  <td className="num">{j.sortOrder}</td>
                  <td>
                    <b>{j.name}</b>
                  </td>
                  <td className="sns-no">{j.code}</td>
                  <td>{j.description || '-'}</td>
                  <td>{j.isOther ? 'Ya (Lainnya)' : '-'}</td>
                  <td className="num">{j.usedCount}</td>
                  <td>
                    <span className={`lyn-badge tone-${j.active ? 'green' : 'gray'}`}>{j.active ? 'Aktif' : 'Nonaktif'}</span>
                  </td>
                  <td className="sns-actions">
                    {canManage(j.divisionMenuId) && (
                      <>
                        {permission?.canUpdate && (
                          <button type="button" className="lyn-icon-button" onClick={() => openEdit(j)}>
                            Ubah
                          </button>
                        )}
                        {permission?.canDelete && j.active && (
                          <button type="button" className="lyn-icon-button danger-text" onClick={() => void remove(j)}>
                            {j.usedCount > 0 ? 'Nonaktifkan' : 'Hapus'}
                          </button>
                        )}
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <p className="lyn-muted">
          Hak di halaman ini diatur lewat Hak Akses &gt; Akses Menu (menu "Master Jenis Permintaan"). Jenis hanya bisa dikelola oleh
          group yang punya hak Update atau Approve di menu divisinya.
        </p>
      </div>

      {editing && (
        <div className="lyn-modal-backdrop" onClick={() => setEditing(null)}>
          <form className="lyn-modal sns-form-modal" onClick={e => e.stopPropagation()} onSubmit={submit}>
            <div className="lyn-modal-header">
              <h2>{editing.id == null ? 'Tambah jenis permintaan' : 'Ubah jenis permintaan'}</h2>
              <button type="button" className="lyn-icon-button" onClick={() => setEditing(null)} aria-label="Tutup">
                ✕
              </button>
            </div>
            <div className="lyn-modal-body">
              <div className="lyn-form-grid">
                <label className="lyn-field">
                  <span>Divisi *</span>
                  <select required value={editing.form.divisionMenuId || ''} onChange={e => set({ divisionMenuId: Number(e.target.value) })}>
                    <option value="">Pilih divisi</option>
                    {manageable.map(d => (
                      <option key={d.id} value={d.id}>
                        {d.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="lyn-field">
                  <span>Nama jenis *</span>
                  <input required maxLength={150} value={editing.form.name} onChange={e => set({ name: e.target.value })} placeholder="Contoh: Pembuatan Peta" />
                </label>
                <label className="lyn-field">
                  <span>Kode</span>
                  <input maxLength={50} value={editing.form.code ?? ''} onChange={e => set({ code: e.target.value })} placeholder="Kosongkan = dibuat otomatis" />
                </label>
                <label className="lyn-field">
                  <span>Urutan tampil</span>
                  <input type="number" value={editing.form.sortOrder} onChange={e => set({ sortOrder: Number(e.target.value) || 0 })} />
                </label>
                <label className="lyn-field sns-wide">
                  <span>Keterangan (tampil di bawah pilihan)</span>
                  <input maxLength={500} value={editing.form.description ?? ''} onChange={e => set({ description: e.target.value })} />
                </label>
                <label className="sns-inline-check">
                  <input type="checkbox" checked={editing.form.isOther} onChange={e => set({ isOther: e.target.checked })} />
                  Jenis "Lainnya" (pemohon menuliskan sendiri jenis permintaannya)
                </label>
                <label className="sns-inline-check">
                  <input type="checkbox" checked={editing.form.active} onChange={e => set({ active: e.target.checked })} />
                  Aktif (tampil di form pengajuan)
                </label>
              </div>
              {error && <p className="error">{error}</p>}
              <div className="lyn-form-actions">
                <button type="button" className="lyn-button ghost" onClick={() => setEditing(null)}>
                  Batal
                </button>
                <button type="submit" className="lyn-button primary" disabled={saving}>
                  {saving ? 'Menyimpan...' : 'Simpan'}
                </button>
              </div>
            </div>
          </form>
        </div>
      )}
    </section>
  )
}
