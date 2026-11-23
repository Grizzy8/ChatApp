using System.Net.Sockets;
using ChatShared;

namespace ChatServer;

/// <summary>
/// Sunucuya bağlanan tek bir istemci: ağ kanalı ile kullanıcı adı ve bağlantı bilgilerini bir arada tutar.
/// </summary>
public sealed class ClientSession : IDisposable
{
    private readonly MessageChannel _channel;
    private volatile bool _isActive;

    public ClientSession(TcpClient tcpClient)
    {
        _channel = new MessageChannel(tcpClient);
        ConnectedAt = DateTime.Now;
    }

    /// <summary>
    /// JoinAccepted gönderildikten sonra true olur. Yayınlar yalnızca aktif oturumlara gider;
    /// böylece katılmakta olan bir istemci el sıkışması bitmeden başka mesaj almaz.
    /// </summary>
    public bool IsActive => _isActive;

    public void MarkActive() => _isActive = true;

    /// <summary>Join kabul edilene kadar null kalır.</summary>
    public string? Username { get; private set; }

    /// <summary>İstemcinin IP:port bilgisi.</summary>
    public string Endpoint => _channel.RemoteEndpoint;

    public DateTime ConnectedAt { get; }

    /// <summary>Bu istemcinin mesaj gönderme hızını sınırlar; sunucuyu mesaj yağmurundan korur.</summary>
    public SlidingWindowRateLimiter RateLimiter { get; } =
        new(ProtocolConstants.MaxMessagesPerWindow, ProtocolConstants.RateLimitWindow);

    /// <summary>Hesabın benzersiz kimliği; giriş kabul edilene kadar null kalır.</summary>
    public string? UserId { get; private set; }

    public void AssignUserId(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        UserId = userId;
    }

    public void AssignUsername(string username)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        Username = username;
    }

    /// <summary>Bir satır okur; bağlantı kapandıysa null döner.</summary>
    public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
        _channel.ReadLineAsync(cancellationToken);

    /// <summary>Mesajı tek satır JSON olarak gönderir.</summary>
    public Task SendAsync(ChatMessage message, CancellationToken cancellationToken) =>
        _channel.SendAsync(message, cancellationToken);

    public void Dispose() => _channel.Dispose();
}
