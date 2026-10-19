using System.Security.Claims;
using AuthUtility.Api.Chat;
using AuthUtility.Application.DTOs.Chat;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

// Live Chat.
// Akses halaman diatur menu LIVE_CHAT (View = buka chat, Create = buat grup baru).
// Di dalam grup, aksi diatur permission role (ChatPermission), mirip Discord:
//   - Owner grup selalu punya semua permission.
//   - Anggota hanya bisa mengatur anggota / role yang posisinya di bawah dirinya.
//   - Anggota hanya bisa memberikan permission yang ia miliki sendiri.
[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController(
    AppDbContext db,
    IPermissionService permissionService,
    IHubContext<ChatHub> hub,
    ChatPresenceTracker presence
) : ControllerBase
{
    private const string MenuCode = "LIVE_CHAT";
    private const int MaxMessageLength = 4000;
    private const int OwnerPosition = int.MaxValue;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /* =========================================================
       GRUP
    ========================================================= */

    // Daftar grup milik user + jumlah pesan belum dibaca.
    [HttpGet("groups")]
    public async Task<IActionResult> GetMyGroups()
    {
        if (!await CanOpenChat()) return Forbid();

        var me = CurrentUserId;

        var groups = await db.ChatMembers
            .Where(m => m.UserId == me)
            .Join(db.ChatGroups.Where(g => g.Active), m => m.GroupId, g => g.Id, (m, g) => new { Group = g, m.LastReadMessageId })
            .Select(x => new
            {
                x.Group.Id,
                x.Group.Name,
                x.Group.Description,
                MemberCount = db.ChatMembers.Count(c => c.GroupId == x.Group.Id),
                UnreadCount = db.ChatMessages.Count(c =>
                    c.GroupId == x.Group.Id &&
                    c.Id > x.LastReadMessageId &&
                    c.SenderUserId != me &&
                    c.DeletedAt == null),
                LastMessage = db.ChatMessages
                    .Where(c => c.GroupId == x.Group.Id && c.DeletedAt == null)
                    .OrderByDescending(c => c.Id)
                    .Select(c => new { c.Content, c.CreatedAt })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var result = groups
            .Select(x => new ChatGroupSummaryDto(
                x.Id, x.Name, x.Description, x.MemberCount, x.UnreadCount,
                x.LastMessage?.Content, x.LastMessage?.CreatedAt))
            .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
            .ThenBy(x => x.Name)
            .ToList();

        return Ok(result);
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup(SaveChatGroupRequest request)
    {
        if (!await permissionService.HasPermissionAsync(User, MenuCode, MenuPermission.Create))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Anda tidak memiliki akses membuat grup chat." });

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Nama grup wajib diisi." });

        var me = CurrentUserId;

        var adminRole = new ChatRole
        {
            Name = "Admin",
            Color = "#e11d48",
            Position = 10,
            Permissions = ChatPermission.SendMessages | ChatPermission.ManageMessages |
                          ChatPermission.ManageMembers | ChatPermission.ManageRoles | ChatPermission.ManageGroup
        };

        var memberRole = new ChatRole
        {
            Name = "Member",
            Color = "#64748b",
            Position = 1,
            IsDefault = true,
            Permissions = ChatPermission.SendMessages
        };

        var group = new ChatGroup
        {
            Name = name,
            Description = request.Description?.Trim(),
            OwnerUserId = me,
            Roles = { adminRole, memberRole }
        };

        await using var tx = await db.Database.BeginTransactionAsync();

        db.ChatGroups.Add(group);
        await db.SaveChangesAsync();

        db.ChatMembers.Add(new ChatMember { GroupId = group.Id, UserId = me, RoleId = adminRole.Id });
        await db.SaveChangesAsync();

        await tx.CommitAsync();

        await JoinHubGroup(me, group.Id);
        return Ok(await BuildGroupDetail(group.Id, me));
    }

    [HttpGet("groups/{groupId:int}")]
    public async Task<IActionResult> GetGroup(int groupId)
    {
        if (!await CanOpenChat()) return Forbid();

        var access = await GetAccess(groupId);
        if (access == null) return NotFound(new { message = "Grup tidak ditemukan atau Anda bukan anggota." });

        return Ok(await BuildGroupDetail(groupId, CurrentUserId));
    }

    [HttpPut("groups/{groupId:int}")]
    public async Task<IActionResult> UpdateGroup(int groupId, SaveChatGroupRequest request)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();
        if (!access.Has(ChatPermission.ManageGroup)) return NoAccess("mengubah grup");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Nama grup wajib diisi." });

        access.Group.Name = name;
        access.Group.Description = request.Description?.Trim();
        access.Group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await NotifyGroupChanged(groupId);
        return Ok(new { message = "Grup berhasil diubah." });
    }

    // Hanya owner. Grup tidak dihapus permanen, hanya dinonaktifkan.
    [HttpDelete("groups/{groupId:int}")]
    public async Task<IActionResult> DeleteGroup(int groupId)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();
        if (!access.IsOwner) return NoAccess("menghapus grup (hanya owner)");

        access.Group.Active = false;
        access.Group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await hub.Clients.Group(ChatHub.GroupName(groupId)).SendAsync("RemovedFromGroup", groupId);
        return Ok(new { message = "Grup berhasil dihapus." });
    }

    /* =========================================================
       PESAN
    ========================================================= */

    // Ambil pesan terbaru (atau pesan sebelum beforeId untuk "muat lebih lama").
    [HttpGet("groups/{groupId:int}/messages")]
    public async Task<IActionResult> GetMessages(int groupId, long? beforeId, int take = 50)
    {
        if (await GetAccess(groupId) == null) return NotFound();

        take = Math.Clamp(take, 1, 100);

        var query = db.ChatMessages.Where(x => x.GroupId == groupId);
        if (beforeId.HasValue) query = query.Where(x => x.Id < beforeId.Value);

        var messages = await query
            .OrderByDescending(x => x.Id)
            .Take(take)
            .Select(ToMessageDto)
            .ToListAsync();

        messages.Reverse(); // urut dari lama ke baru
        return Ok(messages);
    }

    [HttpPost("groups/{groupId:int}/messages")]
    public async Task<IActionResult> SendMessage(int groupId, ChatMessageRequest request)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();
        if (!access.Has(ChatPermission.SendMessages)) return NoAccess("mengirim pesan di grup ini");

        var content = request.Content.Trim();
        if (content.Length == 0) return BadRequest(new { message = "Pesan tidak boleh kosong." });
        if (content.Length > MaxMessageLength) return BadRequest(new { message = $"Pesan maksimal {MaxMessageLength} karakter." });

        var message = new ChatMessage { GroupId = groupId, SenderUserId = CurrentUserId, Content = content };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync();

        // Pesan sendiri dianggap sudah dibaca.
        access.Member.LastReadMessageId = message.Id;
        await db.SaveChangesAsync();

        var dto = await GetMessageDto(message.Id);
        await hub.Clients.Group(ChatHub.GroupName(groupId)).SendAsync("MessageCreated", dto);
        return Ok(dto);
    }

    // Edit hanya untuk pesan milik sendiri.
    [HttpPut("messages/{messageId:long}")]
    public async Task<IActionResult> EditMessage(long messageId, ChatMessageRequest request)
    {
        var message = await db.ChatMessages.FindAsync(messageId);
        if (message == null || message.DeletedAt != null) return NotFound();
        if (await GetAccess(message.GroupId) == null) return NotFound();
        if (message.SenderUserId != CurrentUserId) return NoAccess("mengedit pesan orang lain");

        var content = request.Content.Trim();
        if (content.Length == 0) return BadRequest(new { message = "Pesan tidak boleh kosong." });
        if (content.Length > MaxMessageLength) return BadRequest(new { message = $"Pesan maksimal {MaxMessageLength} karakter." });

        message.Content = content;
        message.EditedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var dto = await GetMessageDto(message.Id);
        await hub.Clients.Group(ChatHub.GroupName(message.GroupId)).SendAsync("MessageUpdated", dto);
        return Ok(dto);
    }

    // Hapus pesan sendiri, atau pesan orang lain jika punya permission ManageMessages.
    [HttpDelete("messages/{messageId:long}")]
    public async Task<IActionResult> DeleteMessage(long messageId)
    {
        var message = await db.ChatMessages.FindAsync(messageId);
        if (message == null || message.DeletedAt != null) return NotFound();

        var access = await GetAccess(message.GroupId);
        if (access == null) return NotFound();

        var isOwnMessage = message.SenderUserId == CurrentUserId;
        if (!isOwnMessage && !access.Has(ChatPermission.ManageMessages)) return NoAccess("menghapus pesan orang lain");

        message.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var dto = await GetMessageDto(message.Id);
        await hub.Clients.Group(ChatHub.GroupName(message.GroupId)).SendAsync("MessageDeleted", dto);
        return Ok(dto);
    }

    [HttpPost("groups/{groupId:int}/read")]
    public async Task<IActionResult> MarkRead(int groupId, MarkChatReadRequest request)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();

        if (request.MessageId > access.Member.LastReadMessageId)
        {
            access.Member.LastReadMessageId = request.MessageId;
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    /* =========================================================
       ANGGOTA
    ========================================================= */

    // Cari user aktif untuk ditambahkan ke grup.
    [HttpGet("users")]
    public async Task<IActionResult> SearchUsers(string? search)
    {
        if (!await CanOpenChat()) return Forbid();

        var keyword = search?.Trim().ToLower() ?? "";

        var users = await db.Users
            .Where(x => x.Active)
            .Where(x => keyword == "" || x.Name.ToLower().Contains(keyword) || x.UserId.ToLower().Contains(keyword))
            .OrderBy(x => x.Name)
            .Take(20)
            .Select(x => new ChatUserOptionDto(x.Id, x.UserId, x.Name))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("groups/{groupId:int}/members")]
    public async Task<IActionResult> AddMembers(int groupId, AddChatMembersRequest request)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();
        if (!access.Has(ChatPermission.ManageMembers)) return NoAccess("menambah anggota");

        var role = request.RoleId.HasValue
            ? await db.ChatRoles.FirstOrDefaultAsync(x => x.Id == request.RoleId && x.GroupId == groupId)
            : await db.ChatRoles.FirstOrDefaultAsync(x => x.GroupId == groupId && x.IsDefault);

        if (role == null) return BadRequest(new { message = "Role tidak ditemukan." });
        if (role.Position >= access.Position) return NoAccess($"memberi role \"{role.Name}\"");

        var existingIds = await db.ChatMembers.Where(x => x.GroupId == groupId).Select(x => x.UserId).ToListAsync();

        var newUserIds = await db.Users
            .Where(x => x.Active && request.UserIds.Contains(x.Id) && !existingIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        // Anggota baru tidak dibanjiri badge pesan lama.
        var latestMessageId = await db.ChatMessages
            .Where(m => m.GroupId == groupId)
            .MaxAsync(m => (long?)m.Id) ?? 0;

        db.ChatMembers.AddRange(newUserIds.Select(userId => new ChatMember
        {
            GroupId = groupId,
            UserId = userId,
            RoleId = role.Id,
            LastReadMessageId = latestMessageId
        }));
        await db.SaveChangesAsync();

        foreach (var userId in newUserIds)
        {
            await JoinHubGroup(userId, groupId);
            await hub.Clients.User(userId.ToString()).SendAsync("AddedToGroup", groupId);
        }

        await NotifyGroupChanged(groupId);
        return Ok(new { message = $"{newUserIds.Count} anggota berhasil ditambahkan." });
    }

    [HttpPut("groups/{groupId:int}/members/{userId:int}")]
    public async Task<IActionResult> UpdateMember(int groupId, int userId, UpdateChatMemberRequest request)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();
        if (!access.Has(ChatPermission.ManageMembers)) return NoAccess("mengubah role anggota");

        var target = await db.ChatMembers.Include(x => x.Role).FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId);
        if (target == null) return NotFound(new { message = "Anggota tidak ditemukan." });
        if (userId == access.Group.OwnerUserId) return BadRequest(new { message = "Role owner tidak dapat diubah." });
        if (target.Role!.Position >= access.Position) return NoAccess("mengubah anggota dengan role setara / lebih tinggi");

        var role = await db.ChatRoles.FirstOrDefaultAsync(x => x.Id == request.RoleId && x.GroupId == groupId);
        if (role == null) return BadRequest(new { message = "Role tidak ditemukan." });
        if (role.Position >= access.Position) return NoAccess($"memberi role \"{role.Name}\"");

        target.RoleId = role.Id;
        await db.SaveChangesAsync();

        await NotifyGroupChanged(groupId);
        return Ok(new { message = "Role anggota berhasil diubah." });
    }

    // Mengeluarkan anggota, atau keluar sendiri jika userId = diri sendiri.
    [HttpDelete("groups/{groupId:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int groupId, int userId)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();

        var isLeaving = userId == CurrentUserId;

        if (userId == access.Group.OwnerUserId)
            return BadRequest(new { message = isLeaving ? "Owner tidak dapat keluar dari grup. Hapus grup jika tidak dipakai lagi." : "Owner tidak dapat dikeluarkan." });

        var target = await db.ChatMembers.Include(x => x.Role).FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId);
        if (target == null) return NotFound(new { message = "Anggota tidak ditemukan." });

        if (!isLeaving)
        {
            if (!access.Has(ChatPermission.ManageMembers)) return NoAccess("mengeluarkan anggota");
            if (target.Role!.Position >= access.Position) return NoAccess("mengeluarkan anggota dengan role setara / lebih tinggi");
        }

        db.ChatMembers.Remove(target);
        await db.SaveChangesAsync();

        await LeaveHubGroup(userId, groupId);
        await hub.Clients.User(userId.ToString()).SendAsync("RemovedFromGroup", groupId);
        await NotifyGroupChanged(groupId);

        return Ok(new { message = isLeaving ? "Anda telah keluar dari grup." : "Anggota berhasil dikeluarkan." });
    }

    /* =========================================================
       ROLE
    ========================================================= */

    [HttpPost("groups/{groupId:int}/roles")]
    public async Task<IActionResult> CreateRole(int groupId, SaveChatRoleRequest request)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();

        var error = ValidateRole(access, request);
        if (error != null) return error;

        db.ChatRoles.Add(new ChatRole
        {
            GroupId = groupId,
            Name = request.Name.Trim(),
            Color = request.Color,
            Permissions = (ChatPermission)request.Permissions,
            Position = request.Position
        });
        await db.SaveChangesAsync();

        await NotifyGroupChanged(groupId);
        return Ok(new { message = "Role berhasil dibuat." });
    }

    [HttpPut("groups/{groupId:int}/roles/{roleId:int}")]
    public async Task<IActionResult> UpdateRole(int groupId, int roleId, SaveChatRoleRequest request)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();

        var role = await db.ChatRoles.FirstOrDefaultAsync(x => x.Id == roleId && x.GroupId == groupId);
        if (role == null) return NotFound(new { message = "Role tidak ditemukan." });
        if (role.Position >= access.Position) return NoAccess("mengubah role setara / lebih tinggi");

        var error = ValidateRole(access, request);
        if (error != null) return error;

        role.Name = request.Name.Trim();
        role.Color = request.Color;
        role.Permissions = (ChatPermission)request.Permissions;
        role.Position = request.Position;
        await db.SaveChangesAsync();

        await NotifyGroupChanged(groupId);
        return Ok(new { message = "Role berhasil diubah." });
    }

    // Anggota dengan role yang dihapus dipindahkan ke role default.
    [HttpDelete("groups/{groupId:int}/roles/{roleId:int}")]
    public async Task<IActionResult> DeleteRole(int groupId, int roleId)
    {
        var access = await GetAccess(groupId);
        if (access == null) return NotFound();
        if (!access.Has(ChatPermission.ManageRoles)) return NoAccess("mengelola role");

        var role = await db.ChatRoles.FirstOrDefaultAsync(x => x.Id == roleId && x.GroupId == groupId);
        if (role == null) return NotFound(new { message = "Role tidak ditemukan." });
        if (role.IsDefault) return BadRequest(new { message = "Role default tidak dapat dihapus." });
        if (role.Position >= access.Position) return NoAccess("menghapus role setara / lebih tinggi");

        var defaultRole = await db.ChatRoles.FirstAsync(x => x.GroupId == groupId && x.IsDefault);

        await using var tx = await db.Database.BeginTransactionAsync();

        await db.ChatMembers
            .Where(x => x.GroupId == groupId && x.RoleId == roleId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RoleId, defaultRole.Id));

        db.ChatRoles.Remove(role);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        await NotifyGroupChanged(groupId);
        return Ok(new { message = "Role berhasil dihapus." });
    }

    /* =========================================================
       HELPER
    ========================================================= */

    // Hak akses user saat ini di sebuah grup.
    private record GroupAccess(ChatGroup Group, ChatMember Member, bool IsOwner, ChatPermission Permissions, int Position)
    {
        public bool Has(ChatPermission permission) =>
            IsOwner ||
            Permissions.HasFlag(ChatPermission.Administrator) ||
            Permissions.HasFlag(permission);
    }

    // null = grup tidak aktif / user bukan anggota.
    private async Task<GroupAccess?> GetAccess(int groupId)
    {
        var me = CurrentUserId;

        var group = await db.ChatGroups.FirstOrDefaultAsync(x => x.Id == groupId && x.Active);
        if (group == null) return null;

        var member = await db.ChatMembers.Include(x => x.Role).FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == me);
        if (member == null) return null;

        var isOwner = group.OwnerUserId == me;

        return new GroupAccess(
            group,
            member,
            isOwner,
            isOwner ? ChatPermission.All : member.Role!.Permissions,
            isOwner ? OwnerPosition : member.Role!.Position);
    }

    private Task<bool> CanOpenChat() =>
        permissionService.HasPermissionAsync(User, MenuCode, MenuPermission.View);

    private ObjectResult NoAccess(string action) =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = $"Anda tidak memiliki permission untuk {action}." });

    private IActionResult? ValidateRole(GroupAccess access, SaveChatRoleRequest request)
    {
        if (!access.Has(ChatPermission.ManageRoles)) return NoAccess("mengelola role");
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Nama role wajib diisi." });
        if (request.Position < 0) return BadRequest(new { message = "Posisi role minimal 0." });
        if (request.Position >= access.Position) return NoAccess("membuat role setara / lebih tinggi dari role Anda");

        // Hanya boleh memberikan permission yang dimiliki sendiri (kecuali owner / Administrator).
        var requested = (ChatPermission)request.Permissions;
        var canGrantAll = access.IsOwner || access.Permissions.HasFlag(ChatPermission.Administrator);
        if (!canGrantAll && (requested & ~access.Permissions) != 0)
            return NoAccess("memberikan permission yang tidak Anda miliki");

        return null;
    }

    private static readonly System.Linq.Expressions.Expression<Func<ChatMessage, ChatMessageDto>> ToMessageDto = x =>
        new ChatMessageDto(
            x.Id,
            x.GroupId,
            x.SenderUserId,
            x.Sender!.Name,
            x.DeletedAt == null ? x.Content : "",
            x.CreatedAt,
            x.EditedAt,
            x.DeletedAt != null);

    private Task<ChatMessageDto> GetMessageDto(long messageId) =>
        db.ChatMessages.Where(x => x.Id == messageId).Select(ToMessageDto).FirstAsync();

    private async Task<ChatGroupDetailDto> BuildGroupDetail(int groupId, int me)
    {
        var group = await db.ChatGroups.AsNoTracking().FirstAsync(x => x.Id == groupId);

        var members = await db.ChatMembers
            .AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .Select(x => new { x.UserId, Login = x.User!.UserId, x.User.Name, x.RoleId })
            .ToListAsync();

        var roles = await db.ChatRoles
            .AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .OrderByDescending(x => x.Position)
            .ToListAsync();

        var isOwner = group.OwnerUserId == me;
        var myRole = roles.First(r => r.Id == members.First(m => m.UserId == me).RoleId);

        return new ChatGroupDetailDto(
            group.Id,
            group.Name,
            group.Description,
            group.OwnerUserId,
            isOwner,
            (long)(isOwner ? ChatPermission.All : myRole.Permissions),
            isOwner ? OwnerPosition : myRole.Position,
            roles.Select(r => new ChatRoleDto(
                r.Id, r.Name, r.Color, (long)r.Permissions, r.Position, r.IsDefault,
                members.Count(m => m.RoleId == r.Id))).ToList(),
            members
                .OrderBy(m => m.Name)
                .Select(m => new ChatMemberDto(
                    m.UserId, m.Login, m.Name, m.RoleId,
                    m.UserId == group.OwnerUserId,
                    presence.IsOnline(m.UserId)))
                .ToList());
    }

    private Task NotifyGroupChanged(int groupId) =>
        hub.Clients.Group(ChatHub.GroupName(groupId)).SendAsync("GroupChanged", groupId);

    private async Task JoinHubGroup(int userId, int groupId)
    {
        foreach (var connectionId in presence.GetConnections(userId))
            await hub.Groups.AddToGroupAsync(connectionId, ChatHub.GroupName(groupId));
    }

    private async Task LeaveHubGroup(int userId, int groupId)
    {
        foreach (var connectionId in presence.GetConnections(userId))
            await hub.Groups.RemoveFromGroupAsync(connectionId, ChatHub.GroupName(groupId));
    }
}
