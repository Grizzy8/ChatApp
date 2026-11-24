using System.Text;
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
/// <param name="Timestamp">Sunucunun atadığı zaman damgası (UTC). İstemci göstermeden önce yerel saate çevirir.</param>
/// <param name="Users">Çevrimiçi kullanıcı listesi (JoinAccepted).</param>
/// <param name="ErrorCode">Hata kodu (JoinRejected, Error).</param>
/// <param name="Password">Parola (yalnızca Register/Login isteğinde). ToString'e ve log'a asla yazılmaz.</param>
/// <param name="UserId">Hesabın benzersiz kimliği (JoinAccepted).</param>
/// <param name="Contacts">Kişi listesi (ContactList).</param>
/// <param name="Groups">Üyesi olunan gruplar (GroupList).</param>
/// <param name="Invites">Bekleyen davetler (InviteList).</param>
/// <param name="Members">Bir grubun üyeleri (GroupMembers).</param>
public sealed record ChatMessage(
    [property: JsonRequired] MessageType Type,
    string? Sender = null,
    string? Recipient = null,
    string? Text = null,
    DateTime? Timestamp = null,
    IReadOnlyList<string>? Users = null,
    ErrorCode? ErrorCode = null,
    string? Password = null,
    string? UserId = null,
    IReadOnlyList<ContactInfo>? Contacts = null,
    IReadOnlyList<GroupInfo>? Groups = null,
    IReadOnlyList<GroupInviteInfo>? Invites = null,
    IReadOnlyList<GroupMemberInfo>? Members = null)
{
    // Record'un otomatik ToString'i tüm alanları yazar; bir hata ayıklama çıktısı ya da log satırı
    // parolayı ifşa etmesin diye Password bu çıktıdan çıkarılır.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Type = ").Append(Type);
        if (Sender is not null) builder.Append(", Sender = ").Append(Sender);
        if (Recipient is not null) builder.Append(", Recipient = ").Append(Recipient);
        if (ErrorCode is not null) builder.Append(", ErrorCode = ").Append(ErrorCode);
        if (UserId is not null) builder.Append(", UserId = ").Append(UserId);
        return true;
    }
}
