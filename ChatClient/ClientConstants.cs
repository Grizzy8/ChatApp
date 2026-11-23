namespace ChatClient;

/// <summary>Yalnızca istemciyi ilgilendiren sabitler (protokol sabitleri ChatShared'dedir).</summary>
public static class ClientConstants
{
    /// <summary>Bağlantıyı keserken sunucuya Leave göndermek için beklenecek en uzun süre.</summary>
    public static readonly TimeSpan LeaveTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>Bir sohbet açılırken diskten okunacak ve bellekte tutulacak en fazla mesaj sayısı.</summary>
    public const int MaxMessagesInMemory = 300;

    /// <summary>Kullanıcı aramasında yazmayı bıraktıktan sonra sunucuya sorulmadan önce beklenen süre (ms).</summary>
    public const int SearchDebounceMs = 400;

    /// <summary>Geçici bildirimin ekranda kalma süresi (ms).</summary>
    public const int NoticeDurationMs = 7000;

    /// <summary>Varsayılan geçmiş klasörü: Belgeler\ChatApp\History.</summary>
    public static readonly string DefaultHistoryRoot = Path.Combine(GetBaseFolder(), "ChatApp", "History");

    // Belgeler klasörü bulunamayan nadir sistemlerde geçmiş göreli yola düşmesin diye yedek klasör kullanılır.
    private static string GetBaseFolder()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrEmpty(documents)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : documents;
    }
}
