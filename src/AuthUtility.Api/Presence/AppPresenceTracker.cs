namespace AuthUtility.Api.Presence;

// Mencatat koneksi SignalR aktif per user untuk fitur "user online" (di memori server).
// Terpisah dari ChatPresenceTracker: ini mencakup semua user yang membuka aplikasi,
// bukan hanya yang membuka Live Chat. Satu user bisa punya beberapa koneksi (beberapa tab).
public class AppPresenceTracker
{
    private readonly Dictionary<int, HashSet<string>> _connections = new();
    private readonly object _lock = new();

    // true jika user baru saja online (koneksi pertamanya).
    public bool Connect(int userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var set))
            {
                set = new HashSet<string>();
                _connections[userId] = set;
            }

            set.Add(connectionId);
            return set.Count == 1;
        }
    }

    // true jika user baru saja offline (koneksi terakhirnya terputus).
    public bool Disconnect(int userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var set)) return false;

            set.Remove(connectionId);
            if (set.Count > 0) return false;

            _connections.Remove(userId);
            return true;
        }
    }

    public List<int> GetOnlineUserIds()
    {
        lock (_lock) return _connections.Keys.ToList();
    }
}
