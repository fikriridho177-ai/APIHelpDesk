export interface User {
  id: number

  userId: string
  name: string

  email?: string
  phone?: string

  nik?: string
  regNo?: number
  deptId?: string
  companyId?: number

  groupId: number
  groupName: string

  active: boolean
  mustChangePassword: boolean

  lastLoginAt?: string

  createdBy?: string
  createdAt?: string

  updatedBy?: string
  updatedAt?: string
}

export interface CreateUserRequest {
  userId: string
  name: string
  email: string
  phone: string

  nik?: string
  regNo?: number
  deptId?: string
  companyId?: number

  groupId: number

  password: string
  active: boolean
}

export interface UpdateUserRequest {
  userId?: string
  name: string

  email: string
  phone: string

  nik?: string
  regNo?: number
  deptId?: string
  companyId?: number

  groupId: number

  active: boolean

  /*
   * Optional.
   * Kalau kosong/tidak dikirim,
   * password tidak diubah.
   */
  password?: string
}

export interface PKBPayrollEmployee {
  regNo: number | null
  name: string
  nik: string
  companyId: number | null
  deptId: string
}
