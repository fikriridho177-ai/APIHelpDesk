import { http } from '../../../api/http'

import type {
  ChatGroupDetail,
  ChatGroupSummary,
  ChatMessage,
  ChatUserOption,
  SaveChatRoleRequest,
} from '../types/chat'

/* ---------- Grup ---------- */

export async function getMyChatGroups(): Promise<ChatGroupSummary[]> {
  return (await http.get<ChatGroupSummary[]>('/chat/groups')).data
}

export async function getChatGroup(groupId: number): Promise<ChatGroupDetail> {
  return (await http.get<ChatGroupDetail>(`/chat/groups/${groupId}`)).data
}

export async function createChatGroup(name: string, description: string): Promise<ChatGroupDetail> {
  return (await http.post<ChatGroupDetail>('/chat/groups', { name, description })).data
}

export async function updateChatGroup(groupId: number, name: string, description: string): Promise<void> {
  await http.put(`/chat/groups/${groupId}`, { name, description })
}

export async function deleteChatGroup(groupId: number): Promise<void> {
  await http.delete(`/chat/groups/${groupId}`)
}

/* ---------- Pesan ---------- */

export async function getChatMessages(groupId: number, beforeId?: number): Promise<ChatMessage[]> {
  return (await http.get<ChatMessage[]>(`/chat/groups/${groupId}/messages`, { params: { beforeId } })).data
}

export async function sendChatMessage(groupId: number, content: string): Promise<ChatMessage> {
  return (await http.post<ChatMessage>(`/chat/groups/${groupId}/messages`, { content })).data
}

export async function editChatMessage(messageId: number, content: string): Promise<ChatMessage> {
  return (await http.put<ChatMessage>(`/chat/messages/${messageId}`, { content })).data
}

export async function deleteChatMessage(messageId: number): Promise<void> {
  await http.delete(`/chat/messages/${messageId}`)
}

export async function markChatRead(groupId: number, messageId: number): Promise<void> {
  await http.post(`/chat/groups/${groupId}/read`, { messageId })
}

/* ---------- Anggota ---------- */

export async function searchChatUsers(search: string): Promise<ChatUserOption[]> {
  return (await http.get<ChatUserOption[]>('/chat/users', { params: { search } })).data
}

export async function addChatMembers(groupId: number, userIds: number[], roleId?: number): Promise<void> {
  await http.post(`/chat/groups/${groupId}/members`, { userIds, roleId })
}

export async function updateChatMemberRole(groupId: number, userId: number, roleId: number): Promise<void> {
  await http.put(`/chat/groups/${groupId}/members/${userId}`, { roleId })
}

// Dipakai juga untuk keluar dari grup (userId = diri sendiri).
export async function removeChatMember(groupId: number, userId: number): Promise<void> {
  await http.delete(`/chat/groups/${groupId}/members/${userId}`)
}

/* ---------- Role ---------- */

export async function createChatRole(groupId: number, role: SaveChatRoleRequest): Promise<void> {
  await http.post(`/chat/groups/${groupId}/roles`, role)
}

export async function updateChatRole(groupId: number, roleId: number, role: SaveChatRoleRequest): Promise<void> {
  await http.put(`/chat/groups/${groupId}/roles/${roleId}`, role)
}

export async function deleteChatRole(groupId: number, roleId: number): Promise<void> {
  await http.delete(`/chat/groups/${groupId}/roles/${roleId}`)
}

/* ---------- Helper ---------- */

export function getChatErrorMessage(error: any, fallback: string) {
  return error?.response?.data?.message || fallback
}
