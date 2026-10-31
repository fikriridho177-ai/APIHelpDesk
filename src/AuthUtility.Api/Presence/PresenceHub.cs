using System.Security.Claims;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Presence;

public record OnlineUserDto(int Id, string UserId, string Name, string? Group);

// Koneksi realtime daftar user online (endpoint: /hubs/presence).
// Dibuka frontend untuk semua user yang login, di semua halaman.
//
// Event yang dikirim ke client:
//   OnlineUsers(users) - daftar lengkap user online; dikirim ke koneksi yang baru terhubung,
//                        dan ke semua client setiap ada user yang masuk / keluar.
[Authorize]
public class PresenceHub(AppDbContext db, AppPresenceTracker presence) : Hub
{
    private int CurrentUserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public override async Task OnConnectedAsync()
    {
        var changed = presence.Connect(CurrentUserId, Context.ConnectionId);
        var users = await GetOnlineUsers();

        if (changed)
            await Clients.All.SendAsync("OnlineUsers", users);
        else
            await Clients.Caller.SendAsync("OnlineUsers", users); // tab kedua: daftar tidak berubah

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (presence.Disconnect(CurrentUserId, Context.ConnectionId))
            await Clients.All.SendAsync("OnlineUsers", await GetOnlineUsers());

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<List<OnlineUserDto>> GetOnlineUsers()
    {
        var ids = presence.GetOnlineUserIds();

        return await db.Users
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new OnlineUserDto(x.Id, x.UserId, x.Name, x.Group != null ? x.Group.Name : null))
            .ToListAsync();
    }
}
