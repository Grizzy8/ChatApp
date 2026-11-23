namespace ChatClient;

/// <summary>RichTextBox'a zaman damgalı ve renkli satırlar ekleyen yardımcı sınıf.</summary>
public sealed class ChatView
{
    private readonly RichTextBox _box;
    private readonly Font _boldFont;

    public ChatView(RichTextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        _box = box;

        // Kalın yazı tipi bir kez üretilir; her satırda yeni Font üretmek GDI nesnesi sızdırırdı.
        _boldFont = new Font(box.Font, FontStyle.Bold);
        box.Disposed += (_, _) => _boldFont.Dispose();
    }

    /// <summary>Bilgi satırı (örn. "Sunucuya bağlandı.").</summary>
    public void AppendSystem(string text) => AppendLine(DateTime.Now, text, ClientConstants.SystemMessageColor);

    /// <summary>Hata satırı.</summary>
    public void AppendError(string text) => AppendLine(DateTime.Now, text, ClientConstants.ErrorMessageColor);

    /// <summary>Sohbet satırı: gönderen adı kalın ve renkli, kendi mesajlarım ile başkalarınınki ayrı renkte.</summary>
    public void AppendChat(DateTime time, string sender, string text, bool isOwn)
    {
        Color senderColor = isOwn ? ClientConstants.OwnMessageColor : ClientConstants.OtherMessageColor;

        AppendSegment($"[{FormatTime(time)}] ", ClientConstants.SystemMessageColor, bold: false);
        AppendSegment($"{sender}: ", senderColor, bold: true);
        AppendSegment(text + Environment.NewLine, _box.ForeColor, bold: false);
        _box.ScrollToCaret();
    }

    /// <summary>Özel mesaj satırı: mor ve "(özel)" etiketli. Kendi mesajım "Sen → alıcı", gelen "gönderen → Sen" biçiminde.</summary>
    public void AppendPrivate(DateTime time, string sender, string recipient, string text, bool isOwn)
    {
        string route = isOwn ? $"Sen → {recipient}" : $"{sender} → Sen";

        AppendSegment($"[{FormatTime(time)}] ", ClientConstants.SystemMessageColor, bold: false);
        AppendSegment($"{route} (özel): ", ClientConstants.PrivateMessageColor, bold: true);
        AppendSegment(text + Environment.NewLine, ClientConstants.PrivateMessageColor, bold: false);
        _box.ScrollToCaret();
    }

    public void AppendLine(DateTime time, string text, Color color)
    {
        AppendSegment($"[{FormatTime(time)}] {text}{Environment.NewLine}", color, bold: false);
        _box.ScrollToCaret();
    }

    private static string FormatTime(DateTime time) => time.ToString(ClientConstants.TimestampFormat);

    private void AppendSegment(string text, Color color, bool bold)
    {
        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;
        _box.SelectionColor = color;
        _box.SelectionFont = bold ? _boldFont : _box.Font;
        _box.AppendText(text);
        _box.SelectionColor = _box.ForeColor;
    }
}
