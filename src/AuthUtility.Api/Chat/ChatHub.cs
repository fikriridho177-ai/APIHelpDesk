using System.Security.Claims;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Chat;

// Koneksi realtime Live Chat (endpoint: /hubs/chat).
// Semua perubahan data (kirim pesan, anggota, role) lewat ChatController;
// hub ini hanya untuk menerima event, status online, dan indikator mengetik.
//
// Event yang dikirim ke client:
//   MessageCreated(message), MessageUpdated(message), MessageDeleted(message)
//   GroupChanged(groupId), AddedToGroup(groupId), RemovedFromGroup(groupId)
//   PresenceChanged(userId, online), Typing(groupId, userId, name)
[Authorize]
public class ChatHub(AppDbContext db, ChatPresenceTracker presence) : Hub
{
    public static string GroupName(int groupId) => $"chat-{groupId}";

    private int CurrentUserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;

        // Masukkan koneksi ini ke semua grup chat milik user.
        var groupIds = await db.ChatMembers
            .Where(x => x.UserId == userId)
            .Select(x => x.GroupId)
            .ToListAsync();

        foreach (var groupId in groupIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId));

        if (presence.Connect(userId, Context.ConnectionId))
            await Clients.Others.SendAsync("PresenceChanged", userId, true);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;

        if (presence.Disconnect(userId, Context.ConnectionId))
            await Clients.Others.SendAsync("PresenceChanged", userId, false);

        await base.OnDisconnectedAsync(exception);
    }

    // Dipanggil client saat user sedang mengetik di sebuah grup.
    public async Task Typing(int groupId)
    {
        var userId = CurrentUserId;

        var name = await db.ChatMembers
            .Where(x => x.GroupId == groupId && x.UserId == userId)
            .Select(x => x.User!.Name)
            .FirstOrDefaultAsync();

        if (name == null) return; // bukan anggota grup

        await Clients.OthersInGroup(GroupName(groupId)).SendAsync("Typing", groupId, userId, name);
    }
}
