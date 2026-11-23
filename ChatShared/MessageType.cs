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

    /// <summary>İstemci → Sunucu: kişi ekleme isteği. Text = kullanıcı adı ya da kimlik (ID).</summary>
    AddContact,

    /// <summary>İstemci → Sunucu: kişiyi silme isteği. Text = kişinin kimliği (ID).</summary>
    RemoveContact,

    /// <summary>Sunucu → İstemci: kişi listesinin tamamı (Contacts). Girişte ve her değişiklikten sonra gönderilir.</summary>
    ContactList,

    /// <summary>Sunucu → İstemci: kayıt/giriş reddedildi, ErrorCode ekli.</summary>
    JoinRejected,

    /// <summary>Genel sohbet mesajı (istemci → sunucu → herkes).</summary>
    Chat,

    /// <summary>Özel mesaj (istemci → sunucu → alıcı ve gönderen).</summary>
    Private,

    /// <summary>
    /// Sunucu → gönderen: alıcı çevrimdışıydı, özel mesaj sunucuda bekletiliyor (alanlar Private ile aynı).
    /// Alıcı bir sonraki girişinde mesajı normal Private olarak alır.
    /// </summary>
    PrivateQueued,

    /// <summary>Sunucu → herkes: yeni bir kullanıcı katıldı.</summary>
    UserJoined,

    /// <summary>Sunucu → herkes: bir kullanıcı ayrıldı.</summary>
    UserLeft,

    /// <summary>İstemci → Sunucu: kullanıcı bağlantıyı kendi isteğiyle kesiyor.</summary>
    Leave,

    /// <summary>Sunucu → İstemci: bir isteğin işlenemediğini bildirir, ErrorCode ekli.</summary>
    Error
}
