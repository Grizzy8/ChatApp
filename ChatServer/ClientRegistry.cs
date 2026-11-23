using System.Collections.Concurrent;
using ChatShared;

namespace ChatServer;

/// <summary>
/// Katılmış istemcilerin kaydı. Kullanıcı adı karşılaştırması büyük/küçük harf duyarsızdır
/// ve ekleme işlemi atomiktir (aynı anda gelen iki "ali" için yalnızca biri kabul edilir).
/// </summary>
public sealed class ClientRegistry
{
    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new(UsernameValidator.Comparer);

    public int Count => _sessions.Count;

    /// <summary>Ad boştaysa oturumu kaydeder ve kullanıcı adını atar; ad doluysa false döner.</summary>
    public bool TryAdd(string username, ClientSession session)
    {
        if (!_sessions.TryAdd(username, session))
        {
            return false;
        }

        session.AssignUsername(username);
        return true;
    }

    /// <summary>Katılımı tamamlanmış (aktif) bir kullanıcının oturumunu bulur; ad büyük/küçük harf duyarsızdır.</summary>
    public bool TryGetActive(string username, out ClientSession session)
    {
        if (_sessions.TryGetValue(username, out ClientSession? found) && found.IsActive)
        {
            session = found;
            return true;
        }

        session = null!;
        return false;
    }

    /// <summary>Oturum kayıtlıysa çıkarır. Yalnızca bu oturuma ait kayıt silinir.</summary>
    public bool Remove(ClientSession session)
    {
        if (session.Username is null)
        {
            return false;
        }

        return ((ICollection<KeyValuePair<string, ClientSession>>)_sessions)
            .Remove(new KeyValuePair<string, ClientSession>(session.Username, session));
    }

    /// <summary>Şu an kayıtlı kullanıcı adları (katılma sırasına göre).</summary>
    public IReadOnlyList<string> GetUsernames() =>
        GetOrderedEntries().Select(entry => entry.Key).ToList();

    /// <summary>Şu an kayıtlı oturumlar (katılma sırasına göre).</summary>
    public IReadOnlyList<ClientSession> GetSessions() =>
        GetOrderedEntries().Select(entry => entry.Value).ToList();

    /// <summary>Arayüzde göstermek için istemci bilgileri (katılma sırasına göre).</summary>
    public IReadOnlyList<ClientInfo> GetClients() =>
        GetOrderedEntries()
            .Select(entry => new ClientInfo(entry.Value.UserId ?? "?", entry.Value.Endpoint, entry.Value.ConnectedAt))
            .ToList();

    // Kullanıcı adı olarak sözlük anahtarı kullanılır; böylece TryAdd ile AssignUsername
    // arasındaki kısa anda bile tutarlı bir liste döner.
    private IEnumerable<KeyValuePair<string, ClientSession>> GetOrderedEntries() =>
        _sessions.OrderBy(entry => entry.Value.ConnectedAt);
}
