import type { MenuType } from '../../menus/types/menu'

export interface Group {
  id: number
  code: string
  name: string
  description?: string | null
  isSuperAdmin: boolean
  allCompanies: boolean
  allDepartments: boolean
  allItemSubCategories: boolean
  active: boolean
  createdBy?: string | null
  createdAt: string
  updatedBy?: string | null
  updatedAt?: string | null
}

export interface SaveGroupRequest {
  code: string
  name: string
  description?: string | null
  isSuperAdmin: boolean
  active: boolean
}

export interface GroupMenuAccess {
  menuId: number
  parentId?: number | null
  code: string
  name: string
  menuType: MenuType
  sortOrder: number
  canView: boolean
  canRead: boolean
  canCreate: boolean
  canUpdate: boolean
  canDelete: boolean
  canApprove: boolean
  canExport: boolean
}

export type ScopeId = string | number

export interface ScopeOption {
  id: ScopeId
  code: string
  name: string
  extra?: string | null
}

export interface GroupScope {
  all: boolean
  selectedIds: ScopeId[]
  options: ScopeOption[]
}
