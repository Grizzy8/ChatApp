namespace ChatShared;

/// <summary>
/// Sunucunun istemciye bildirdiği hata kodları.
/// Sunucu hazır metin göndermez; Türkçe açıklamayı istemci üretir.
/// </summary>
public enum ErrorCode
{
    None,
    UsernameTaken,
    UsernameInvalid,
    RecipientNotFound,
    MessageTooLong,
    InvalidMessage,
    NotJoined,
    ServerShuttingDown
}
