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

    /// <summary>Tek bir sohbet mesajının en fazla karakter sayısı.</summary>
    public const int MaxMessageLength = 500;

    /// <summary>Bozuk veya aşırı büyük veriye karşı, tek bir satırın en fazla karakter sayısı.</summary>
    public const int MaxLineLength = 8 * 1024;

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Bağlanan istemcinin Join mesajını göndermesi için tanınan süre.</summary>
    public static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(10);
}
