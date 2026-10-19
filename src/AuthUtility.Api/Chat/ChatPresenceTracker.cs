namespace AuthUtility.Api.Chat;

// Mencatat koneksi SignalR yang aktif per user (di memori server) untuk status online.
// Satu user bisa punya beberapa koneksi (beberapa tab / perangkat).
public class ChatPresenceTracker
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

    public bool IsOnline(int userId)
    {
        lock (_lock) return _connections.ContainsKey(userId);
    }

    public List<string> GetConnections(int userId)
    {
        lock (_lock)
        {
            return _connections.TryGetValue(userId, out var set) ? set.ToList() : new List<string>();
        }
    }

    public List<int> GetOnlineUserIds()
    {
        lock (_lock) return _connections.Keys.ToList();
    }
}
