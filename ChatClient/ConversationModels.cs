using ChatShared;

namespace ChatClient;

public enum ConversationKind
{
    Private,
    Group
}

/// <summary>Sohbet penceresinde gösterilen tek mesaj.</summary>
public sealed record ChatEntry(DateTime Time, string Sender, string Text, bool IsOwn);

/// <summary>
/// Sol listedeki bir sohbet (özel ya da grup). Mesajlar ilk gerektiğinde diskten yüklenir.
/// Özel sohbetin anahtarı karşı tarafın kullanıcı adından, grubunki grup ID'sinden üretilir.
/// </summary>
public sealed class Conversation
{
    public Conversation(string key, ConversationKind kind, string title, string? groupId = null)
    {
        Key = key;
        Kind = kind;
        Title = title;
        GroupId = groupId;
    }

    public string Key { get; }

    public ConversationKind Kind { get; }

    /// <summary>Özel sohbette karşı tarafın kullanıcı adı, grupta grubun adı.</summary>
    public string Title { get; set; }

    public string? GroupId { get; }

    /// <summary>Listeyi sıralamak için kullanılan son hareket zamanı (son mesaj ya da gruba katılma anı).</summary>
    public DateTime LastActivity { get; set; } = DateTime.MinValue;

    /// <summary>Okunmamış (başkasından gelen) mesaj sayısı.</summary>
    public int Unread { get; set; }

    /// <summary>Henüz yüklenmediyse null.</summary>
    public List<ChatEntry>? Messages { get; set; }

    public bool IsGroup => Kind == ConversationKind.Group;
}

/// <summary>Sohbet anahtarları: dosya adı olarak da kullanıldığı için güvenli karakterlerle sınırlıdır.</summary>
public static class ConversationKey
{
    public static string ForPrivate(string peerUsername) => "p_" + peerUsername;

    public static string ForGroup(string groupId) => "g_" + groupId;

    /// <summary>Anahtar yalnızca harf, rakam, '_' ve '-' içerir; yol ayracı ya da ".." taşıyamaz.</summary>
    public static bool IsValid(string? key) =>
        key is { Length: > 2 and <= 64 }
        && (key.StartsWith("p_", StringComparison.Ordinal) || key.StartsWith("g_", StringComparison.Ordinal))
        && key.All(c => char.IsLetterOrDigit(c) || c is '_' or '-');
}
