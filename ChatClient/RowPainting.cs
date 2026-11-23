using System.Drawing.Drawing2D;

namespace ChatClient;

/// <summary>Liste satırlarının ortak çizimi: seçili/üzerinde durulan arka plan, avatar ve metin.</summary>
public static class RowPainting
{
    /// <summary>Mantıksal (96 DPI) pikseli, çizimin yapıldığı ekranın DPI'ına göre ölçekler.</summary>
    public static int S(this Graphics g, int pixels) => (int)Math.Round(pixels * g.DpiX / 96f);

    public static bool IsSelected(DrawItemEventArgs e) => (e.State & DrawItemState.Selected) != 0;

    /// <summary>Seçili ya da fare üstündeki satırın yuvarlak köşeli arka planını çizer.</summary>
    public static void Background(DrawItemEventArgs e, bool hover)
    {
        if (!IsSelected(e) && !hover)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(e.Bounds.X + e.Graphics.S(6), e.Bounds.Y + e.Graphics.S(1), e.Bounds.Width - e.Graphics.S(12), e.Bounds.Height - e.Graphics.S(2));
        using GraphicsPath path = RoundedRectangle(area, e.Graphics.S(6));
        using var brush = new SolidBrush(IsSelected(e) ? Theme.Selected : Theme.Hover);
        e.Graphics.FillPath(brush, path);
    }

    /// <summary>Baş harfli yuvarlak avatar. <paramref name="online"/> null değilse köşesine durum noktası konur.</summary>
    public static void Avatar(Graphics g, Rectangle bounds, string name, Color color, bool? online, Color ringColor)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(color))
        {
            g.FillEllipse(brush, bounds);
        }

        string initial = name.Length > 0 ? char.ToUpper(name[0]).ToString() : "?";
        using var font = new Font("Segoe UI", bounds.Height * 0.36f, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, initial, font, bounds, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (online is { } isOnline)
        {
            int dot = bounds.Width / 3;
            var dotBounds = new Rectangle(bounds.Right - dot, bounds.Bottom - dot, dot, dot);
            using var ring = new SolidBrush(ringColor);
            g.FillEllipse(ring, Rectangle.Inflate(dotBounds, g.S(2), g.S(2)));
            using var fill = new SolidBrush(isOnline ? Theme.Online : Theme.Muted);
            g.FillEllipse(fill, dotBounds);
            if (!isOnline)
            {
                using var hole = new SolidBrush(ringColor);
                g.FillEllipse(hole, Rectangle.Inflate(dotBounds, -g.S(3), -g.S(3)));
            }
        }
    }

    public static void Text(Graphics g, string text, Font font, Rectangle bounds, Color color, bool alignRight = false)
    {
        TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | (alignRight ? TextFormatFlags.Right : TextFormatFlags.Left);
        TextRenderer.DrawText(g, text, font, bounds, color, flags);
    }

    private static GraphicsPath RoundedRectangle(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
