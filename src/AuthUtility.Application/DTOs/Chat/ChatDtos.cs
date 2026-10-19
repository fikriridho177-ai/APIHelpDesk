namespace AuthUtility.Application.DTOs.Chat;

/* ---------- Response ---------- */

public record ChatGroupSummaryDto(
    int Id,
    string Name,
    string? Description,
    int MemberCount,
    int UnreadCount,
    string? LastMessage,
    DateTime? LastMessageAt
);

public record ChatRoleDto(
    int Id,
    string Name,
    string Color,
    long Permissions,
    int Position,
    bool IsDefault,
    int MemberCount
);

public record ChatMemberDto(
    int UserId,
    string UserName,
    string Name,
    int RoleId,
    bool IsOwner,
    bool Online
);

public record ChatGroupDetailDto(
    int Id,
    string Name,
    string? Description,
    int OwnerUserId,
    bool IsOwner,
    long MyPermissions,
    int MyPosition,
    List<ChatRoleDto> Roles,
    List<ChatMemberDto> Members
);

public record ChatMessageDto(
    long Id,
    int GroupId,
    int SenderUserId,
    string SenderName,
    string Content,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsDeleted
);

public record ChatUserOptionDto(int Id, string UserId, string Name);

/* ---------- Request ---------- */

public class SaveChatGroupRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class ChatMessageRequest
{
    public string Content { get; set; } = "";
}

public class MarkChatReadRequest
{
    public long MessageId { get; set; }
}

public class AddChatMembersRequest
{
    public List<int> UserIds { get; set; } = new();

    // Kosong = pakai role default grup.
    public int? RoleId { get; set; }
}

public class UpdateChatMemberRequest
{
    public int RoleId { get; set; }
}

public class SaveChatRoleRequest
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#64748b";
    public long Permissions { get; set; }
    public int Position { get; set; } = 5;
}
