using System.Net.Sockets;
using System.Text;
using ChatShared;

namespace ChatServer;

/// <summary>
/// Sunucuya bağlanan tek bir istemcinin bağlantısı: satır okuma ve mesaj gönderme.
/// Gönderme işlemleri birbirine karışmaması için sıraya sokulur.
/// </summary>
public sealed class ClientSession : IDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly TcpClient _tcpClient;
    private readonly BoundedLineReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ClientSession(TcpClient tcpClient)
    {
        ArgumentNullException.ThrowIfNull(tcpClient);

        _tcpClient = tcpClient;
        Endpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "bilinmiyor";
        ConnectedAt = DateTime.Now;

        NetworkStream stream = tcpClient.GetStream();
        _reader = new BoundedLineReader(
            new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: false),
            ProtocolConstants.MaxLineLength);
        _writer = new StreamWriter(stream, Utf8NoBom) { AutoFlush = true, NewLine = "\n" };
    }

    /// <summary>Join kabul edilene kadar null kalır.</summary>
    public string? Username { get; private set; }

    /// <summary>İstemcinin IP:port bilgisi.</summary>
    public string Endpoint { get; }

    public DateTime ConnectedAt { get; }

    public void AssignUsername(string username)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        Username = username;
    }

    /// <summary>Bir satır okur; bağlantı kapandıysa null döner.</summary>
    public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
        _reader.ReadLineAsync(cancellationToken);

    /// <summary>Mesajı tek satır JSON olarak gönderir.</summary>
    public async Task SendAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        string line = MessageSerializer.Serialize(message);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose() => _tcpClient.Dispose();
}
