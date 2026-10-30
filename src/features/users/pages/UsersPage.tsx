import { FormEvent, useEffect, useMemo, useState } from 'react'

import { createUser, disableUser, getPKBPayrollEmployees, getUsers, updateUser } from '../api/userApi'
import {
  getGroups,
  getPKBPayrollCompanies,
  getPKBPayrollDepartments,
  type PKBPayrollCompany,
  type PKBPayrollDepartment,
} from '../../groups/api/groupApi'
import { getMyMenuPermission, type CurrentMenuPermission } from '../../security/api/securityApi'
import type { CreateUserRequest, PKBPayrollEmployee, UpdateUserRequest, User } from '../types/user'
import type { Group } from '../../groups/types/group'

/* =========================================================
   TIPE & NILAI AWAL
========================================================= */

type UserForm = {
  userId: string
  name: string
  email: string
  phone: string
  groupId: number
  password: string
  nik: string
  regNo: string
  deptId: string
  companyId: number | null
}

type UserFilter = {
  userId: string
  name: string
  email: string
  phone: string
  group: string
}

const initialForm: UserForm = {
  userId: '',
  name: '',
  email: '',
  phone: '',
  groupId: 0,
  password: 'User@123',
  nik: '',
  regNo: '',
  deptId: '',
  companyId: null,
}

const initialFilter: UserFilter = {
  userId: '',
  name: '',
  email: '',
  phone: '',
  group: '',
}

/* =========================================================
   HELPER
========================================================= */

// Ubah error dari backend (validasi / message / title) menjadi teks untuk alert.
function getErrorMessage(error: any) {
  const data = error?.response?.data

  if (data?.errors) {
    return Object.entries(data.errors)
      .flatMap(([field, value]) =>
        (Array.isArray(value) ? value : [String(value)]).map(message => `${field}: ${message}`),
      )
      .join('\n')
  }

  return data?.message || data?.title || 'Gagal menyimpan user.'
}

/* =========================================================
   HALAMAN
========================================================= */

export default function UsersPage() {
  const [data, setData] = useState<User[]>([])
  const [groups, setGroups] = useState<Group[]>([])
  const [payrollEmployees, setPayrollEmployees] = useState<PKBPayrollEmployee[]>([])
  const [payrollCompanies, setPayrollCompanies] = useState<PKBPayrollCompany[]>([])
  const [payrollDepartments, setPayrollDepartments] = useState<PKBPayrollDepartment[]>([])

  const [form, setForm] = useState<UserForm>(initialForm)
  const [filter, setFilter] = useState<UserFilter>(initialFilter)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [loading, setLoading] = useState(false)

  const [employeeSearch, setEmployeeSearch] = useState('')
  const [employeeDropdownOpen, setEmployeeDropdownOpen] = useState(false)

  const [permission, setPermission] = useState<CurrentMenuPermission | null>(null)
  const [permissionLoading, setPermissionLoading] = useState(true)

  const isEditing = editingId !== null

  /* ---------- Data karyawan / perusahaan / departemen terpilih ---------- */

  const matchingEmployees = useMemo(() => {
    const keyword = employeeSearch.trim().toLowerCase()

    return payrollEmployees
      .filter(employee => !keyword || `${employee.name} ${employee.nik}`.toLowerCase().includes(keyword))
      .slice(0, 100)
  }, [payrollEmployees, employeeSearch])

  const selectedEmployee = payrollEmployees.find(employee => String(employee.regNo ?? '') === form.regNo)

  // Kode perusahaan PKBPayroll bisa diawali nol ("04"), sedangkan companyId berupa angka (4).
  const isSameCompany = (code: string) =>
    form.companyId != null && code.trim() !== '' && Number(code) === Number(form.companyId)

  const selectedCompany = payrollCompanies.find(company => isSameCompany(company.id))

  const selectedDepartment =
    payrollDepartments.find(department => department.id === form.deptId && isSameCompany(department.companyId)) ??
    payrollDepartments.find(department => department.id === form.deptId)

  /* ---------- Load data ---------- */

  async function load() {
    const [users, groupData, employeeData, companyData, departmentData] = await Promise.all([
      getUsers(),
      getGroups(),
      getPKBPayrollEmployees(),
      getPKBPayrollCompanies(),
      getPKBPayrollDepartments(),
    ])

    setData(users)
    setGroups(groupData)
    setPayrollEmployees(employeeData)
    setPayrollCompanies(companyData)
    setPayrollDepartments(departmentData)

    setForm(current => ({
      ...current,
      groupId: current.groupId || groupData[0]?.id || 0,
    }))
  }

  useEffect(() => {
    async function init() {
      try {
        setPermissionLoading(true)

        const menuPermission = await getMyMenuPermission('MASTER_USER')
        setPermission(menuPermission)

        if (menuPermission.canRead) {
          await load()
        }
      } catch (error) {
        console.error('Gagal mengambil permission MASTER_USER:', error)
      } finally {
        setPermissionLoading(false)
      }
    }

    init()
  }, [])

  /* ---------- Filter tabel (hanya user aktif) ---------- */

  const activeUsers = useMemo(() => {
    const contains = (value: string | undefined, keyword: string) =>
      (value ?? '').toLowerCase().includes(keyword.toLowerCase())

    return data
      .filter(user => user.active)
      .filter(
        user =>
          contains(user.userId, filter.userId) &&
          contains(user.name, filter.name) &&
          contains(user.email, filter.email) &&
          contains(user.phone, filter.phone) &&
          contains(user.groupName, filter.group),
      )
  }, [data, filter])

  /* ---------- Aksi form ---------- */

  function resetForm() {
    setEditingId(null)
    setForm({ ...initialForm, groupId: groups[0]?.id ?? 0 })
  }

  function handleAdd() {
    if (!permission?.canCreate) return

    resetForm()
    setShowForm(true)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  function handleEdit(user: User) {
    if (!permission?.canUpdate) return

    setEditingId(user.id)
    setForm({
      userId: user.userId ?? '',
      name: user.name ?? '',
      email: user.email ?? '',
      phone: user.phone ?? '',
      groupId: user.groupId ?? 0,
      password: '', // kosong = password lama tidak diubah
      nik: user.nik ?? '',
      regNo: user.regNo != null ? String(user.regNo) : '',
      deptId: user.deptId ?? '',
      companyId: user.companyId ?? null,
    })

    setShowPassword(false)
    setShowForm(true)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  function handleCancel() {
    resetForm()
    setShowForm(false)
  }

  function handleSelectEmployee(employee: PKBPayrollEmployee) {
    const firstName = (employee.name.trim().split(/\s+/)[0] ?? '').toLowerCase()

    setForm(current => ({
      ...current,
      regNo: employee.regNo != null ? String(employee.regNo) : '',
      nik: employee.nik,
      name: employee.name,
      deptId: employee.deptId,
      companyId: employee.companyId,
      // User ID & password awal hanya diisi otomatis saat tambah user.
      userId: isEditing ? current.userId : firstName,
      password: isEditing ? current.password : `${firstName}@123`,
    }))

    setEmployeeDropdownOpen(false)
    setEmployeeSearch('')
  }

  function handleUserIdChange(userId: string) {
    setForm(current => ({
      ...current,
      userId,
      password: isEditing ? current.password : `${userId}@123`,
    }))
  }

  /* ---------- Simpan (tambah / update) ---------- */

  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

    if (!form.name.trim()) {
      alert('Nama lengkap wajib diisi.')
      return
    }

    if (!form.groupId || Number(form.groupId) <= 0) {
      alert('Group / Role wajib dipilih.')
      return
    }

    // Field yang sama untuk tambah maupun update.
    const commonPayload = {
      name: form.name.trim(),
      email: form.email.trim(),
      phone: form.phone.trim(),
      nik: form.nik.trim() || undefined,
      regNo: form.regNo.trim() ? Number(form.regNo.trim()) : undefined,
      deptId: form.deptId.trim() || undefined,
      companyId: form.companyId ?? undefined,
      groupId: Number(form.groupId),
      active: true,
    }

    try {
      setLoading(true)

      if (editingId !== null) {
        if (!permission?.canUpdate) {
          alert('Anda tidak memiliki akses Update pada menu Master User.')
          return
        }

        const payload: UpdateUserRequest = {
          ...commonPayload,
          userId: form.userId.trim() || undefined,
        }

        // Password hanya dikirim jika diisi; kosong = password lama tetap.
        if (form.password.trim()) {
          payload.password = form.password.trim()
        }

        await updateUser(editingId, payload)
      } else {
        if (!permission?.canCreate) {
          alert('Anda tidak memiliki akses Create pada menu Master User.')
          return
        }

        if (!form.regNo.trim()) {
          alert('Karyawan PKBPayroll wajib dipilih.')
          return
        }

        if (!form.userId.trim()) {
          alert('User ID wajib diisi.')
          return
        }

        if (!form.password.trim()) {
          alert('Password wajib diisi.')
          return
        }

        const payload: CreateUserRequest = {
          ...commonPayload,
          userId: form.userId.trim(),
          password: form.password.trim(),
        }

        await createUser(payload)
      }

      handleCancel()
      await load()
    } catch (error) {
      console.error('Gagal menyimpan user:', error)
      alert(getErrorMessage(error))
    } finally {
      setLoading(false)
    }
  }

  /* ---------- Nonaktifkan user ---------- */

  async function handleDisable(user: User) {
    if (!permission?.canDelete) return
    if (!window.confirm(`Nonaktifkan user "${user.userId}"?`)) return

    await disableUser(user.id)
    await load()
  }

  /* ---------- Render ---------- */

  const filterInput = (field: keyof UserFilter) => (
    <input value={filter[field]} onChange={e => setFilter({ ...filter, [field]: e.target.value })} />
  )

  return (
    <section className="user-page">
      {!permissionLoading && permission && !permission.canRead && (
        <div className="permission-info">
          Anda memiliki akses membuka menu, tetapi tidak memiliki permission Read untuk melihat daftar pengguna.
        </div>
      )}

      <div className="page-header">
        <div>
          <h1>Daftar Pengguna</h1>
          <p className="text-muted">Kelola pengguna aplikasi.</p>
        </div>
      </div>

      <div className="card">
        {!showForm && permission?.canCreate && (
          <div className="user-toolbar">
            <button type="button" onClick={handleAdd}>
              + Tambah User
            </button>
          </div>
        )}

        {/* FORM TAMBAH / EDIT */}
        {showForm && (
          <form className="user-form" onSubmit={save}>
            <div className="form-title">{isEditing ? 'Edit User' : 'Tambah User'}</div>

            <div className="grid">
              {/* Karyawan PKBPayroll: cari berdasarkan nama / NIK */}
              <div className="user-payroll-picker">
                <label htmlFor="payroll-employee-search">Karyawan PKBPayroll</label>
                <input
                  id="payroll-employee-search"
                  value={
                    employeeDropdownOpen || !selectedEmployee
                      ? employeeSearch
                      : `${selectedEmployee.name} - NIK ${selectedEmployee.nik}`
                  }
                  placeholder="Cari nama atau NIK karyawan..."
                  autoComplete="off"
                  onFocus={() => {
                    setEmployeeSearch('')
                    setEmployeeDropdownOpen(true)
                  }}
                  onChange={e => {
                    setEmployeeSearch(e.target.value)
                    setEmployeeDropdownOpen(true)
                  }}
                  onKeyDown={e => {
                    if (e.key === 'Escape') setEmployeeDropdownOpen(false)
                  }}
                />

                {employeeDropdownOpen && (
                  <div className="user-payroll-options">
                    {matchingEmployees.map(employee => (
                      <button type="button" key={employee.regNo} onClick={() => handleSelectEmployee(employee)}>
                        {employee.name} - NIK {employee.nik || '-'}
                      </button>
                    ))}
                    {matchingEmployees.length === 0 && <p>Tidak ada karyawan ditemukan.</p>}
                  </div>
                )}
              </div>

              <label>
                User ID
                <input value={form.userId} onChange={e => handleUserIdChange(e.target.value)} />
              </label>

              <label>
                Nama Lengkap
                <input required value={form.name} readOnly placeholder="Otomatis dari PKBPayroll" />
              </label>

              <label>
                NIK
                <input value={form.nik} readOnly placeholder="Otomatis dari PKBPayroll" />
              </label>

              <label>
                RegNo
                <input value={form.regNo} readOnly placeholder="Otomatis dari PKBPayroll" />
              </label>

              <label>
                Departemen
                <input value={selectedDepartment?.name ?? form.deptId} readOnly placeholder="Departemen PKBPayroll" />
              </label>

              <label>
                Perusahaan
                <input
                  value={selectedCompany?.name ?? String(form.companyId ?? '')}
                  readOnly
                  placeholder="Perusahaan PKBPayroll"
                />
              </label>

              <label>
                Email
                <input type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
              </label>

              <label>
                No HP
                <input value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} />
              </label>

              <label>
                Group / Role
                <select value={form.groupId} onChange={e => setForm({ ...form, groupId: Number(e.target.value) })}>
                  {groups.map(group => (
                    <option key={group.id} value={group.id}>
                      {group.name}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                {isEditing ? 'Password Baru' : 'Password Awal'}
                <div className="password-input-wrapper">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    required={!isEditing}
                    placeholder={isEditing ? 'Kosongkan jika tidak ingin mengubah password' : ''}
                    value={form.password}
                    onChange={e => setForm({ ...form, password: e.target.value })}
                  />
                  <button
                    type="button"
                    className="password-eye-button"
                    onClick={() => setShowPassword(value => !value)}
                    title={showPassword ? 'Sembunyikan password' : 'Lihat password'}
                  >
                    {showPassword ? '🙈' : '👁'}
                  </button>
                </div>
              </label>
            </div>

            <div className="form-actions">
              <button type="button" className="secondary" onClick={handleCancel}>
                Batal
              </button>
              <button type="submit" disabled={loading}>
                {loading ? 'Menyimpan...' : isEditing ? 'Update User' : 'Simpan User'}
              </button>
            </div>
          </form>
        )}

        {/* TABEL USER */}
        <div className="table-container">
          <table className="user-table">
            <thead>
              <tr>
                <th className="aksi-column">AKSI</th>
                <th>NAMA PENGGUNA</th>
                <th>NAMA LENGKAP</th>
                <th>EMAIL</th>
                <th>NO HP</th>
                <th>TERAKHIR LOGIN</th>
                <th>ROLE</th>
                <th>AKTIF</th>
              </tr>

              {/* Baris pencarian, urutan kolom sama dengan header di atas */}
              <tr className="filter-row">
                <th />
                <th>{filterInput('userId')}</th>
                <th>{filterInput('name')}</th>
                <th>{filterInput('email')}</th>
                <th>{filterInput('phone')}</th>
                <th />
                <th>{filterInput('group')}</th>
                <th />
              </tr>
            </thead>

            <tbody>
              {activeUsers.length === 0 ? (
                <tr>
                  <td colSpan={8} className="empty-table">
                    Tidak ada user aktif.
                  </td>
                </tr>
              ) : (
                activeUsers.map(user => (
                  <tr key={user.id}>
                    <td>
                      <div className="action-buttons">
                        {permission?.canUpdate && (
                          <button
                            type="button"
                            className="icon-button edit"
                            title="Edit User"
                            onClick={() => handleEdit(user)}
                          >
                            ✎
                          </button>
                        )}
                        {permission?.canDelete && (
                          <button
                            type="button"
                            className="icon-button danger"
                            title="Nonaktifkan User"
                            onClick={() => handleDisable(user)}
                          >
                            🗑
                          </button>
                        )}
                      </div>
                    </td>
                    <td>{user.userId}</td>
                    <td>{user.name}</td>
                    <td>{user.email || '-'}</td>
                    <td>{user.phone || '-'}</td>
                    <td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString('id-ID') : '-'}</td>
                    <td>{user.groupName}</td>
                    <td>Ya</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  )
}
