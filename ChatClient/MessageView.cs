using System.Globalization;
using System.Runtime.InteropServices;

namespace ChatClient;

/// <summary>
/// Sohbet penceresi: RichTextBox üzerinde, gelen mesajlar sola, kendi mesajlarım sağa hizalı gösterilir.
/// RichTextBox seçilip kopyalanabilir ve kaydırılabilir olduğu için baloncuklar özel çizilmez;
/// paragraf hizası, girinti ve metin arka planıyla baloncuk görünümü verilir.
/// </summary>
public sealed class MessageView
{
    private const int SetRedraw = 0x000B;
    private const int SideMargin = 14;
    private const int FarMargin = 110;
    private const int SameSenderGapMinutes = 5;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Color OwnBubble = Color.FromArgb(67, 78, 210);

    private readonly RichTextBox _box;
    private readonly Font _bold;
    private readonly Font _small;
    private readonly Font _tiny;

    public MessageView(RichTextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        _box = box;
        _bold = new Font(box.Font, FontStyle.Bold);
        _small = new Font(box.Font.FontFamily, box.Font.Size - 1.5f);
        _tiny = new Font(box.Font.FontFamily, 3f);
        box.Disposed += (_, _) =>
        {
            _bold.Dispose();
            _small.Dispose();
            _tiny.Dispose();
        };
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    /// <summary>Toplu çizim sırasında ekran güncellemesini durdurur (titremeyi önler); <see cref="EndBulk"/> ile bitirilir.</summary>
    public void BeginBulk()
    {
        if (_box.IsHandleCreated)
        {
            SendMessage(_box.Handle, SetRedraw, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void EndBulk()
    {
        ScrollToEnd();
        if (_box.IsHandleCreated)
        {
            SendMessage(_box.Handle, SetRedraw, new IntPtr(1), IntPtr.Zero);
            _box.Invalidate();
        }
    }

    public void Clear() => _box.Clear();

    /// <summary>
    /// Mesajı ekler. Gün değiştiyse önce tarih ayracı; gönderen değişmişse ya da arada uzun süre geçmişse
    /// ad/saat başlığı da eklenir. <paramref name="showSender"/> yalnızca gruplarda başkasının adını gösterir.
    /// </summary>
    public void AppendMessage(ChatEntry entry, ChatEntry? previous, bool showSender)
    {
        // Mesaj zamanları UTC tutulur; gün ayracı ve saat kullanıcının yerel saatine göre gösterilir.
        DateTime entryLocal = entry.Time.ToLocalTime();
        DateTime? previousLocal = previous?.Time.ToLocalTime();

        if (previousLocal is null || previousLocal.Value.Date != entryLocal.Date)
        {
            AppendCentered(entryLocal.ToString("d MMMM yyyy", Turkish), Theme.Muted);
        }

        bool sameRun = previous is not null
            && previousLocal!.Value.Date == entryLocal.Date
            && string.Equals(previous.Sender, entry.Sender, StringComparison.OrdinalIgnoreCase)
            && (entry.Time - previous.Time).TotalMinutes < SameSenderGapMinutes;

        int indent = entry.IsOwn ? FarMargin : SideMargin;
        int rightIndent = entry.IsOwn ? SideMargin : FarMargin;
        HorizontalAlignment align = entry.IsOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        if (!sameRun)
        {
            AppendSpacer();
            int start = _box.TextLength;
            if (showSender && !entry.IsOwn)
            {
                AppendRun(entry.Sender, Theme.ColorFor(entry.Sender), _bold);
                AppendRun("  ", Theme.Muted, _small);
            }

            AppendRun(entryLocal.ToString("HH:mm", Turkish), Theme.Muted, _small);
            EndParagraph(start, align, indent, rightIndent);
        }

        int bodyStart = _box.TextLength;
        AppendRun($"\u00A0{entry.Text}\u00A0", Color.White, _box.Font, entry.IsOwn ? OwnBubble : Theme.OtherBubble);
        EndParagraph(bodyStart, align, indent, rightIndent);
    }

    /// <summary>Ortalanmış bilgi satırı (örn. "Ayşe çevrimdışı; mesajınız giriş yapınca iletilecek.").</summary>
    public void AppendNotice(string text, bool isError = false)
    {
        AppendSpacer();
        AppendCentered(text, isError ? Theme.ErrorText : Theme.Muted);
    }

    public void ScrollToEnd()
    {
        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;
        _box.ScrollToCaret();
    }

    private void AppendCentered(string text, Color color)
    {
        int start = _box.TextLength;
        AppendRun(text, color, _small);
        EndParagraph(start, HorizontalAlignment.Center, SideMargin, SideMargin);
    }

    // Mesajlar arasında küçük boşluk: çok küçük yazı tipli boş paragraf.
    private void AppendSpacer()
    {
        int start = _box.TextLength;
        AppendRun(string.Empty, Theme.Muted, _tiny);
        EndParagraph(start, HorizontalAlignment.Left, SideMargin, SideMargin);
    }

    private void AppendRun(string text, Color color, Font font, Color? back = null)
    {
        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;
        _box.SelectionColor = color;
        _box.SelectionFont = font;
        _box.SelectionBackColor = back ?? _box.BackColor;
        _box.AppendText(text);
    }

    // Paragrafı bitirir (satır sonu ekler) ve hizayı/girintiyi yalnızca bu paragrafa uygular.
    private void EndParagraph(int start, HorizontalAlignment align, int indent, int rightIndent)
    {
        int length = _box.TextLength - start;
        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;
        _box.SelectionBackColor = _box.BackColor;
        _box.AppendText("\n");

        _box.Select(start, length);
        _box.SelectionAlignment = align;
        _box.SelectionIndent = Scale(indent);
        _box.SelectionRightIndent = Scale(rightIndent);
        _box.Select(_box.TextLength, 0);
    }

    private int Scale(int pixels) => (int)Math.Round(pixels * _box.DeviceDpi / 96.0);
}
