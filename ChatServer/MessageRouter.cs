using System.Net.Sockets;
using ChatShared;

namespace ChatServer;

/// <summary>
/// Mesajları kayıtlı istemcilere iletir. Bir istemciye ulaşılamaması diğerlerinin
/// mesaj almasını engellemez; ulaşılamayan bağlantı kapatılır.
/// </summary>
public sealed class MessageRouter
{
    /// <summary>Yazma işlemi bu sürede bitmezse istemci takılmış sayılır ve bağlantısı kapatılır.</summary>
    public static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(5);

    private readonly ClientRegistry _registry;

    public MessageRouter(ClientRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>Bir mesaj bir istemciye iletilemedi; olay verisi log metnidir.</summary>
    public event EventHandler<string>? DeliveryFailed;

    /// <summary>
    /// Mesajı katılımı tamamlanmış (aktif) tüm istemcilere iletir; excluded verilirse o oturum atlanır.
    /// </summary>
    public async Task BroadcastAsync(ChatMessage message, CancellationToken cancellationToken, ClientSession? excluded = null)
    {
        IEnumerable<ClientSession> recipients = _registry.GetSessions()
            .Where(session => session.IsActive && !ReferenceEquals(session, excluded));

        await Task.WhenAll(recipients.Select(session => DeliverAsync(session, message, cancellationToken)))
            .ConfigureAwait(false);
    }

    /// <summary>Mesajı tek bir istemciye iletir. İletilemezse bağlantı kapatılır ve false döner.</summary>
    public Task<bool> SendToAsync(ClientSession target, ChatMessage message, CancellationToken cancellationToken) =>
        DeliverAsync(target, message, cancellationToken);

    private async Task<bool> DeliverAsync(ClientSession session, ChatMessage message, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DeliveryTimeout);

        try
        {
            await session.SendAsync(message, timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Sunucu kapanıyor; çağıran bunu normal kapanış olarak ele alır.
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Yarım yazılmış satır akışı bozar; bu yüzden bağlantı tamamen kapatılır.
            // İstemcinin kendi okuma döngüsü bunu fark edip kaydını siler.
            DeliveryFailed?.Invoke(this, $"{session.Username} kullanıcısına mesaj iletilemedi, bağlantısı kapatılıyor: {ex.Message}");
            session.Dispose();
            return false;
        }
    }
}
