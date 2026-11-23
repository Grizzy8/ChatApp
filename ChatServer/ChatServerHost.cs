using System.Net;
using System.Net.Sockets;
using ChatShared;

namespace ChatServer;

/// <summary>
/// TcpListener ile gelen bağlantıları dinler ve Join el sıkışmasını yürütür.
/// Arayüzden bağımsızdır; olayları event ile dışarı bildirir.
/// Event'ler arka plan thread'lerinden tetiklenebilir.
/// </summary>
public sealed class ChatServerHost : IDisposable
{
    private static readonly TimeSpan ShutdownNoticeTimeout = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private readonly ClientRegistry _registry = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    /// <summary>Sunucu bir log kaydı üretti.</summary>
    public event EventHandler<ServerLogEntry>? LogWritten;

    /// <summary>Sunucu durdu (kullanıcı isteğiyle veya hata nedeniyle).</summary>
    public event EventHandler? Stopped;

    /// <summary>Bağlı istemci listesi değişti; güncel liste için GetClients çağrılmalıdır.</summary>
    public event EventHandler? ClientsChanged;

    /// <summary>Son başlatılan dinleme portu.</summary>
    public int Port { get; private set; }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _listener is not null;
            }
        }
    }

    /// <summary>Katılmış istemcilerin güncel listesi (katılma sırasına göre).</summary>
    public IReadOnlyList<ClientInfo> GetClients() => _registry.GetClients();

    /// <summary>
    /// Verilen portta dinlemeye başlar. Port kullanımdaysa SocketException fırlatır.
    /// </summary>
    public void Start(int port)
    {
        if (port < ProtocolConstants.MinPort || port > ProtocolConstants.MaxPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port), port, $"Port {ProtocolConstants.MinPort}-{ProtocolConstants.MaxPort} aralığında olmalıdır.");
        }

        lock (_gate)
        {
            if (_listener is not null)
            {
                throw new InvalidOperationException("Sunucu zaten çalışıyor.");
            }

            // Aynı ağdaki diğer bilgisayarlar da bağlanabilsin diye tüm arayüzler dinlenir.
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            var cts = new CancellationTokenSource();
            _listener = listener;
            _cts = cts;
            Port = port;
            _ = Task.Run(() => AcceptLoopAsync(listener, cts.Token));
        }

        WriteLog($"Sunucu başlatıldı. Port: {port}");
    }

    /// <summary>Dinlemeyi durdurur ve bağlı istemcilere kapanacağını bildirir. Zaten durmuşsa bir şey yapmaz.</summary>
    public void Stop()
    {
        TcpListener listener;
        CancellationTokenSource cts;

        lock (_gate)
        {
            if (_listener is null || _cts is null)
            {
                return;
            }

            listener = _listener;
            cts = _cts;
            _listener = null;
            _cts = null;
        }

        // İptal edilen oturumlar kendi thread'lerinde ServerShuttingDown bildirip kapanır.
        cts.Cancel();
        listener.Stop();
        cts.Dispose();

        WriteLog("Sunucu durduruldu.");
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                _ = HandleClientAsync(client, token);
            }
        }
        catch when (token.IsCancellationRequested)
        {
            // Stop() çağrıldı: dinleyici kapanırken fırlatılan hatalar beklenen durumdur.
        }
        catch (Exception ex)
        {
            WriteLog($"Bağlantı kabul döngüsü beklenmedik şekilde sonlandı: {ex.Message}", ServerLogLevel.Error);
            Stop();
        }
    }

    /// <summary>Bir istemcinin tüm ömrünü yönetir. Hiçbir hatayı dışarı sızdırmaz.</summary>
    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken token)
    {
        ClientSession session;
        try
        {
            session = new ClientSession(tcpClient);
        }
        catch (Exception ex)
        {
            tcpClient.Dispose();
            WriteLog($"Bağlantı kurulamadı: {ex.Message}", ServerLogLevel.Error);
            return;
        }

        using (session)
        {
            WriteLog($"Bağlantı alındı: {session.Endpoint}");

            try
            {
                if (await TryJoinAsync(session, token).ConfigureAwait(false))
                {
                    await ReceiveLoopAsync(session, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await NotifyShuttingDownAsync(session).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException)
            {
                WriteLog($"{Describe(session)} ile bağlantı koptu: {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog($"{Describe(session)} için beklenmedik hata: {ex.Message}", ServerLogLevel.Error);
            }
            finally
            {
                if (_registry.Remove(session))
                {
                    WriteLog($"{session.Username} ayrıldı.");
                    ClientsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    /// <summary>
    /// Join el sıkışması: ilk mesaj süre içinde gelen geçerli bir Join olmalıdır.
    /// Kabul edilirse oturum kaydedilir; aksi halde istemciye sebep bildirilir.
    /// </summary>
    private async Task<bool> TryJoinAsync(ClientSession session, CancellationToken token)
    {
        string? line = await ReadWithTimeoutAsync(session, ProtocolConstants.JoinTimeout, token).ConfigureAwait(false);
        if (line is null)
        {
            WriteLog($"{session.Endpoint} katılma mesajı göndermeden ayrıldı veya süre doldu.");
            return false;
        }

        ChatMessage? message = MessageSerializer.Deserialize(line);
        if (message is null)
        {
            await SendErrorAsync(session, ErrorCode.InvalidMessage, token).ConfigureAwait(false);
            WriteLog($"{session.Endpoint} geçersiz ilk mesaj gönderdi, bağlantı kapatıldı.", ServerLogLevel.Error);
            return false;
        }

        if (message.Type != MessageType.Join)
        {
            await SendErrorAsync(session, ErrorCode.NotJoined, token).ConfigureAwait(false);
            WriteLog($"{session.Endpoint} katılmadan {message.Type} mesajı gönderdi, bağlantı kapatıldı.", ServerLogLevel.Error);
            return false;
        }

        ErrorCode rejection = UsernameValidator.Validate(message.Sender);
        if (rejection == ErrorCode.None && !_registry.TryAdd(message.Sender!, session))
        {
            rejection = ErrorCode.UsernameTaken;
        }

        if (rejection != ErrorCode.None)
        {
            await session.SendAsync(new ChatMessage(MessageType.JoinRejected, ErrorCode: rejection), token).ConfigureAwait(false);
            WriteLog($"{session.Endpoint} için katılma reddedildi ({rejection}).");
            return false;
        }

        await session.SendAsync(
            new ChatMessage(MessageType.JoinAccepted, Timestamp: DateTime.Now, Users: _registry.GetUsernames()),
            token).ConfigureAwait(false);

        WriteLog($"{session.Username} katıldı ({session.Endpoint}).");
        ClientsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Katılmış istemciden gelen mesajları, bağlantı kapanana veya Leave gelene kadar okur.</summary>
    private async Task ReceiveLoopAsync(ClientSession session, CancellationToken token)
    {
        while (true)
        {
            string? line = await session.ReadLineAsync(token).ConfigureAwait(false);
            if (line is null)
            {
                WriteLog($"{session.Username} bağlantıyı kapattı.");
                return;
            }

            ChatMessage? message = MessageSerializer.Deserialize(line);
            if (message is null)
            {
                WriteLog($"{session.Username} geçersiz bir mesaj gönderdi, yok sayıldı.", ServerLogLevel.Error);
                continue;
            }

            if (message.Type == MessageType.Leave)
            {
                return;
            }

            // Sohbet ve özel mesajların iletilmesi sonraki adımlarda (MessageRouter) eklenecek.
        }
    }

    /// <summary>Bir satır okur; süre dolarsa null döner. Sunucu durdurulursa OperationCanceledException fırlatır.</summary>
    private static async Task<string?> ReadWithTimeoutAsync(ClientSession session, TimeSpan timeout, CancellationToken token)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await session.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return null;
        }
    }

    private static Task SendErrorAsync(ClientSession session, ErrorCode code, CancellationToken token) =>
        session.SendAsync(new ChatMessage(MessageType.Error, ErrorCode: code), token);

    /// <summary>Kapanış sırasında istemciye haber vermeyi dener; başarısız olması önemli değildir.</summary>
    private static async Task NotifyShuttingDownAsync(ClientSession session)
    {
        using var cts = new CancellationTokenSource(ShutdownNoticeTimeout);
        try
        {
            await SendErrorAsync(session, ErrorCode.ServerShuttingDown, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Sunucu zaten kapanıyor; istemci bağlantının koptuğunu kendisi fark edecek.
        }
    }

    private static string Describe(ClientSession session) => session.Username ?? session.Endpoint;

    private void WriteLog(string message, ServerLogLevel level = ServerLogLevel.Info)
    {
        LogWritten?.Invoke(this, new ServerLogEntry(DateTime.Now, message, level));
    }
}
