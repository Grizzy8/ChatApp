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
    ServerShuttingDown,
    RateLimited,
    PasswordInvalid,

    /// <summary>Kullanıcı adı veya parola yanlış (hangisinin yanlış olduğu bilerek söylenmez).</summary>
    InvalidCredentials,

    /// <summary>Hesap şu anda başka bir yerde çevrimiçi.</summary>
    AccountAlreadyOnline,

    /// <summary>Kısa sürede çok fazla kayıt/giriş denemesi yapıldı.</summary>
    TooManyAttempts,

    /// <summary>Sunucu tarafında beklenmeyen bir sorun (ör. veritabanı) oluştu.</summary>
    ServerError,

    /// <summary>Eklenmek istenen kullanıcı adı/ID ile eşleşen bir hesap yok.</summary>
    ContactNotFound,

    ContactAlreadyExists,

    CannotAddSelf,

    ContactLimitReached
}
