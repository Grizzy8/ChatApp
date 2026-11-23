using System.Net;
using System.Net.Sockets;
using ChatShared;

namespace ChatServer;

/// <summary>
/// TcpListener ile gelen bağlantıları dinler. Arayüzden bağımsızdır;
/// olayları event ile dışarı bildirir. Event'ler arka plan thread'inden tetiklenebilir.
/// </summary>
public sealed class ChatServerHost : IDisposable
{
    private readonly object _gate = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    /// <summary>Sunucu bir log kaydı üretti.</summary>
    public event EventHandler<ServerLogEntry>? LogWritten;

    /// <summary>Sunucu durdu (kullanıcı isteğiyle veya hata nedeniyle).</summary>
    public event EventHandler? Stopped;

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

    /// <summary>Dinlemeyi durdurur. Zaten durmuşsa hiçbir şey yapmaz.</summary>
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
                HandleAcceptedClient(client);
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

    private void HandleAcceptedClient(TcpClient client)
    {
        string endpoint = client.Client.RemoteEndPoint?.ToString() ?? "bilinmiyor";
        WriteLog($"Bağlantı kabul edildi: {endpoint}");

        // Oturum yönetimi (Join el sıkışması, ClientSession) bir sonraki adımda eklenecek.
        client.Dispose();
    }

    private void WriteLog(string message, ServerLogLevel level = ServerLogLevel.Info)
    {
        LogWritten?.Invoke(this, new ServerLogEntry(DateTime.Now, message, level));
    }
}
