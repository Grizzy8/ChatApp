using System.Net;
using System.Net.Sockets;
using ChatServer.Storage;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer;

/// <summary>
/// TcpListener ile gelen bağlantıları dinler ve kayıt/giriş el sıkışmasını yürütür.
/// Arayüzden bağımsızdır; olayları event ile dışarı bildirir.
/// Event'ler arka plan thread'lerinden tetiklenebilir.
/// </summary>
public sealed partial class ChatServerHost : IDisposable
{
    private static readonly TimeSpan ShutdownNoticeTimeout = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private readonly ClientRegistry _registry = new();
    private readonly MessageRouter _router;
    private readonly SemaphoreSlim _membershipLock = new(1, 1);
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _connectionCount;

    // Var olmayan kullanıcı için de gerçek bir doğrulama yapılır; böylece yanıt süresine bakarak
    // hangi kullanıcı adlarının kayıtlı olduğu anlaşılamaz.
    private static readonly Lazy<string> DummyPasswordHash = new(() => PasswordHasher.Hash("zamanlama-icin-sahte-parola"));

    private readonly UserRepository _users;
    private readonly ContactRepository _contacts;
    private readonly MessageRepository _messages;
    private readonly GroupRepository _groups;
    private readonly AuthAttemptLimiter _authLimiter;

    public ChatServerHost(UserRepository users, ContactRepository contacts, MessageRepository messages, GroupRepository groups, AuthAttemptLimiter? authLimiter = null)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(groups);
        _users = users;
        _contacts = contacts;
        _messages = messages;
        _groups = groups;
        _authLimiter = authLimiter ?? new AuthAttemptLimiter();

        _router = new MessageRouter(_registry);
        _router.DeliveryFailed += (_, text) => WriteLog(text, ServerLogLevel.Error);
    }

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

                if (Interlocked.Increment(ref _connectionCount) > ProtocolConstants.MaxConnections)
                {
                    Interlocked.Decrement(ref _connectionCount);
                    client.Dispose();
                    WriteLog($"Bağlantı sınırı ({ProtocolConstants.MaxConnections}) dolu; yeni bağlantı reddedildi.", ServerLogLevel.Error);
                    continue;
                }

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
        try
        {
            await RunClientAsync(tcpClient, token).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _connectionCount);
        }
    }

    private async Task RunClientAsync(TcpClient tcpClient, CancellationToken token)
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
            catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException or ObjectDisposedException)
            {
                WriteLog($"{Describe(session)} ile bağlantı koptu: {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog($"{Describe(session)} için beklenmedik hata: {ex.Message}", ServerLogLevel.Error);
            }
            finally
            {
                try
                {
                    await RemoveAndAnnounceAsync(session, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Temizlik sırasındaki bir hata sunucuyu düşürmemeli; yine de kayda geçer.
                    WriteLog($"{Describe(session)} temizlenirken hata: {ex.Message}", ServerLogLevel.Error);
                }
            }
        }
    }

    /// <summary>
    /// Kimlik el sıkışması: ilk mesaj süre içinde gelen geçerli bir Register ya da Login olmalıdır.
    /// Kabul edilirse oturum kaydedilir; aksi halde istemciye sebep bildirilir ve bağlantı kapanır.
    /// Parola hiçbir zaman log'a yazılmaz.
    /// </summary>
    private async Task<bool> TryJoinAsync(ClientSession session, CancellationToken token)
    {
        string? line = await ReadWithTimeoutAsync(session, ProtocolConstants.JoinTimeout, token).ConfigureAwait(false);
        if (line is null)
        {
            WriteLog($"{session.Endpoint} giriş mesajı göndermeden ayrıldı veya süre doldu.");
            return false;
        }

        ChatMessage? message = MessageSerializer.Deserialize(line);
        if (message is null)
        {
            await SendErrorAsync(session, ErrorCode.InvalidMessage, token).ConfigureAwait(false);
            WriteLog($"{session.Endpoint} geçersiz ilk mesaj gönderdi, bağlantı kapatıldı.", ServerLogLevel.Error);
            return false;
        }

        if (message.Type is not (MessageType.Register or MessageType.Login))
        {
            await SendErrorAsync(session, ErrorCode.NotJoined, token).ConfigureAwait(false);
            WriteLog($"{session.Endpoint} giriş yapmadan {message.Type} mesajı gönderdi, bağlantı kapatıldı.", ServerLogLevel.Error);
            return false;
        }

        if (!_authLimiter.TryAcquire(session.Endpoint))
        {
            WriteLog($"{session.Endpoint} çok fazla kayıt/giriş denemesi yaptı, reddedildi.", ServerLogLevel.Error);
            await session.SendAsync(new ChatMessage(MessageType.JoinRejected, ErrorCode: ErrorCode.TooManyAttempts), token).ConfigureAwait(false);
            return false;
        }

        ErrorCode rejection;
        UserRecord? user;
        try
        {
            (rejection, user) = message.Type == MessageType.Register
                ? await RegisterAccountAsync(message, token).ConfigureAwait(false)
                : await AuthenticateAsync(message, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException)
        {
            WriteLog($"{session.Endpoint} için veritabanı hatası: {ex.Message}", ServerLogLevel.Error);
            (rejection, user) = (ErrorCode.ServerError, null);
        }

        if (rejection == ErrorCode.None)
        {
            rejection = await RegisterAndAnnounceAsync(session, user!, token).ConfigureAwait(false);
        }

        if (rejection != ErrorCode.None)
        {
            await session.SendAsync(new ChatMessage(MessageType.JoinRejected, ErrorCode: rejection), token).ConfigureAwait(false);
            WriteLog($"{session.Endpoint} için katılma reddedildi ({rejection}).");
            return false;
        }

        WriteLog($"{session.Username} katıldı ({session.Endpoint}).");
        ClientsChanged?.Invoke(this, EventArgs.Empty);

        // Kişi listesi girişten hemen sonra, JoinAccepted'ın ardından gönderilir.
        await SendContactListAsync(session, token).ConfigureAwait(false);
        await SendGroupStateAsync(session, token).ConfigureAwait(false);
        await DeliverPendingMessagesAsync(session, token).ConfigureAwait(false);
        return true;
    }

    /// <summary>Yeni hesap oluşturur: kuralları doğrular, parolayı hash'ler (CPU işi, thread havuzunda) ve kaydeder.</summary>
    private async Task<(ErrorCode Error, UserRecord? User)> RegisterAccountAsync(ChatMessage message, CancellationToken token)
    {
        ErrorCode problem = UsernameValidator.Validate(message.Sender);
        if (problem == ErrorCode.None)
        {
            problem = PasswordValidator.Validate(message.Password);
        }

        if (problem != ErrorCode.None)
        {
            return (problem, null);
        }

        string password = message.Password!;
        string hash = await Task.Run(() => PasswordHasher.Hash(password), token).ConfigureAwait(false);
        UserRecord? created = await _users.CreateAsync(message.Sender!, hash, token).ConfigureAwait(false);

        return created is null ? (ErrorCode.UsernameTaken, null) : (ErrorCode.None, created);
    }

    /// <summary>
    /// Mevcut hesabı doğrular. Kullanıcı adı yanlış da olsa parola yanlış da olsa aynı hata ve benzer süre
    /// döner; hangisinin yanlış olduğu bilerek söylenmez.
    /// </summary>
    private async Task<(ErrorCode Error, UserRecord? User)> AuthenticateAsync(ChatMessage message, CancellationToken token)
    {
        string? password = message.Password;
        if (UsernameValidator.Validate(message.Sender) != ErrorCode.None
            || string.IsNullOrEmpty(password)
            || password.Length > ProtocolConstants.PasswordMaxLength)
        {
            return (ErrorCode.InvalidCredentials, null);
        }

        UserRecord? user = await _users.FindByUsernameAsync(message.Sender!, token).ConfigureAwait(false);
        string stored = user?.PasswordHash ?? DummyPasswordHash.Value;
        bool isCorrect = await Task.Run(() => PasswordHasher.Verify(password, stored), token).ConfigureAwait(false);

        return user is not null && isCorrect ? (ErrorCode.None, user) : (ErrorCode.InvalidCredentials, null);
    }

    /// <summary>
    /// Kullanıcıyı çevrimiçi olarak kaydeder, JoinAccepted yanıtını gönderir ve diğerlerine UserJoined bildirir.
    /// Katılma ve ayrılma işlemleri sırayla yürütülür; böylece her istemcinin gördüğü kullanıcı listesi
    /// ile sonradan aldığı UserJoined/UserLeft olayları birbiriyle tutarlı kalır.
    /// </summary>
    private async Task<ErrorCode> RegisterAndAnnounceAsync(ClientSession session, UserRecord user, CancellationToken token)
    {
        await _membershipLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            session.AssignUserId(user.Id);
            if (!_registry.TryAdd(user.Username, session))
            {
                return ErrorCode.AccountAlreadyOnline;
            }

            using (var sendCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                sendCts.CancelAfter(MessageRouter.DeliveryTimeout);
                await session.SendAsync(
                    new ChatMessage(
                        MessageType.JoinAccepted, Sender: user.Username, UserId: user.Id,
                        Timestamp: DateTime.Now, Users: _registry.GetUsernames()),
                    sendCts.Token).ConfigureAwait(false);
            }

            session.MarkActive();

            var joined = new ChatMessage(MessageType.UserJoined, Sender: user.Username, Timestamp: DateTime.Now);
            await _router.BroadcastAsync(joined, token, excluded: session).ConfigureAwait(false);
            return ErrorCode.None;
        }
        finally
        {
            _membershipLock.Release();
        }
    }

    /// <summary>Oturumu kayıttan çıkarır; katılımı tamamlanmış bir kullanıcıysa diğerlerine UserLeft bildirir.</summary>
    private async Task RemoveAndAnnounceAsync(ClientSession session, CancellationToken token)
    {
        // Bekleme iptal edilemez: sunucu kapanırken bile kayıt temizliği tamamlanmalıdır.
        // Kilidi tutan işlemler zaman aşımıyla sınırlı olduğu için bekleme sonsuza uzamaz.
        await _membershipLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_registry.Remove(session))
            {
                return;
            }

            WriteLog($"{session.Username} ayrıldı.");
            ClientsChanged?.Invoke(this, EventArgs.Empty);

            if (!session.IsActive || token.IsCancellationRequested)
            {
                return; // Hiç duyurulmamış bir katılım ya da sunucunun kapanışı: bildirilecek kimse yok.
            }

            var left = new ChatMessage(MessageType.UserLeft, Sender: session.Username, Timestamp: DateTime.Now);
            await _router.BroadcastAsync(left, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Sunucu bu sırada kapandı; kalan istemciler zaten kapanış bildirimi alıyor.
        }
        finally
        {
            _membershipLock.Release();
        }
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

            await HandleMessageAsync(session, message, token).ConfigureAwait(false);
        }
    }

    private async Task HandleMessageAsync(ClientSession session, ChatMessage message, CancellationToken token)
    {
        if ((message.Type is MessageType.Chat or MessageType.Private or MessageType.AddContact or MessageType.RemoveContact || IsGroupRequest(message.Type))
            && !session.RateLimiter.TryAcquire())
        {
            await RejectMessageAsync(session, ErrorCode.RateLimited, token).ConfigureAwait(false);
            return;
        }

        switch (message.Type)
        {
            case MessageType.Chat:
                await HandleChatAsync(session, message, token).ConfigureAwait(false);
                break;
            case MessageType.Private:
                await HandlePrivateAsync(session, message, token).ConfigureAwait(false);
                break;
            case MessageType.AddContact or MessageType.RemoveContact:
                await HandleContactRequestAsync(session, message, token).ConfigureAwait(false);
                break;
            case var type when IsGroupRequest(type):
                await HandleGroupRequestAsync(session, message, token).ConfigureAwait(false);
                break;
            default:
                WriteLog($"{session.Username} geçersiz mesaj türü gönderdi ({message.Type}), yok sayıldı.", ServerLogLevel.Error);
                break;
        }
    }

    /// <summary>Genel sohbet mesajını doğrular ve herkese iletir. Gönderen adı istemciden değil, oturumdan alınır.</summary>
    private async Task HandleChatAsync(ClientSession session, ChatMessage message, CancellationToken token)
    {
        ErrorCode problem = ValidateMessageText(message.Text);
        if (problem != ErrorCode.None)
        {
            await RejectMessageAsync(session, problem, token).ConfigureAwait(false);
            return;
        }

        WriteLog($"{session.Username}: {message.Text}");

        var broadcast = new ChatMessage(MessageType.Chat, Sender: session.Username, Text: message.Text, Timestamp: DateTime.Now);
        await _router.BroadcastAsync(broadcast, token).ConfigureAwait(false);
    }

    private static ErrorCode ValidateMessageText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ErrorCode.InvalidMessage;
        }

        return text.Length > ProtocolConstants.MaxMessageLength ? ErrorCode.MessageTooLong : ErrorCode.None;
    }

    /// <summary>Reddedilen mesajı gönderene bildirir ve log'a yazar (mesaj içeriği yazılmaz).</summary>
    private async Task RejectMessageAsync(ClientSession session, ErrorCode reason, CancellationToken token)
    {
        WriteLog($"{session.Username} tarafından gönderilen mesaj reddedildi ({reason}).", ServerLogLevel.Error);
        await SendErrorAsync(session, reason, token).ConfigureAwait(false);
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
