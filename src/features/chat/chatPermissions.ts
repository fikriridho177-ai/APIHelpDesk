// Harus sama dengan enum ChatPermission di backend (AuthUtility.Domain/Entities/ChatEntities.cs).
export const ChatPermission = {
  SendMessages: 1,
  ManageMessages: 2,
  ManageMembers: 4,
  ManageRoles: 8,
  ManageGroup: 16,
  Administrator: 32,
} as const

export const CHAT_PERMISSION_OPTIONS = [
  { value: ChatPermission.SendMessages, label: 'Kirim pesan', hint: 'Boleh menulis pesan di grup.' },
  { value: ChatPermission.ManageMessages, label: 'Kelola pesan', hint: 'Boleh menghapus pesan anggota lain.' },
  { value: ChatPermission.ManageMembers, label: 'Kelola anggota', hint: 'Tambah, keluarkan, dan ganti role anggota.' },
  { value: ChatPermission.ManageRoles, label: 'Kelola role', hint: 'Buat, ubah, dan hapus role di bawah posisinya.' },
  { value: ChatPermission.ManageGroup, label: 'Kelola grup', hint: 'Ubah nama dan deskripsi grup.' },
  { value: ChatPermission.Administrator, label: 'Administrator', hint: 'Semua permission di atas.' },
]

export function hasChatPermission(myPermissions: number, permission: number) {
  return (myPermissions & ChatPermission.Administrator) !== 0 || (myPermissions & permission) !== 0
}
