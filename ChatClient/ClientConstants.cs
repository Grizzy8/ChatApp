namespace ChatClient;

/// <summary>Yalnızca istemciyi ilgilendiren sabitler (protokol sabitleri ChatShared'dedir).</summary>
public static class ClientConstants
{
    /// <summary>Bağlantıyı keserken sunucuya Leave göndermek için beklenecek en uzun süre.</summary>
    public static readonly TimeSpan LeaveTimeout = TimeSpan.FromMilliseconds(500);

    public const string TimestampFormat = "HH:mm:ss";

    public const string HistoryFileDateFormat = "yyyy-MM-dd";

    /// <summary>Bağlanınca ekrana yüklenecek en fazla geçmiş satırı sayısı.</summary>
    public const int MaxHistoryLinesToLoad = 500;

    /// <summary>Varsayılan geçmiş klasörü: Belgeler\ChatApp\History.</summary>
    public static readonly string DefaultHistoryRoot = Path.Combine(GetBaseFolder(), "ChatApp", "History");

    public static readonly Color SystemMessageColor = Color.DimGray;
    public static readonly Color ErrorMessageColor = Color.Firebrick;
    public static readonly Color OwnMessageColor = Color.FromArgb(0, 90, 170);
    public static readonly Color OtherMessageColor = Color.FromArgb(30, 110, 60);
    public static readonly Color PrivateMessageColor = Color.FromArgb(125, 55, 160);
    public static readonly Color HistoryMessageColor = Color.FromArgb(150, 150, 150);
    public static readonly Color WarningColor = Color.FromArgb(180, 90, 0);

    // Belgeler klasörü bulunamayan nadir sistemlerde geçmiş göreli yola düşmesin diye yedek klasör kullanılır.
    private static string GetBaseFolder()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrEmpty(documents)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : documents;
    }
}
