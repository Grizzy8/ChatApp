namespace ChatClient;

/// <summary>İstemcinin sunucuyla bağlantı durumu.</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected
}

/// <summary>Kurulmuş bir bağlantının neden sona erdiği.</summary>
public enum DisconnectReason
{
    /// <summary>Kullanıcı Bağlantıyı Kes'e bastı.</summary>
    UserRequested,

    /// <summary>Sunucu kapanacağını bildirdi.</summary>
    ServerShuttingDown,

    /// <summary>Sunucu bağlantıyı bir bildirim olmadan kapattı.</summary>
    ServerClosed,

    /// <summary>Ağ hatası nedeniyle bağlantı koptu.</summary>
    ConnectionLost
}

/// <summary>Bağlantı sona erdiğinde kullanıcıya gösterilecek bilgi.</summary>
public sealed record DisconnectInfo(DisconnectReason Reason, string Message);

/// <summary>Bağlanırken mevcut hesapla mı giriş yapılacağı, yoksa yeni hesap mı oluşturulacağı.</summary>
public enum AuthMode
{
    Login,
    Register
}

/// <summary>
/// ConnectAsync sonucu. Başarısızlık sebebi kullanıcıya gösterilebilir Türkçe metindir.
/// Başarıda Username hesabın sunucuda kayıtlı yazımıdır (kullanıcı "ALI" yazsa da kayıt "Ali" ise "Ali").
/// </summary>
public sealed record ConnectResult(
    bool Success, string? Error = null, IReadOnlyList<string>? Users = null, string? Username = null, string? UserId = null)
{
    public static ConnectResult Succeeded(string username, string userId, IReadOnlyList<string> users) =>
        new(true, Users: users, Username: username, UserId: userId);

    public static ConnectResult Failed(string error) => new(false, error);
}
