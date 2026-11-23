using System.Net.Sockets;
using ChatShared;

namespace ChatClient;

/// <summary>
/// Sunucuya bağlanır, Join el sıkışmasını yapar ve gelen mesajları dinler. Arayüzden bağımsızdır.
/// Event'ler, oluşturulurken verilen SynchronizationContext üzerinden (WinForms'ta UI thread'i)
/// ve tetiklendikleri sırayla iletilir; context verilmezse çağıran thread'de tetiklenir.
/// </summary>
public sealed class ChatClientService : IDisposable
{
    private readonly SynchronizationContext? _context;
    private readonly object _gate = new();

    private ConnectionState _state = ConnectionState.Disconnected;
    private string? _username;
    private MessageChannel? _channel;
    private CancellationTokenSource? _receiveCts;
    private Task _receiveTask = Task.CompletedTask;
    private bool _shutdownNoticed;

    public ChatClientService(SynchronizationContext? context = null)
    {
        _context = context;
    }

    /// <summary>Bağlantı durumu değişti.</summary>
    public event EventHandler<ConnectionState>? StateChanged;

    /// <summary>Sunucudan (hata ve kapanış bildirimleri dışında) bir mesaj geldi.</summary>
    public event EventHandler<ChatMessage>? MessageReceived;

    /// <summary>Kurulmuş bir bağlantı sona erdi (kullanıcı isteğiyle veya beklenmedik şekilde).</summary>
    public event EventHandler<DisconnectInfo>? Disconnected;

    public ConnectionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Bağlıyken kullanıcının adı, aksi halde null.</summary>
    public string? Username
    {
        get
        {
            lock (_gate)
            {
                return _username;
            }
        }
    }

    /// <summary>
    /// Sunucuya bağlanır ve Join el sıkışmasını tamamlar. Beklenen hatalar (sunucu kapalı, ad kullanımda,
    /// zaman aşımı...) exception olarak değil, Türkçe açıklamalı bir ConnectResult olarak döner.
    /// </summary>
    public async Task<ConnectResult> ConnectAsync(string host, int port, string username, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_state != ConnectionState.Disconnected)
            {
                throw new InvalidOperationException("İstemci zaten bağlı veya bağlanıyor.");
            }

            _state = ConnectionState.Connecting;
        }

        RaiseStateChanged(ConnectionState.Connecting);

        var tcpClient = new TcpClient();
        try
        {
            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeCts.CancelAfter(ProtocolConstants.ConnectTimeout);

            await tcpClient.ConnectAsync(host, port, handshakeCts.Token).ConfigureAwait(false);
            var channel = new MessageChannel(tcpClient);

            await channel.SendAsync(new ChatMessage(MessageType.Join, Sender: username), handshakeCts.Token).ConfigureAwait(false);
            ConnectResult result = await ReadJoinReplyAsync(channel, handshakeCts.Token).ConfigureAwait(false);

            if (!result.Success)
            {
                return AbortConnect(tcpClient, result);
            }

            CompleteConnect(channel, username);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AbortConnect(tcpClient, ConnectResult.Failed("Bağlantı iptal edildi."));
        }
        catch (OperationCanceledException)
        {
            return AbortConnect(tcpClient, ConnectResult.Failed(ErrorMessages.TimeoutMessage));
        }
        catch (SocketException ex)
        {
            return AbortConnect(tcpClient, ConnectResult.Failed(ErrorMessages.Describe(ex, host, port)));
        }
        catch (ArgumentException)
        {
            return AbortConnect(tcpClient, ConnectResult.Failed("Sunucu adresi geçersiz."));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException)
        {
            return AbortConnect(tcpClient, ConnectResult.Failed($"Bağlantı sırasında hata oluştu: {ex.Message}"));
        }
    }

    /// <summary>
    /// Genel sohbete mesaj gönderir. Bağlı değilse veya gönderme başarısız olursa false döner;
    /// gönderme hatası bağlantının koptuğu anlamına gelir ve Disconnected olayı tetiklenir.
    /// </summary>
    public async Task<bool> SendChatAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, ProtocolConstants.MaxMessageLength);

        MessageChannel? channel;
        lock (_gate)
        {
            channel = _state == ConnectionState.Connected ? _channel : null;
        }

        if (channel is null)
        {
            return false;
        }

        try
        {
            await channel.SendAsync(new ChatMessage(MessageType.Chat, Text: text), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            EndConnection(channel, new DisconnectInfo(DisconnectReason.ConnectionLost, $"Mesaj gönderilemedi, bağlantı koptu: {ex.Message}"));
            return false;
        }
    }

    /// <summary>Bağlantıyı kullanıcı isteğiyle keser. Bağlı değilse bir şey yapmaz.</summary>
    public async Task DisconnectAsync()
    {
        MessageChannel? channel;
        Task receiveTask;

        lock (_gate)
        {
            channel = _state == ConnectionState.Connected ? _channel : null;
            receiveTask = _receiveTask;
        }

        if (channel is null)
        {
            return;
        }

        await TrySendLeaveAsync(channel).ConfigureAwait(false);
        EndConnection(channel, new DisconnectInfo(DisconnectReason.UserRequested, "Bağlantı kesildi."));
        await receiveTask.ConfigureAwait(false);
    }

    /// <summary>Olay göndermeden bağlantıyı kapatır (uygulama kapanırken kullanılır).</summary>
    public void Dispose()
    {
        MessageChannel? channel;
        CancellationTokenSource? cts;

        lock (_gate)
        {
            channel = _channel;
            cts = _receiveCts;
            _channel = null;
            _receiveCts = null;
            _state = ConnectionState.Disconnected;
            _username = null;
        }

        cts?.Cancel();
        channel?.Dispose();
        cts?.Dispose();
    }

    private static async Task<ConnectResult> ReadJoinReplyAsync(MessageChannel channel, CancellationToken token)
    {
        string? line = await channel.ReadLineAsync(token).ConfigureAwait(false);
        if (line is null)
        {
            return ConnectResult.Failed("Sunucu bağlantıyı kapattı.");
        }

        ChatMessage? reply = MessageSerializer.Deserialize(line);
        return reply?.Type switch
        {
            MessageType.JoinAccepted => ConnectResult.Succeeded(reply.Users ?? Array.Empty<string>()),
            MessageType.JoinRejected or MessageType.Error => ConnectResult.Failed(ErrorMessages.Describe(reply.ErrorCode)),
            _ => ConnectResult.Failed("Sunucudan beklenmeyen bir yanıt alındı.")
        };
    }

    private ConnectResult AbortConnect(TcpClient tcpClient, ConnectResult failure)
    {
        tcpClient.Dispose();

        lock (_gate)
        {
            _state = ConnectionState.Disconnected;
        }

        RaiseStateChanged(ConnectionState.Disconnected);
        return failure;
    }

    private void CompleteConnect(MessageChannel channel, string username)
    {
        var cts = new CancellationTokenSource();

        lock (_gate)
        {
            _channel = channel;
            _receiveCts = cts;
            _username = username;
            _shutdownNoticed = false;
            _state = ConnectionState.Connected;
            _receiveTask = Task.Run(() => ReceiveLoopAsync(channel, cts.Token));
        }

        RaiseStateChanged(ConnectionState.Connected);
    }

    /// <summary>Bağlantı süresince sunucudan gelen mesajları okur. Hiçbir hatayı dışarı sızdırmaz.</summary>
    private async Task ReceiveLoopAsync(MessageChannel channel, CancellationToken token)
    {
        DisconnectInfo info;
        try
        {
            info = await ReadMessagesAsync(channel, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // Bağlantıyı DisconnectAsync/Dispose kapattı; sonlandırmayı o yapar.
        }
        catch (Exception ex)
        {
            info = new DisconnectInfo(DisconnectReason.ConnectionLost, $"Sunucuyla bağlantı koptu: {ex.Message}");
        }

        EndConnection(channel, info);
    }

    private async Task<DisconnectInfo> ReadMessagesAsync(MessageChannel channel, CancellationToken token)
    {
        while (true)
        {
            string? line = await channel.ReadLineAsync(token).ConfigureAwait(false);
            if (line is null)
            {
                return _shutdownNoticed
                    ? new DisconnectInfo(DisconnectReason.ServerShuttingDown, "Sunucu kapatıldı. Bağlantı sonlandırıldı.")
                    : new DisconnectInfo(DisconnectReason.ServerClosed, "Sunucu bağlantıyı kapattı.");
            }

            ChatMessage? message = MessageSerializer.Deserialize(line);
            if (message is null)
            {
                continue; // Bozuk satır yok sayılır, bağlantı düşmez.
            }

            if (message.Type == MessageType.Error && message.ErrorCode == ErrorCode.ServerShuttingDown)
            {
                _shutdownNoticed = true;
                continue;
            }

            RaiseMessageReceived(message);
        }
    }

    /// <summary>Bağlantıyı bir kez ve güvenle sonlandırır; aynı bağlantı için tekrar çağrılırsa bir şey yapmaz.</summary>
    private void EndConnection(MessageChannel channel, DisconnectInfo info)
    {
        CancellationTokenSource? cts;

        lock (_gate)
        {
            if (!ReferenceEquals(_channel, channel))
            {
                return;
            }

            cts = _receiveCts;
            _channel = null;
            _receiveCts = null;
            _username = null;
            _state = ConnectionState.Disconnected;
        }

        cts?.Cancel();
        channel.Dispose();
        cts?.Dispose();

        RaiseStateChanged(ConnectionState.Disconnected);
        Raise(() => Disconnected?.Invoke(this, info));
    }

    private static async Task TrySendLeaveAsync(MessageChannel channel)
    {
        using var cts = new CancellationTokenSource(ClientConstants.LeaveTimeout);
        try
        {
            await channel.SendAsync(new ChatMessage(MessageType.Leave), cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Sunucuya ulaşılamıyorsa zaten bağlantıyı yerelde kapatıyoruz; Leave bildirimi şart değil.
        }
    }

    private void RaiseStateChanged(ConnectionState state) => Raise(() => StateChanged?.Invoke(this, state));

    private void RaiseMessageReceived(ChatMessage message) => Raise(() => MessageReceived?.Invoke(this, message));

    private void Raise(Action action)
    {
        if (_context is null)
        {
            action();
            return;
        }

        _context.Post(_ => action(), null);
    }
}
