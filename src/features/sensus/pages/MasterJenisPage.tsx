import { FormEvent, useCallback, useEffect, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { deactivateMaster, deleteParameter, getJenis, getSensusErrorMessage, saveMaster, saveParameter } from '../api/sensusApi'
import { NoAccess, SensusHero } from '../components/SensusParts'
import { useMenuPermission } from '../hooks/useSensus'
import { SNS_MENU, formatAngka } from '../sensusConfig'
import type { SnsJenis, SnsParameter } from '../types/sensus'
import '../../layanan/layanan.css'
import '../sensus.css'

type JenisForm = Omit<SnsJenis, 'id' | 'parameters'>
type ParamForm = Omit<SnsParameter, 'id' | 'jenisId' | 'ambangSedang' | 'ambangBerat'> & { ambangSedang: string; ambangBerat: string }

const EMPTY_JENIS: JenisForm = {
  code: '', name: '', kategori: 'HPT', komoditas: 'KELAPA', menuCode: '', description: '', active: true,
}

const EMPTY_PARAM: ParamForm = {
  code: '', name: '', satuan: '', tipeData: 'NUMBER', opsi: '', wajib: false, isIndikator: false,
  ambangSedang: '', ambangBerat: '', active: true,
}

const matches = (search: string, ...values: (string | null | undefined)[]) => {
  const q = search.trim().toLowerCase()
  return !q || values.some(v => v?.toLowerCase().includes(q))
}

// /sensus/master/jenis : jenis sensus (Plasispa, Ulat Api, ...) dan parameter yang diisi di Android.
export default function MasterJenisPage() {
  const { permission, loaded } = useMenuPermission(SNS_MENU.masterJenis)

  const [list, setList] = useState<SnsJenis[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [jenisForm, setJenisForm] = useState<{ id: number | null; form: JenisForm } | null>(null)
  const [paramForm, setParamForm] = useState<{ id: number | null; form: ParamForm } | null>(null)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [jenisSearch, setJenisSearch] = useState('')
  const [paramSearch, setParamSearch] = useState('')

  const canRead = !!permission?.canRead
  const canUpdate = !!permission?.canUpdate

  const load = useCallback(async () => {
    const data = await getJenis()
    setList(data)
    setSelectedId(current => current ?? data[0]?.id ?? null)
  }, [])

  useEffect(() => {
    if (canRead) load().catch(err => console.error('Gagal memuat jenis sensus:', err))
  }, [canRead, load])

  if (!canRead) return <NoAccess loaded={loaded} />

  const selected = list.find(x => x.id === selectedId)
  const jenisRows = list.filter(j => matches(jenisSearch, j.name, j.code, j.kategori, j.komoditas))
  const paramRows = (selected?.parameters ?? []).filter(p => matches(paramSearch, p.name, p.code, p.satuan))

  async function run(action: () => Promise<unknown>, onDone: () => void) {
    setSaving(true)
    setError('')
    try {
      await action()
      onDone()
      await load()
    } catch (err) {
      setError(getSensusErrorMessage(err, 'Gagal menyimpan data.'))
    } finally {
      setSaving(false)
    }
  }

  function submitJenis(event: FormEvent) {
    event.preventDefault()
    if (!jenisForm) return
    const body = jenisForm.form
    void run(() => saveMaster('jenis', jenisForm.id, body), () => setJenisForm(null))
  }

  function submitParam(event: FormEvent) {
    event.preventDefault()
    if (!paramForm || !selected) return
    const f = paramForm.form
    const body = {
      ...f,
      ambangSedang: f.ambangSedang === '' ? null : Number(f.ambangSedang),
      ambangBerat: f.ambangBerat === '' ? null : Number(f.ambangBerat),
    }
    void run(() => saveParameter(selected.id, paramForm.id, body), () => setParamForm(null))
  }

  async function removeParam(p: SnsParameter) {
    if (!confirm(`Hapus parameter ${p.name}? Bila sudah dipakai sensus, parameter hanya dinonaktifkan.`)) return
    try {
      await deleteParameter(p.id)
      await load()
    } catch (err) {
      alert(getSensusErrorMessage(err, 'Gagal menghapus parameter.'))
    }
  }

  async function deactivateJenis(j: SnsJenis) {
    if (!confirm(`Nonaktifkan jenis sensus ${j.name}?`)) return
    try {
      await deactivateMaster('jenis', j.id)
      await load()
    } catch (err) {
      alert(getSensusErrorMessage(err, 'Gagal menonaktifkan jenis sensus.'))
    }
  }

  const setJ = (patch: Partial<JenisForm>) => setJenisForm(s => (s ? { ...s, form: { ...s.form, ...patch } } : s))
  const setP = (patch: Partial<ParamForm>) => setParamForm(s => (s ? { ...s, form: { ...s.form, ...patch } } : s))

  return (
    <section className="lyn-page">
      <SensusHero
        title="Master Jenis & Parameter"
        subtitle="Atur jenis sensus dan parameter yang diisi Asisten Lapangan, termasuk indikator & ambang batas tingkat serangan untuk analisis."
      />

      <div className="sns-master-layout">
        {/* DAFTAR JENIS */}
        <div className="lyn-card">
          <div className="sns-toolbar">
            <h3>Jenis sensus</h3>
            {permission?.canCreate && (
              <button
                type="button"
                className="lyn-button primary"
                onClick={() => {
                  setError('')
                  setJenisForm({ id: null, form: { ...EMPTY_JENIS } })
                }}
              >
                <DashboardIcon name="plus" /> Tambah
              </button>
            )}
          </div>

          <span className="lyn-search">
            <DashboardIcon name="search" />
            <input value={jenisSearch} onChange={e => setJenisSearch(e.target.value)} placeholder="Cari jenis..." />
          </span>

          <div className="sns-jenis-list sns-scroll">
            {jenisRows.length === 0 && <p className="lyn-muted">Jenis tidak ditemukan.</p>}
            {jenisRows.map(j => (
              <button
                key={j.id}
                type="button"
                className={`sns-jenis-item${j.id === selectedId ? ' active' : ''}${j.active ? '' : ' inactive'}`}
                onClick={() => { setSelectedId(j.id); setParamSearch('') }}
              >
                <strong>{j.name}</strong>
                <small>
                  {j.kategori} · {j.komoditas} · {j.parameters.filter(p => p.active).length} parameter
                  {!j.active && ' · nonaktif'}
                </small>
              </button>
            ))}
          </div>
        </div>

        {/* DETAIL & PARAMETER */}
        <div className="lyn-card">
          {!selected ? (
            <div className="lyn-empty">Pilih jenis sensus.</div>
          ) : (
            <>
              <div className="sns-toolbar">
                <div>
                  <h3>{selected.name}</h3>
                  <p className="lyn-muted">
                    Kode {selected.code} · {selected.kategori} · Komoditas {selected.komoditas}
                    {selected.menuCode && ` · Menu ${selected.menuCode}`}
                  </p>
                  {selected.description && <p className="lyn-muted">{selected.description}</p>}
                </div>
                <div className="sns-toolbar-right">
                  {canUpdate && (
                    <button
                      type="button"
                      className="lyn-button ghost"
                      onClick={() => {
                        const { id: _id, parameters: _p, ...form } = selected
                        setError('')
                        setJenisForm({ id: selected.id, form: { ...form, menuCode: form.menuCode ?? '', description: form.description ?? '' } })
                      }}
                    >
                      Ubah
                    </button>
                  )}
                  {permission?.canDelete && selected.active && (
                    <button type="button" className="lyn-button danger" onClick={() => deactivateJenis(selected)}>
                      Nonaktifkan
                    </button>
                  )}
                </div>
              </div>

              <div className="sns-toolbar">
                <h4 className="sns-subtitle">Parameter</h4>
                <div className="sns-toolbar-right">
                  <span className="lyn-search">
                    <DashboardIcon name="search" />
                    <input value={paramSearch} onChange={e => setParamSearch(e.target.value)} placeholder="Cari parameter..." />
                  </span>
                  {canUpdate && (
                    <button
                      type="button"
                      className="lyn-button primary"
                      onClick={() => {
                        setError('')
                        setParamForm({ id: null, form: { ...EMPTY_PARAM } })
                      }}
                    >
                      <DashboardIcon name="plus" /> Tambah parameter
                    </button>
                  )}
                </div>
              </div>

              <div className="table-container sns-scroll">
                <table className="sns-table sns-sticky-head">
                  <thead>
                    <tr>
                      <th className="num">#</th>
                      <th>Kode</th>
                      <th>Nama</th>
                      <th>Wajib</th>
                      <th>Indikator</th>
                      <th className="num">Ambang sedang</th>
                      <th className="num">Ambang berat</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {paramRows.length === 0 && (
                      <tr>
                        <td colSpan={8} className="lyn-muted">
                          {selected.parameters.length === 0 ? 'Belum ada parameter.' : 'Parameter tidak ditemukan.'}
                        </td>
                      </tr>
                    )}
                    {paramRows.map((p, i) => (
                      <tr key={p.id} className={p.active ? '' : 'row-inactive'}>
                        <td className="num lyn-muted">{i + 1}</td>
                        <td className="sns-no">{p.code}</td>
                        <td>
                          {p.name} {p.satuan && <small className="lyn-muted">({p.satuan})</small>}
                          {p.tipeData === 'OPTION' && p.opsi && <small className="lyn-muted"> · {p.opsi}</small>}
                        </td>
                        <td>{p.wajib ? 'Ya' : '-'}</td>
                        <td>{p.isIndikator ? <span className="sns-tag">indikator</span> : '-'}</td>
                        <td className="num">{formatAngka(p.ambangSedang)}</td>
                        <td className="num">{formatAngka(p.ambangBerat)}</td>
                        <td className="sns-actions">
                          {canUpdate && (
                            <>
                              <button
                                type="button"
                                className="lyn-icon-button"
                                onClick={() => {
                                  const { id: _id, jenisId: _j, ...rest } = p
                                  setError('')
                                  setParamForm({
                                    id: p.id,
                                    form: {
                                      ...rest,
                                      satuan: rest.satuan ?? '',
                                      opsi: rest.opsi ?? '',
                                      ambangSedang: rest.ambangSedang == null ? '' : String(rest.ambangSedang),
                                      ambangBerat: rest.ambangBerat == null ? '' : String(rest.ambangBerat),
                                    },
                                  })
                                }}
                              >
                                Ubah
                              </button>
                              <button type="button" className="lyn-icon-button danger-text" onClick={() => removeParam(p)}>
                                Hapus
                              </button>
                            </>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <p className="lyn-muted">
                Tingkat serangan dihitung dari rata-rata parameter <b>indikator</b>: ≥ ambang berat = Berat, ≥ ambang sedang = Sedang,
                &gt; 0 = Ringan, 0 = Nihil.
              </p>
            </>
          )}
        </div>
      </div>

      {/* FORM JENIS */}
      {jenisForm && (
        <div className="lyn-modal-backdrop" onClick={() => setJenisForm(null)}>
          <form className="lyn-modal sns-form-modal" onClick={e => e.stopPropagation()} onSubmit={submitJenis}>
            <div className="lyn-modal-header">
              <h2>{jenisForm.id ? 'Ubah' : 'Tambah'} jenis sensus</h2>
              <button type="button" className="lyn-icon-button" onClick={() => setJenisForm(null)} aria-label="Tutup">
                ✕
              </button>
            </div>
            <div className="lyn-modal-body">
              <div className="lyn-form-grid">
                <label className="lyn-field">
                  <span>Kode</span>
                  <input required maxLength={30} value={jenisForm.form.code} onChange={e => setJ({ code: e.target.value.toUpperCase() })} />
                </label>
                <label className="lyn-field">
                  <span>Nama</span>
                  <input required maxLength={150} value={jenisForm.form.name} onChange={e => setJ({ name: e.target.value })} />
                </label>
                <label className="lyn-field">
                  <span>Kategori</span>
                  <select value={jenisForm.form.kategori} onChange={e => setJ({ kategori: e.target.value as JenisForm['kategori'] })}>
                    <option value="HPT">HPT (hama & penyakit)</option>
                    <option value="TANAMAN">Tanaman</option>
                  </select>
                </label>
                <label className="lyn-field">
                  <span>Komoditas</span>
                  <select value={jenisForm.form.komoditas} onChange={e => setJ({ komoditas: e.target.value as JenisForm['komoditas'] })}>
                    <option value="KELAPA">Kelapa</option>
                    <option value="NANAS">Nanas</option>
                    <option value="SEMUA">Semua</option>
                  </select>
                </label>
                <label className="lyn-field">
                  <span>Kode menu terkait (opsional)</span>
                  <input value={jenisForm.form.menuCode ?? ''} onChange={e => setJ({ menuCode: e.target.value })} placeholder="mis. LYN_RNA_HPT_ULAT_API" />
                </label>
                <label className="lyn-field sns-wide">
                  <span>Keterangan</span>
                  <textarea rows={2} value={jenisForm.form.description ?? ''} onChange={e => setJ({ description: e.target.value })} />
                </label>
                <label className="sns-inline-check">
                  <input type="checkbox" checked={jenisForm.form.active} onChange={e => setJ({ active: e.target.checked })} /> Aktif
                </label>
              </div>
              {error && <p className="error">{error}</p>}
              <div className="lyn-form-actions">
                <button type="button" className="lyn-button ghost" onClick={() => setJenisForm(null)}>
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

      {/* FORM PARAMETER */}
      {paramForm && (
        <div className="lyn-modal-backdrop" onClick={() => setParamForm(null)}>
          <form className="lyn-modal sns-form-modal" onClick={e => e.stopPropagation()} onSubmit={submitParam}>
            <div className="lyn-modal-header">
              <h2>{paramForm.id ? 'Ubah' : 'Tambah'} parameter</h2>
              <button type="button" className="lyn-icon-button" onClick={() => setParamForm(null)} aria-label="Tutup">
                ✕
              </button>
            </div>
            <div className="lyn-modal-body">
              <div className="lyn-form-grid">
                <label className="lyn-field">
                  <span>Kode</span>
                  <input required maxLength={30} value={paramForm.form.code} onChange={e => setP({ code: e.target.value.toUpperCase() })} />
                </label>
                <label className="lyn-field">
                  <span>Nama</span>
                  <input required maxLength={150} value={paramForm.form.name} onChange={e => setP({ name: e.target.value })} />
                </label>
                <label className="lyn-field">
                  <span>Satuan</span>
                  <input maxLength={30} value={paramForm.form.satuan ?? ''} onChange={e => setP({ satuan: e.target.value })} placeholder="ekor, %, pokok" />
                </label>
                {paramForm.form.tipeData === 'OPTION' && (
                  <label className="lyn-field sns-wide">
                    <span>Pilihan (pisahkan dengan koma)</span>
                    <input required value={paramForm.form.opsi ?? ''} onChange={e => setP({ opsi: e.target.value })} />
                  </label>
                )}
                <div className="sns-checks">
                  <label className="sns-inline-check">
                    <input type="checkbox" checked={paramForm.form.wajib} onChange={e => setP({ wajib: e.target.checked })} /> Wajib diisi
                  </label>
                  <label className="sns-inline-check">
                    <input
                      type="checkbox"
                      disabled={paramForm.form.tipeData !== 'NUMBER'}
                      checked={paramForm.form.isIndikator}
                      onChange={e => setP({ isIndikator: e.target.checked })}
                    />{' '}
                    Indikator tingkat serangan
                  </label>
                  <label className="sns-inline-check">
                    <input type="checkbox" checked={paramForm.form.active} onChange={e => setP({ active: e.target.checked })} /> Aktif
                  </label>
                </div>
                {paramForm.form.isIndikator && (
                  <>
                    <label className="lyn-field">
                      <span>Ambang sedang (≥)</span>
                      <input type="number" step="any" value={paramForm.form.ambangSedang} onChange={e => setP({ ambangSedang: e.target.value })} />
                    </label>
                    <label className="lyn-field">
                      <span>Ambang berat (≥)</span>
                      <input type="number" step="any" value={paramForm.form.ambangBerat} onChange={e => setP({ ambangBerat: e.target.value })} />
                    </label>
                  </>
                )}
              </div>
              {error && <p className="error">{error}</p>}
              <div className="lyn-form-actions">
                <button type="button" className="lyn-button ghost" onClick={() => setParamForm(null)}>
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
