using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChatShared;

namespace ChatClient;

/// <summary>Diskte saklanan tek mesaj satırı.</summary>
public sealed record StoredMessage(
    [property: JsonPropertyName("t")] DateTime Time,
    [property: JsonPropertyName("s")] string Sender,
    [property: JsonPropertyName("x")] string Text);

/// <summary>Başlangıçta sol listeyi kurmak için gereken özet.</summary>
public sealed record StoredConversation(string Key, DateTime LastTime, int Unread);

/// <summary>
/// Bir kullanıcının sohbet geçmişi: her sohbet için bir dosya ({kök}\{kullanıcı}\sohbetler\{anahtar}.jsonl, satır başına
/// bir JSON mesajı) ve "en son ne zaman okudum" bilgisini tutan state.json. Mesajlar yalnızca bu bilgisayarda saklanır.
/// </summary>
public sealed class ConversationStore
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _userDirectory;
    private readonly string _conversationsDirectory;
    private readonly string _statePath;
    private readonly string _me;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Dictionary<string, DateTime> _lastRead = new();

    /// <param name="username">Geçerli kullanıcı adı; klasör adı olarak kullanıldığı için doğrulanır.</param>
    public ConversationStore(string username, string? rootDirectory = null)
    {
        if (!UsernameValidator.IsValid(username))
        {
            throw new ArgumentException("Geçmiş klasörü için geçersiz kullanıcı adı.", nameof(username));
        }

        _me = username;
        _userDirectory = Path.Combine(rootDirectory ?? ClientConstants.DefaultHistoryRoot, username);
        _conversationsDirectory = Path.Combine(_userDirectory, "sohbetler");
        _statePath = Path.Combine(_userDirectory, "state.json");
        LoadState();
    }

    /// <summary>Diskteki tüm sohbetlerin özeti (son mesaj zamanı ve okunmamış sayısı).</summary>
    public IReadOnlyList<StoredConversation> LoadIndex()
    {
        if (!Directory.Exists(_conversationsDirectory))
        {
            return Array.Empty<StoredConversation>();
        }

        var result = new List<StoredConversation>();
        foreach (string path in Directory.EnumerateFiles(_conversationsDirectory, "*.jsonl"))
        {
            string key = Path.GetFileNameWithoutExtension(path);
            if (!ConversationKey.IsValid(key))
            {
                continue;
            }

            DateTime lastRead = _lastRead.GetValueOrDefault(key, DateTime.MinValue);
            DateTime lastTime = DateTime.MinValue;
            int unread = 0;
            foreach (StoredMessage message in ReadFile(path))
            {
                lastTime = message.Time > lastTime ? message.Time : lastTime;
                if (message.Time > lastRead && !string.Equals(message.Sender, _me, StringComparison.OrdinalIgnoreCase))
                {
                    unread++;
                }
            }

            result.Add(new StoredConversation(key, lastTime, unread));
        }

        return result;
    }

    /// <summary>Sohbetin son <paramref name="maxMessages"/> mesajını eskiden yeniye döner.</summary>
    public IReadOnlyList<StoredMessage> ReadMessages(string key, int maxMessages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMessages);

        string path = GetPath(key);
        if (!File.Exists(path))
        {
            return Array.Empty<StoredMessage>();
        }

        var last = new Queue<StoredMessage>(maxMessages);
        foreach (StoredMessage message in ReadFile(path))
        {
            if (last.Count == maxMessages)
            {
                last.Dequeue();
            }

            last.Enqueue(message);
        }

        return last.ToList();
    }

    /// <summary>
    /// Mesajı sohbet dosyasının sonuna ekler. Tek satırlık yazma olduğundan eşzamanlıdır; böylece hemen ardından
    /// yapılan bir okuma yarım kalmış yazmayla karşılaşmaz. Disk hatası fırlatılır; çağıran sohbeti durdurmadan bildirmelidir.
    /// </summary>
    public void Append(string key, StoredMessage message)
    {
        string path = GetPath(key);
        string line = JsonSerializer.Serialize(message);

        _writeLock.Wait();
        try
        {
            Directory.CreateDirectory(_conversationsDirectory);
            File.AppendAllText(path, line + Environment.NewLine, Utf8NoBom);
        }
        finally
        {
            _writeLock.Release();
        }
    }
    /// <summary>Sohbetin "en son okunma" zamanını günceller ve state.json'a yazar.</summary>
    public void MarkRead(string key, DateTime time)
    {
        if (!ConversationKey.IsValid(key))
        {
            throw new ArgumentException("Geçersiz sohbet anahtarı.", nameof(key));
        }

        _writeLock.Wait();
        try
        {
            _lastRead[key] = time;
            Directory.CreateDirectory(_userDirectory);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(_lastRead.ToDictionary(p => p.Key, p => p.Value)), Utf8NoBom);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public string GetPath(string key)
    {
        if (!ConversationKey.IsValid(key))
        {
            throw new ArgumentException("Geçersiz sohbet anahtarı.", nameof(key));
        }

        return Path.Combine(_conversationsDirectory, key + ".jsonl");
    }

    public static bool IsFileError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException or JsonException;

    private void LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(_statePath, Utf8NoBom));
                foreach ((string key, DateTime time) in loaded ?? new())
                {
                    if (ConversationKey.IsValid(key))
                    {
                        _lastRead[key] = time;
                    }
                }
            }
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            // Bozuk durum dosyası yalnızca "okunmamış" bilgisini sıfırlar; geçmiş etkilenmez.
        }
    }

    // Bozuk satırlar (yarım yazılmış, elle değiştirilmiş) atlanır; geri kalan geçmiş okunabilir kalır.
    private static IEnumerable<StoredMessage> ReadFile(string path)
    {
        foreach (string line in File.ReadLines(path, Utf8NoBom))
        {
            if (line.Length == 0)
            {
                continue;
            }

            StoredMessage? message = null;
            try
            {
                message = JsonSerializer.Deserialize<StoredMessage>(line);
            }
            catch (JsonException)
            {
            }

            if (message is { Sender: not null, Text: not null })
            {
                yield return message;
            }
        }
    }
}
