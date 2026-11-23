namespace ChatShared;

/// <summary>Sunucu ile istemci arasındaki mesaj türleri.</summary>
public enum MessageType
{
    /// <summary>İstemci → Sunucu: kullanıcı adıyla katılma isteği.</summary>
    Join,

    /// <summary>Sunucu → İstemci: katılma kabul edildi, çevrimiçi kullanıcı listesi ekli.</summary>
    JoinAccepted,

    /// <summary>Sunucu → İstemci: katılma reddedildi, ErrorCode ekli.</summary>
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
