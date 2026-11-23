using System.Text;

namespace ChatShared;

/// <summary>
/// Beklenmeyen hataları kullanıcının yerel uygulama verisi klasörüne yazar:
/// %LocalAppData%\ChatApp\logs\{uygulama}-{yyyy-MM-dd}.log. Sohbet mesajı içeriği asla yazılmaz.
/// </summary>
public static class CrashLog
{
    /// <summary>Varsayılan log klasörü.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatApp", "logs");

    /// <summary>
    /// Hatayı log dosyasına ekler ve dosya yolunu döner. Hata anında ikinci bir hata üretmemek için
    /// yazma başarısız olursa null döner; çağıran kullanıcıya yine de bilgi verebilir.
    /// </summary>
    public static string? Write(string appName, Exception exception, string? directory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            string logDirectory = directory ?? DefaultDirectory;
            Directory.CreateDirectory(logDirectory);

            string path = Path.Combine(logDirectory, $"{appName}-{DateTime.Now:yyyy-MM-dd}.log");
            var entry = new StringBuilder()
                .Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ")
                .AppendLine(exception.ToString())
                .AppendLine();

            File.AppendAllText(path, entry.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
