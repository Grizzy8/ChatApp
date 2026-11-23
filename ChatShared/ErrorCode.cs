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

    ContactLimitReached,

    /// <summary>Alıcının bekleyen mesaj kutusu dolu; çevrimdışı alıcıya yeni mesaj bırakılamaz.</summary>
    RecipientInboxFull,

    GroupNameInvalid,

    /// <summary>Grup yok ya da kullanıcı üyesi değil (ikisi bilerek ayırt edilmez; grupların varlığı sızmasın).</summary>
    GroupNotFound,

    /// <summary>Rolün bu işlem için yetkisi yok.</summary>
    NotAuthorized,

    GroupLimitReached,

    GroupFull,

    AlreadyGroupMember,

    AlreadyInvited,

    InviteLimitReached,

    InviteNotFound,

    /// <summary>Kurucu gruptan ayrılamaz; grubu silmelidir.</summary>
    OwnerCannotLeave,

    /// <summary>İşlem yapılmak istenen üye grupta yok ya da bu işlem ona uygulanamaz.</summary>
    MemberNotFound
}
