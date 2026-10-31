import { http } from '../../../api/http'

export type CurrentMenuPermission = {
  menuCode: string
  isSuperAdmin: boolean
  canView: boolean
  canRead: boolean
  canCreate: boolean
  canUpdate: boolean
  canDelete: boolean
  canApprove: boolean
  canExport: boolean
}

export const getSecurity = async (groupId: number) =>
  (await http.get(`/utility/security/groups/${groupId}`)).data

export const saveSecurity = async (groupId: number, data: any[]) =>
  (await http.put(`/utility/security/groups/${groupId}`, data)).data

export async function getMyMenuPermission(menuCode: string): Promise<CurrentMenuPermission> {
  return (await http.get<CurrentMenuPermission>(`/utility/security/me/${encodeURIComponent(menuCode)}`)).data
}
