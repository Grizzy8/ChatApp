namespace ChatShared;

/// <summary>Sunucu ile istemci arasındaki mesaj türleri.</summary>
public enum MessageType
{
    /// <summary>İstemci → Sunucu: yeni hesap oluşturma isteği (Sender = kullanıcı adı, Password). Başarılıysa oturum da açılır.</summary>
    Register,

    /// <summary>İstemci → Sunucu: mevcut hesapla giriş isteği (Sender = kullanıcı adı, Password).</summary>
    Login,

    /// <summary>
    /// Sunucu → İstemci: kayıt/giriş kabul edildi ve sohbete katılındı. Sender = hesabın kayıtlı kullanıcı adı,
    /// UserId = hesabın benzersiz kimliği, Users = çevrimiçi kullanıcı listesi.
    /// </summary>
    JoinAccepted,

    /// <summary>Sunucu → İstemci: kayıt/giriş reddedildi, ErrorCode ekli.</summary>
    JoinRejected,

    /// <summary>Genel sohbet mesajı (istemci → sunucu → herkes).</summary>
    Chat,

    /// <summary>Özel mesaj (istemci → sunucu → alıcı ve gönderen).</summary>
    Private,

    /// <summary>Sunucu → herkes: yeni bir kullanıcı katıldı.</summary>
    UserJoined,

    /// <summary>Sunucu → herkes: bir kullanıcı ayrıldı.</summary>
    UserLeft,

    /// <summary>İstemci → Sunucu: kullanıcı bağlantıyı kendi isteğiyle kesiyor.</summary>
    Leave,

    /// <summary>Sunucu → İstemci: bir isteğin işlenemediğini bildirir, ErrorCode ekli.</summary>
    Error
}
