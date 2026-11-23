namespace ChatClient;

/// <summary>Yalnızca istemciyi ilgilendiren sabitler (protokol sabitleri ChatShared'dedir).</summary>
public static class ClientConstants
{
    /// <summary>Bağlantıyı keserken sunucuya Leave göndermek için beklenecek en uzun süre.</summary>
    public static readonly TimeSpan LeaveTimeout = TimeSpan.FromMilliseconds(500);

    public const string TimestampFormat = "HH:mm:ss";

    public static readonly Color SystemMessageColor = Color.DimGray;
    public static readonly Color ErrorMessageColor = Color.Firebrick;
    public static readonly Color OwnMessageColor = Color.FromArgb(0, 90, 170);
    public static readonly Color OtherMessageColor = Color.FromArgb(30, 110, 60);
    public static readonly Color PrivateMessageColor = Color.FromArgb(125, 55, 160);
}
