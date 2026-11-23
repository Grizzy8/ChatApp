using System.Runtime.InteropServices;

namespace ChatClient;

/// <summary>Koyu (Discord benzeri) renk paleti ve standart denetimlere uygulanan tema.</summary>
public static class Theme
{
    public static readonly Color Darkest = Color.FromArgb(30, 31, 34);
    public static readonly Color Sidebar = Color.FromArgb(43, 45, 49);
    public static readonly Color Background = Color.FromArgb(49, 51, 56);
    public static readonly Color Input = Color.FromArgb(56, 58, 64);
    public static readonly Color Hover = Color.FromArgb(53, 55, 60);
    public static readonly Color Selected = Color.FromArgb(64, 66, 73);
    public static readonly Color Text = Color.FromArgb(219, 222, 225);
    public static readonly Color Muted = Color.FromArgb(148, 155, 164);
    public static readonly Color Accent = Color.FromArgb(88, 101, 242);
    public static readonly Color AccentHover = Color.FromArgb(71, 82, 196);
    public static readonly Color Online = Color.FromArgb(35, 165, 90);
    public static readonly Color Danger = Color.FromArgb(242, 63, 67);
    public static readonly Color OtherBubble = Color.FromArgb(64, 68, 75);

    public static readonly Color ErrorText = Color.FromArgb(250, 119, 124);

    /// <summary>Denetimi ve altındakileri temaya uydurur. Rolü belirtilmemiş kapsayıcılar <paramref name="background"/> rengini alır.</summary>
    public static void Apply(Control root, Color? background = null)
    {
        Color back = background ?? Background;
        switch (root)
        {
            case Form:
                root.BackColor = back;
                root.ForeColor = Text;
                break;
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.UseVisualStyleBackColor = false;
                button.BackColor = button.Tag as string == "accent" ? Accent : Input;
                button.FlatAppearance.MouseOverBackColor = button.Tag as string == "accent" ? AccentHover : Selected;
                button.ForeColor = Color.White;
                button.Cursor = Cursors.Hand;
                break;
            case TextBox or NumericUpDown:
                root.BackColor = Input;
                root.ForeColor = Text;
                if (root is TextBox box)
                {
                    box.BorderStyle = BorderStyle.FixedSingle;
                }
                break;
            case ListBox:
                root.BackColor = back;
                root.ForeColor = Text;
                ((ListBox)root).BorderStyle = BorderStyle.None;
                break;
            case RichTextBox:
                root.BackColor = back;
                root.ForeColor = Text;
                break;
            case Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = label.ForeColor == SystemColors.GrayText ? Muted : (label.ForeColor == SystemColors.ControlText ? Text : label.ForeColor);
                break;
            case StatusStrip strip:
                strip.BackColor = Darkest;
                strip.ForeColor = Muted;
                foreach (ToolStripItem item in strip.Items)
                {
                    item.ForeColor = item.ForeColor == SystemColors.ControlText ? Muted : item.ForeColor;
                }
                break;
            default:
                // Açıkça renk verilmiş kapsayıcılar (kenar çubuğu vb.) korunur; yalnızca rengi atanmamış olanlar boyanır.
                if (root.BackColor == SystemColors.Control)
                {
                    root.BackColor = back;
                }
                root.ForeColor = Text;
                break;
        }

        foreach (Control child in root.Controls)
        {
            Apply(child, root is Form ? back : root.BackColor);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DwmUseImmersiveDarkMode = 20;

    /// <summary>Pencerenin başlık çubuğunu koyu yapar (Windows 10 2004+; desteklenmiyorsa sessizce atlanır).</summary>
    public static void UseDarkTitleBar(IntPtr handle)
    {
        int enabled = 1;
        try
        {
            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Başlık çubuğu açık renkli kalır; işlevi etkilemez.
        }
    }

    /// <summary>İsimden tutarlı bir avatar/ad rengi üretir (aynı ad her zaman aynı rengi alır).</summary>
    public static Color ColorFor(string name)
    {
        Color[] palette =
        [
            Color.FromArgb(88, 101, 242), Color.FromArgb(35, 165, 90), Color.FromArgb(240, 177, 50),
            Color.FromArgb(235, 69, 158), Color.FromArgb(87, 190, 230), Color.FromArgb(237, 135, 73),
            Color.FromArgb(155, 132, 236), Color.FromArgb(70, 186, 160)
        ];

        int hash = 17;
        foreach (char c in name.ToUpperInvariant())
        {
            hash = unchecked(hash * 31 + c);
        }

        return palette[(hash & int.MaxValue) % palette.Length];
    }

    /// <summary>Koyu temalı bağlam menüsü.</summary>
    public static ContextMenuStrip NewMenu()
    {
        return new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()),
            BackColor = Darkest,
            ForeColor = Text,
            ShowImageMargin = false
        };
    }

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Darkest;
        public override Color MenuItemSelected => Selected;
        public override Color MenuItemBorder => Selected;
        public override Color MenuBorder => Selected;
        public override Color ImageMarginGradientBegin => Darkest;
        public override Color ImageMarginGradientMiddle => Darkest;
        public override Color ImageMarginGradientEnd => Darkest;
        public override Color MenuItemSelectedGradientBegin => Selected;
        public override Color MenuItemSelectedGradientEnd => Selected;
    }
}