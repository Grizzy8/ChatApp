namespace ChatShared;

/// <summary>Sunucu ve istemcinin ortak kullandığı protokol kuralları ve sınırlar.</summary>
public static class ProtocolConstants
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 5000;

    public const int MinPort = 1;
    public const int MaxPort = 65535;

    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 20;

    public const int PasswordMinLength = 8;

    /// <summary>Üst sınır, aşırı uzun parolalarla sunucuyu yormayı önler.</summary>
    public const int PasswordMaxLength = 64;

    /// <summary>Aynı IP adresinden pencere süresi içinde yapılabilecek en fazla kayıt/giriş denemesi.</summary>
    public const int MaxAuthAttemptsPerWindow = 10;

    public static readonly TimeSpan AuthAttemptWindow = TimeSpan.FromMinutes(1);

    /// <summary>Bir kullanıcının kişi listesinde tutabileceği en fazla kişi.</summary>
    public const int MaxContacts = 200;

    /// <summary>Kişi eklerken yazılan kullanıcı adı/ID metninin en fazla uzunluğu.</summary>
    public const int MaxContactQueryLength = 40;

    /// <summary>Tek bir sohbet mesajının en fazla karakter sayısı.</summary>
    public const int MaxMessageLength = 500;

    /// <summary>Bozuk veya aşırı büyük veriye karşı, tek bir satırın en fazla karakter sayısı.</summary>
    public const int MaxLineLength = 8 * 1024;

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Bir istemcinin pencere süresi içinde gönderebileceği en fazla sohbet/özel mesaj sayısı.</summary>
    public const int MaxMessagesPerWindow = 20;

    public static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(5);

    /// <summary>Sunucunun aynı anda kabul ettiği en fazla bağlantı (Join'e kadar olanlar dahil).</summary>
    public const int MaxConnections = 100;

    /// <summary>Bağlanan istemcinin Join mesajını göndermesi için tanınan süre.</summary>
    public static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(10);
}
