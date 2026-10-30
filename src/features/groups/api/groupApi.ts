import { http } from '../../../api/http'
import type {
  Group,
  GroupMenuAccess,
  GroupScope,
  SaveGroupRequest,
} from '../types/group'

export async function getGroups(): Promise<Group[]> {
  return (await http.get<Group[]>('/utility/groups')).data
}

export async function getGroup(id: number): Promise<Group> {
  return (await http.get<Group>(`/utility/groups/${id}`)).data
}

export async function createGroup(data: SaveGroupRequest): Promise<Group> {
  return (await http.post<Group>('/utility/groups', data)).data
}

export async function updateGroup(id: number, data: SaveGroupRequest): Promise<Group> {
  return (await http.put<Group>(`/utility/groups/${id}`, data)).data
}

export async function disableGroup(id: number): Promise<void> {
  await http.delete(`/utility/groups/${id}`)
}

export async function getGroupMenuAccess(groupId: number): Promise<GroupMenuAccess[]> {
  return (await http.get<GroupMenuAccess[]>(`/utility/groups/${groupId}/menus`)).data
}

export async function saveGroupMenuAccess(
  groupId: number,
  permissions: GroupMenuAccess[],
): Promise<void> {
  await http.put(`/utility/groups/${groupId}/menus`, {
    permissions: permissions.map(x => ({
      menuId: x.menuId,
      canView: x.canView,
      canRead: x.canRead,
      canCreate: x.canCreate,
      canUpdate: x.canUpdate,
      canDelete: x.canDelete,
      canApprove: x.canApprove,
      canExport: x.canExport,
    })),
  })
}

export type ScopeKind = 'companies' | 'departments' | 'item-sub-categories'

export type PKBPayrollCompany = {
  id: string
  code: string
  name: string
  shortName: string
}

export type PKBPayrollDepartment = {
  id: string
  code: string
  name: string
  companyId: string
  shortName: string
}

export async function getPKBPayrollCompanies(): Promise<PKBPayrollCompany[]> {
  return (
    await http.get<PKBPayrollCompany[]>('/utility/pkb-payroll/companies')
  ).data
}

export async function getPKBPayrollDepartments(): Promise<PKBPayrollDepartment[]> {
  return (
    await http.get<PKBPayrollDepartment[]>('/utility/pkb-payroll/departments')
  ).data
}

export async function getGroupScope(
  groupId: number,
  kind: ScopeKind,
): Promise<GroupScope> {
  return (
    await http.get<GroupScope>(`/utility/groups/${groupId}/${kind}`)
  ).data
}

export async function saveGroupScope(
  groupId: number,
  kind: ScopeKind,
  value: GroupScope,
): Promise<void> {
  await http.put(`/utility/groups/${groupId}/${kind}`, {
    all: value.all,
    selectedIds: value.selectedIds,
  })
}
