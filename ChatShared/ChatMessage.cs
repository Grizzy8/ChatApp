using System.Text.Json.Serialization;

namespace ChatShared;

/// <summary>
/// Protokoldeki tek bir mesaj. Türüne göre yalnızca ilgili alanlar doldurulur,
/// diğerleri null kalır (serileştirmede yazılmaz).
/// </summary>
/// <param name="Type">Mesaj türü.</param>
/// <param name="Sender">Gönderen kullanıcı adı.</param>
/// <param name="Recipient">Özel mesajın alıcısı.</param>
/// <param name="Text">Mesaj metni.</param>
/// <param name="Timestamp">Sunucunun atadığı zaman damgası.</param>
/// <param name="Users">Çevrimiçi kullanıcı listesi (JoinAccepted).</param>
/// <param name="ErrorCode">Hata kodu (JoinRejected, Error).</param>
public sealed record ChatMessage(
    [property: JsonRequired] MessageType Type,
    string? Sender = null,
    string? Recipient = null,
    string? Text = null,
    DateTime? Timestamp = null,
    IReadOnlyList<string>? Users = null,
    ErrorCode? ErrorCode = null);
