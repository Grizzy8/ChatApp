namespace ChatClient;

/// <summary>Yalnızca istemciyi ilgilendiren sabitler (protokol sabitleri ChatShared'dedir).</summary>
public static class ClientConstants
{
    /// <summary>Bağlantıyı keserken sunucuya Leave göndermek için beklenecek en uzun süre.</summary>
    public static readonly TimeSpan LeaveTimeout = TimeSpan.FromMilliseconds(500);

    public const string TimestampFormat = "HH:mm:ss";

    public static readonly Color SystemMessageColor = Color.DimGray;
    public static readonly Color ErrorMessageColor = Color.Firebrick;
}
