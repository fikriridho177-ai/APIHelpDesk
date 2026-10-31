import { FormEvent, useEffect, useState } from 'react'

import { DashboardIcon } from '../../dashboard/components/DashboardIcon'
import { createLayananRequest, getLayananErrorMessage, getRequesterProfile } from '../api/layananApi'
import { useDepartmentNames } from '../hooks/useLayanan'
import { COMPANY_OPTIONS, PRIORITY_CONFIG, isOtherService } from '../layananConfig'
import type { LynJenis, LynPriority, LynRequestDetail, LynServiceNode } from '../types/layanan'

type Props = {
  // Divisi (mis. GIS) bila jenisOptions diisi, atau sub-layanan model lama.
  service: LynServiceNode
  // Pilihan Jenis Permintaan dari Master Jenis Permintaan (divisi ini).
  jenisOptions?: LynJenis[]
  onCreated: (request: LynRequestDetail) => void
}

const emptyRequest = {
  jenisId: null as number | null,
  otherType: '',
  description: '',
  location: '',
  neededDate: '',
  priority: 'NORMAL' as LynPriority,
}

const emptyRequester = {
  requesterName: '',
  requesterPosition: '',
  requesterDepartment: '',
  companies: [] as string[],
  otherCompanyChecked: false,
  otherCompany: '',
  requesterPhone: '',
  requesterEmail: '',
}

// Langkah "Isi Formulir" pada flowchart. Isian mengikuti form permintaan (Google Form GIS).
// Jenis Permintaan dipilih dari Master Jenis Permintaan divisi ini; data pemohon diisi awal dari akun login.
export default function RequestForm({ service, jenisOptions, onCreated }: Props) {
  const [form, setForm] = useState(emptyRequest)
  const [requester, setRequester] = useState(emptyRequester)
  const [saving, setSaving] = useState(false)
  const departmentNames = useDepartmentNames()
  const [profileDeptId, setProfileDeptId] = useState<string | null>(null)

  const useJenis = jenisOptions !== undefined
  const selectedJenis = jenisOptions?.find(j => j.id === form.jenisId)
  const isOther = useJenis ? !!selectedJenis?.isOther : isOtherService(service.code)

  useEffect(() => {
    getRequesterProfile()
      .then(profile => {
        setRequester(current => ({
          ...current,
          requesterName: current.requesterName || profile.name,
          requesterEmail: current.requesterEmail || profile.email,
          requesterPhone: current.requesterPhone || (profile.phone ?? ''),
        }))
        setProfileDeptId(profile.deptId?.trim() || null)
      })
      .catch(() => undefined)
  }, [])

  // Nama departemen baru tersedia setelah daftar departemen dimuat.
  useEffect(() => {
    const name = profileDeptId ? departmentNames.get(profileDeptId) : undefined
    if (name) setRequester(current => (current.requesterDepartment ? current : { ...current, requesterDepartment: name }))
  }, [profileDeptId, departmentNames])

  function toggleCompany(company: string) {
    setRequester(current => ({
      ...current,
      companies: current.companies.includes(company)
        ? current.companies.filter(x => x !== company)
        : [...current.companies, company],
    }))
  }

  async function submit(e: FormEvent) {
    e.preventDefault()

    if (useJenis && !selectedJenis) {
      alert('Pilih jenis permintaan.')
      return
    }

    const otherCompany = requester.otherCompanyChecked ? requester.otherCompany.trim() : ''
    if (requester.companies.length === 0 && !otherCompany) {
      alert('Pilih minimal satu perusahaan.')
      return
    }

    try {
      setSaving(true)
      const created = await createLayananRequest({
        jenisId: useJenis ? selectedJenis?.id : undefined,
        menuCode: useJenis ? undefined : service.code,
        otherType: isOther ? form.otherType : undefined,
        requesterName: requester.requesterName,
        requesterPosition: requester.requesterPosition,
        requesterDepartment: requester.requesterDepartment,
        companies: requester.companies,
        otherCompany: otherCompany || undefined,
        requesterPhone: requester.requesterPhone,
        requesterEmail: requester.requesterEmail,
        description: form.description,
        location: form.location,
        neededDate: form.neededDate,
        priority: form.priority,
      })
      // Data pemohon dipertahankan supaya pengajuan berikutnya tidak perlu diisi ulang.
      setForm(emptyRequest)
      onCreated(created)
    } catch (error) {
      alert(getLayananErrorMessage(error, 'Gagal mengirim pengajuan.'))
    } finally {
      setSaving(false)
    }
  }

  const setR = (patch: Partial<typeof requester>) => setRequester(current => ({ ...current, ...patch }))
  const setF = (patch: Partial<typeof form>) => setForm(current => ({ ...current, ...patch }))

  return (
    <form className="lyn-card lyn-form" onSubmit={submit}>
      <div className="lyn-card-header">
        <div>
          <h3>Formulir Pengajuan</h3>
          <p>Isi kebutuhan Anda, admin {service.name} akan meninjau dan menjadwalkannya.</p>
        </div>
      </div>

      {/* DATA PEMOHON */}
      <h4 className="lyn-form-section">Data Pemohon</h4>

      <label className="lyn-field">
        <span>Nama Pemohon *</span>
        <input required maxLength={150} value={requester.requesterName} onChange={e => setR({ requesterName: e.target.value })} />
      </label>

      <label className="lyn-field">
        <span>Jabatan Pemohon *</span>
        <input required maxLength={100} value={requester.requesterPosition} onChange={e => setR({ requesterPosition: e.target.value })} />
      </label>

      <label className="lyn-field">
        <span>Departemen *</span>
        <input required maxLength={150} value={requester.requesterDepartment} onChange={e => setR({ requesterDepartment: e.target.value })} />
      </label>

      <fieldset className="lyn-field lyn-checks">
        <span>Perusahaan *</span>
        {COMPANY_OPTIONS.map(company => (
          <label key={company} className="lyn-check">
            <input type="checkbox" checked={requester.companies.includes(company)} onChange={() => toggleCompany(company)} />
            {company}
          </label>
        ))}
        <label className="lyn-check">
          <input
            type="checkbox"
            checked={requester.otherCompanyChecked}
            onChange={e => setR({ otherCompanyChecked: e.target.checked })}
          />
          Yang lain:
          <input
            className="lyn-check-other"
            maxLength={150}
            value={requester.otherCompany}
            required={requester.otherCompanyChecked}
            onChange={e => setR({ otherCompany: e.target.value, otherCompanyChecked: e.target.value.trim() !== '' })}
          />
        </label>
      </fieldset>

      <div className="lyn-form-grid">
        <label className="lyn-field">
          <span>Nomor HP/Whatsapp *</span>
          <input required type="tel" maxLength={50} value={requester.requesterPhone} onChange={e => setR({ requesterPhone: e.target.value })} />
        </label>
        <label className="lyn-field">
          <span>Email *</span>
          <input required type="email" maxLength={150} value={requester.requesterEmail} onChange={e => setR({ requesterEmail: e.target.value })} />
        </label>
      </div>

      {/* PERMINTAAN */}
      <h4 className="lyn-form-section">Permintaan</h4>

      {jenisOptions ? (
        <fieldset className="lyn-field lyn-jenis-picker">
          <span>Jenis Permintaan *</span>
          {jenisOptions.length === 0 ? (
            <p className="lyn-muted">Belum ada jenis permintaan aktif untuk divisi ini.</p>
          ) : (
            <div className="lyn-jenis-options">
              {jenisOptions.map(jenis => (
                <label key={jenis.id} className={form.jenisId === jenis.id ? 'lyn-jenis-option active' : 'lyn-jenis-option'}>
                  <input
                    type="radio"
                    name="jenisId"
                    checked={form.jenisId === jenis.id}
                    onChange={() => setF({ jenisId: jenis.id })}
                  />
                  <span>
                    <strong>{jenis.name}</strong>
                    {jenis.description && <small>{jenis.description}</small>}
                  </span>
                </label>
              ))}
            </div>
          )}
          {isOther && (
            <input
              required
              maxLength={150}
              value={form.otherType}
              onChange={e => setF({ otherType: e.target.value })}
              placeholder="Tuliskan jenis permintaan"
            />
          )}
        </fieldset>
      ) : (
        <label className="lyn-field">
          <span>Jenis Permintaan *</span>
          {isOther ? (
            <input
              required
              maxLength={150}
              value={form.otherType}
              onChange={e => setF({ otherType: e.target.value })}
              placeholder="Tuliskan jenis permintaan"
            />
          ) : (
            <input value={service.name} readOnly />
          )}
        </label>
      )}

      <label className="lyn-field">
        <span>Deskripsi Permintaan *</span>
        <textarea
          required
          rows={3}
          value={form.description}
          onChange={e => setF({ description: e.target.value })}
          placeholder="Contoh: Permintaan Peta Persil A12"
        />
      </label>

      <label className="lyn-field">
        <span>
          <DashboardIcon name="pin" /> Lokasi Pekerjaan (Divisi/Estate/Wilayah/Desa dll) *
        </span>
        <input
          required
          maxLength={200}
          value={form.location}
          onChange={e => setF({ location: e.target.value })}
          placeholder="Contoh: persil 17-01 Wilayah 2 RSUP"
        />
      </label>

      <div className="lyn-field">
        <span>Skala Prioritas *</span>
        <div className="lyn-priority-picker">
          {(Object.keys(PRIORITY_CONFIG) as LynPriority[]).map(priority => (
            <button
              key={priority}
              type="button"
              className={form.priority === priority ? `lyn-chip tone-${PRIORITY_CONFIG[priority].tone} active` : 'lyn-chip'}
              onClick={() => setF({ priority })}
            >
              {PRIORITY_CONFIG[priority].label}
            </button>
          ))}
        </div>
      </div>

      <label className="lyn-field">
        <span>
          <DashboardIcon name="calendar" /> Tanggal Deadline *
        </span>
        <input required type="date" value={form.neededDate} onChange={e => setF({ neededDate: e.target.value })} />
      </label>

      <div className="lyn-form-actions">
        <button type="submit" className="lyn-button primary" disabled={saving}>
          {saving ? 'Mengirim...' : 'Kirim Pengajuan'}
        </button>
      </div>
    </form>
  )
}
