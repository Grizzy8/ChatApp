using System.Net.Sockets;
using System.Text;

namespace ChatShared;

/// <summary>
/// Bir TcpClient üzerinden satır tabanlı JSON mesajlaşma kanalı. Sunucu ve istemci aynı
/// kodlama, satır sonu ve satır uzunluğu kurallarını kullansın diye ortak tutulur.
/// Gönderme işlemleri birbirine karışmaması için sıraya sokulur.
/// </summary>
public sealed class MessageChannel : IDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly TcpClient _tcpClient;
    private readonly BoundedLineReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public MessageChannel(TcpClient tcpClient)
    {
        ArgumentNullException.ThrowIfNull(tcpClient);

        _tcpClient = tcpClient;
        RemoteEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "bilinmiyor";

        NetworkStream stream = tcpClient.GetStream();
        _reader = new BoundedLineReader(
            new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: false),
            ProtocolConstants.MaxLineLength);
        _writer = new StreamWriter(stream, Utf8NoBom) { AutoFlush = true, NewLine = "\n" };
    }

    /// <summary>Karşı tarafın IP:port bilgisi.</summary>
    public string RemoteEndpoint { get; }

    /// <summary>Bir satır okur; bağlantı kapandıysa null döner. Satır çok uzunsa InvalidDataException fırlatır.</summary>
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
