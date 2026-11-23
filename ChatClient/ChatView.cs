namespace ChatClient;

/// <summary>RichTextBox'a zaman damgalı ve renkli satırlar ekleyen yardımcı sınıf.</summary>
public sealed class ChatView
{
    private readonly RichTextBox _box;

    public ChatView(RichTextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        _box = box;
    }

    /// <summary>Bilgi satırı (örn. "Sunucuya bağlandı.").</summary>
    public void AppendSystem(string text) => AppendLine(DateTime.Now, text, ClientConstants.SystemMessageColor);

    /// <summary>Hata satırı.</summary>
    public void AppendError(string text) => AppendLine(DateTime.Now, text, ClientConstants.ErrorMessageColor);

    public void AppendLine(DateTime time, string text, Color color)
    {
        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;
        _box.SelectionColor = color;
        _box.AppendText($"[{time.ToString(ClientConstants.TimestampFormat)}] {text}{Environment.NewLine}");
        _box.SelectionColor = _box.ForeColor;
        _box.ScrollToCaret();
    }
}
