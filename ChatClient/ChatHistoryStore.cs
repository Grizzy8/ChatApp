using System.Text;
using ChatShared;

namespace ChatClient;

/// <summary>
/// Bir kullanıcının sohbet geçmişini günlük metin dosyalarına yazar ve okur:
/// {kök}\{kullanıcı}\{yyyy-MM-dd}.txt. Her satır ekranda görünen biçimin aynısıdır.
/// </summary>
public sealed class ChatHistoryStore
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _userDirectory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <param name="username">Geçerli bir sohbet kullanıcı adı; klasör adı olarak kullanıldığı için doğrulanır.</param>
    /// <param name="rootDirectory">Geçmiş kök klasörü; verilmezse Belgeler\ChatApp\History kullanılır.</param>
    public ChatHistoryStore(string username, string? rootDirectory = null)
    {
        // Doğrulanmış ad yalnızca harf, rakam ve '_' içerdiği için yol ayracı veya ".." taşıyamaz.
        if (!UsernameValidator.IsValid(username))
        {
            throw new ArgumentException("Geçmiş klasörü için geçersiz kullanıcı adı.", nameof(username));
        }

        _userDirectory = Path.Combine(rootDirectory ?? ClientConstants.DefaultHistoryRoot, username);
    }

    /// <summary>Verilen günün geçmiş dosyasının yolu.</summary>
    public string GetFilePath(DateTime date) =>
        Path.Combine(_userDirectory, date.ToString(ClientConstants.HistoryFileDateFormat) + ".txt");

    /// <summary>Genel sohbet satırı: "[14:02:11] ali: merhaba".</summary>
    public static string FormatChat(DateTime time, string sender, string text) =>
        $"[{time.ToString(ClientConstants.TimestampFormat)}] {sender}: {SingleLine(text)}";

    /// <summary>Özel mesaj satırı: "[14:02:11] Sen → Ayşe (özel): selam".</summary>
    public static string FormatPrivate(DateTime time, string sender, string recipient, string text, bool isOwn)
    {
        string route = isOwn ? $"Sen → {recipient}" : $"{sender} → Sen";
        return $"[{time.ToString(ClientConstants.TimestampFormat)}] {route} (özel): {SingleLine(text)}";
    }

    /// <summary>
    /// Satırı, zaman damgasının gününe ait dosyanın sonuna ekler (klasör yoksa oluşturur).
    /// Yazma hatalarında IOException, UnauthorizedAccessException vb. fırlatır; çağıran sohbeti durdurmadan bildirmelidir.
    /// </summary>
    public async Task AppendAsync(DateTime time, string line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_userDirectory);

            await using var stream = new FileStream(
                GetFilePath(time), FileMode.Append, FileAccess.Write, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
            await using var writer = new StreamWriter(stream, Utf8NoBom);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Verilen günün son satırlarını okur. Dosya yoksa boş liste döner.</summary>
    public IReadOnlyList<string> ReadLastLines(DateTime date, int maxLines)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLines);

        string path = GetFilePath(date);
        if (!File.Exists(path))
        {
            return Array.Empty<string>();
        }

        var lastLines = new Queue<string>(maxLines);
        foreach (string line in File.ReadLines(path, Utf8NoBom))
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (lastLines.Count == maxLines)
            {
                lastLines.Dequeue();
            }

            lastLines.Enqueue(line);
        }

        return lastLines.ToList();
    }

    // Dosya satır tabanlıdır; mesaj metnindeki satır sonları tek satırı bozmasın.
    private static string SingleLine(string text) => text.Replace('\r', ' ').Replace('\n', ' ');
}
