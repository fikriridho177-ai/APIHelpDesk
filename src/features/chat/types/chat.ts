export interface ChatGroupSummary {
  id: number
  name: string
  description?: string | null
  memberCount: number
  unreadCount: number
  lastMessage?: string | null
  lastMessageAt?: string | null
}

export interface ChatRole {
  id: number
  name: string
  color: string
  permissions: number
  position: number
  isDefault: boolean
  memberCount: number
}

export interface ChatMember {
  userId: number
  userName: string
  name: string
  roleId: number
  isOwner: boolean
  online: boolean
}

export interface ChatGroupDetail {
  id: number
  name: string
  description?: string | null
  ownerUserId: number
  isOwner: boolean
  myPermissions: number
  myPosition: number
  roles: ChatRole[]
  members: ChatMember[]
}

export interface ChatMessage {
  id: number
  groupId: number
  senderUserId: number
  senderName: string
  content: string
  createdAt: string
  editedAt?: string | null
  isDeleted: boolean
}

export interface ChatUserOption {
  id: number
  userId: string
  name: string
}

export interface SaveChatRoleRequest {
  name: string
  color: string
  permissions: number
  position: number
}
