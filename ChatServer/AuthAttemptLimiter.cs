using System.Collections.Concurrent;
using System.Net;
using ChatShared;

namespace ChatServer;

/// <summary>
/// Parola deneme saldırılarını yavaşlatmak için kayıt/giriş denemelerini IP adresi başına sınırlar.
/// Port numarası sayılmaz: aynı bilgisayar her denemede yeni bir kaynak portundan bağlanabilir.
/// </summary>
public sealed class AuthAttemptLimiter
{
    // Çok sayıda farklı adresle bellek şişirilmesin: sınır aşılınca kayıtlar sıfırlanır.
    private const int MaxTrackedAddresses = 10_000;

    private readonly ConcurrentDictionary<string, SlidingWindowRateLimiter> _byAddress = new();
    private readonly int _limit;
    private readonly TimeSpan _window;

    public AuthAttemptLimiter(int? limit = null, TimeSpan? window = null)
    {
        _limit = limit ?? ProtocolConstants.MaxAuthAttemptsPerWindow;
        _window = window ?? ProtocolConstants.AuthAttemptWindow;
    }

    /// <summary>Bu adresten bir deneme hakkı alır; sınır aşılmışsa false döner. endpoint "IP:port" biçimindedir.</summary>
    public bool TryAcquire(string endpoint)
    {
        string key = GetAddressKey(endpoint);

        if (_byAddress.Count >= MaxTrackedAddresses && !_byAddress.ContainsKey(key))
        {
            _byAddress.Clear();
        }

        return _byAddress.GetOrAdd(key, _ => new SlidingWindowRateLimiter(_limit, _window)).TryAcquire();
    }

    private static string GetAddressKey(string endpoint) =>
        IPEndPoint.TryParse(endpoint, out IPEndPoint? parsed) ? parsed.Address.ToString() : endpoint;
}
