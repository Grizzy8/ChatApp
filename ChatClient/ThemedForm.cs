namespace ChatClient;

/// <summary>Koyu temalı, sabit boyutlu iletişim penceresi tabanı.</summary>
public class ThemedForm : Form
{
    public ThemedForm(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
    }

    /// <summary>Mantıksal (96 DPI) pikseli ekranın DPI'ına göre ölçekler; kodla verilen boyutlar için.</summary>
    protected int Scaled(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96.0);

    protected void SetClientSizeLogical(int width, int height) => ClientSize = new Size(Scaled(width), Scaled(height));

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    protected static Button NewButton(string text, bool accent = false, bool danger = false, int minWidth = 90)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(minWidth, 30),
            Tag = accent ? "accent" : null,
            Margin = new Padding(4, 4, 0, 0)
        };
        Theme.Apply(button);
        if (danger)
        {
            button.BackColor = Theme.Danger;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 45, 50);
        }

        return button;
    }
}
