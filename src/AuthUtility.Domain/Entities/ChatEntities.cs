namespace AuthUtility.Domain.Entities;

// Permission role di dalam grup chat (disimpan sebagai gabungan flag di ChatRole.Permissions).
[Flags]
public enum ChatPermission : long
{
    None = 0,
    SendMessages = 1,
    ManageMessages = 2,   // hapus pesan anggota lain
    ManageMembers = 4,    // tambah / keluarkan anggota, ganti role anggota
    ManageRoles = 8,      // buat / ubah / hapus role
    ManageGroup = 16,     // ubah nama & deskripsi grup
    Administrator = 32,   // semua permission

    All = SendMessages | ManageMessages | ManageMembers | ManageRoles | ManageGroup | Administrator
}

public class ChatGroup
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public int OwnerUserId { get; set; }

    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<ChatRole> Roles { get; set; } = new List<ChatRole>();

    public ICollection<ChatMember> Members { get; set; } = new List<ChatMember>();
}

public class ChatRole
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public string Name { get; set; } = "";

    public string Color { get; set; } = "#64748b";

    public ChatPermission Permissions { get; set; }

    // Makin besar = makin tinggi. Anggota hanya bisa mengatur role/anggota di bawah posisinya.
    public int Position { get; set; }

    // Role yang otomatis diberikan ke anggota baru.
    public bool IsDefault { get; set; }
}

public class ChatMember
{
    public int GroupId { get; set; }

    public int UserId { get; set; }

    public UtlUser? User { get; set; }

    public int RoleId { get; set; }

    public ChatRole? Role { get; set; }

    public long LastReadMessageId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class ChatMessage
{
    public long Id { get; set; }

    public int GroupId { get; set; }

    public int SenderUserId { get; set; }

    public UtlUser? Sender { get; set; }

    public string Content { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EditedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}
