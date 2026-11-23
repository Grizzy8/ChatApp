using System.Net.Sockets;
using ChatShared;

namespace ChatClient;

/// <summary>
/// Sunucuya bağlanır, kayıt/giriş el sıkışmasını yapar ve gelen mesajları dinler. Arayüzden bağımsızdır.
/// Event'ler, oluşturulurken verilen SynchronizationContext üzerinden (WinForms'ta UI thread'i)
/// ve tetiklendikleri sırayla iletilir; context verilmezse çağıran thread'de tetiklenir.
/// </summary>
public sealed class ChatClientService : IDisposable
{
    private readonly SynchronizationContext? _context;
    private readonly object _gate = new();

    private ConnectionState _state = ConnectionState.Disconnected;
    private string? _username;
    private string? _userId;
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

    /// <summary>
    /// Sunucudan bir mesaj geldi (kapanış bildirimi hariç). Bağlantı kurulunca, çevrimiçi kullanıcı
    /// listesini taşıyan bir JoinAccepted mesajı da ilk olay olarak buradan iletilir.
    /// </summary>
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

    /// <summary>Bağlıyken hesabın benzersiz kimliği, aksi halde null.</summary>
    public string? UserId
    {
        get
        {
            lock (_gate)
            {
                return _userId;
            }
        }
    }

    /// <summary>
    /// Sunucuya bağlanır ve kayıt/giriş el sıkışmasını tamamlar. Beklenen hatalar (sunucu kapalı, yanlış parola,
    /// zaman aşımı...) exception olarak değil, Türkçe açıklamalı bir ConnectResult olarak döner.
    /// Parola yalnızca bu çağrıda gönderilir; serviste saklanmaz.
    /// </summary>
    public async Task<ConnectResult> ConnectAsync(
        string host, int port, string username, string password, AuthMode mode, CancellationToken cancellationToken = default)
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

            var request = new ChatMessage(
                mode == AuthMode.Register ? MessageType.Register : MessageType.Login, Sender: username, Password: password);
            await channel.SendAsync(request, handshakeCts.Token).ConfigureAwait(false);
            ConnectResult result = await ReadJoinReplyAsync(channel, handshakeCts.Token).ConfigureAwait(false);

            if (!result.Success)
            {
                return AbortConnect(tcpClient, result);
            }

            CompleteConnect(channel, result.Username!, result.UserId!, result.Users ?? Array.Empty<string>());
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
    public Task<bool> SendChatAsync(string text, CancellationToken cancellationToken = default) =>
        SendMessageAsync(new ChatMessage(MessageType.Chat, Text: text), cancellationToken);

    /// <summary>Belirtilen kullanıcıya özel mesaj gönderir. Dönüş değeri SendChatAsync ile aynı anlamdadır.</summary>
    public Task<bool> SendPrivateAsync(string recipient, string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        return SendMessageAsync(new ChatMessage(MessageType.Private, Recipient: recipient, Text: text), cancellationToken);
    }

    /// <summary>
    /// Kullanıcı adı ya da ID ile kişi ekletir. Sonuç, sunucudan gelen ContactList (başarı) veya Error (hata)
    /// mesajıyla MessageReceived olayında iletilir. Dönüş değeri SendChatAsync ile aynı anlamdadır.
    /// </summary>
    public Task<bool> AddContactAsync(string usernameOrId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameOrId);
        return SendMessageAsync(new ChatMessage(MessageType.AddContact, Text: usernameOrId.Trim()), cancellationToken);
    }

    /// <summary>Kişiyi kimliğiyle sildirir. Sonuç AddContactAsync gibi ContactList/Error olarak iletilir.</summary>
    public Task<bool> RemoveContactAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return SendMessageAsync(new ChatMessage(MessageType.RemoveContact, Text: userId), cancellationToken);
    }

    // ---- gruplar ----

    /// <summary>Grup kurar. Sonuç GroupList (başarı) ya da Error olarak MessageReceived'da iletilir.</summary>
    public Task<bool> CreateGroupAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return SendMessageAsync(new ChatMessage(MessageType.CreateGroup, Text: name.Trim()), cancellationToken);
    }

    /// <summary>Gruba kullanıcı adı ya da ID ile davet gönderir (kurucu/yönetici).</summary>
    public Task<bool> InviteToGroupAsync(string groupId, string usernameOrId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameOrId);
        return SendGroupRequestAsync(MessageType.InviteToGroup, groupId, usernameOrId.Trim(), cancellationToken);
    }

    public Task<bool> AcceptInviteAsync(string groupId, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.AcceptInvite, groupId, null, cancellationToken);

    public Task<bool> DeclineInviteAsync(string groupId, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.DeclineInvite, groupId, null, cancellationToken);

    public Task<bool> RequestGroupMembersAsync(string groupId, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.GetGroupMembers, groupId, null, cancellationToken);

    public Task<bool> SendGroupMessageAsync(string groupId, string text, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.GroupMessage, groupId, text, cancellationToken);

    public Task<bool> LeaveGroupAsync(string groupId, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.LeaveGroup, groupId, null, cancellationToken);

    /// <summary>Grubun adını değiştirir (yalnızca kurucu).</summary>
    public Task<bool> RenameGroupAsync(string groupId, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        return SendGroupRequestAsync(MessageType.RenameGroup, groupId, newName.Trim(), cancellationToken);
    }

    public Task<bool> DeleteGroupAsync(string groupId, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.DeleteGroup, groupId, null, cancellationToken);

    /// <summary>Üyeyi (kimliğiyle) gruptan çıkarır.</summary>
    public Task<bool> RemoveMemberAsync(string groupId, string memberId, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.RemoveMember, groupId, memberId, cancellationToken);

    /// <summary>Üyeyi yönetici yapar (yalnızca kurucu).</summary>
    public Task<bool> PromoteMemberAsync(string groupId, string memberId, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.PromoteMember, groupId, memberId, cancellationToken);

    /// <summary>Yöneticiliği alır (yalnızca kurucu).</summary>
    public Task<bool> DemoteMemberAsync(string groupId, string memberId, CancellationToken cancellationToken = default) =>
        SendGroupRequestAsync(MessageType.DemoteMember, groupId, memberId, cancellationToken);

    private Task<bool> SendGroupRequestAsync(MessageType type, string groupId, string? text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        return SendMessageAsync(new ChatMessage(type, Recipient: groupId, Text: text), cancellationToken);
    }

    private async Task<bool> SendMessageAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        // Metin zorunlu olmayan istekler (örn. LeaveGroup) null Text ile gönderilir; varsa uzunluğu sınırlıdır.
        if (message.Text is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message.Text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(message.Text.Length, ProtocolConstants.MaxMessageLength);
        }

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
            await channel.SendAsync(message, cancellationToken).ConfigureAwait(false);
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
            _userId = null;
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
            MessageType.JoinAccepted when reply.Sender is not null && reply.UserId is not null =>
                ConnectResult.Succeeded(reply.Sender, reply.UserId, reply.Users ?? Array.Empty<string>()),
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

    private void CompleteConnect(MessageChannel channel, string username, string userId, IReadOnlyList<string> users)
    {
        var cts = new CancellationTokenSource();

        lock (_gate)
        {
            _channel = channel;
            _receiveCts = cts;
            _username = username;
            _userId = userId;
            _shutdownNoticed = false;
            _state = ConnectionState.Connected;
        }

        RaiseStateChanged(ConnectionState.Connected);

        // Başlangıç kullanıcı listesi, sonradan gelecek UserJoined/UserLeft olaylarından önce
        // aynı sıralı kanaldan iletilir; dinleme döngüsü ancak bundan sonra başlar.
        RaiseMessageReceived(new ChatMessage(MessageType.JoinAccepted, Sender: username, UserId: userId, Users: users));

        // Belirteç, görev başlamadan önce alınır: bağlantı hemen kesilirse cts atılmış olabilir ve
        // lambda içinde cts.Token okumak ObjectDisposedException fırlatırdı.
        CancellationToken receiveToken = cts.Token;
        lock (_gate)
        {
            _receiveTask = Task.Run(() => ReceiveLoopAsync(channel, receiveToken));
        }
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
            _userId = null;
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
