namespace ChatClient;

/// <summary>Koyu temalı onay ve tek satırlık metin giriş pencereleri.</summary>
public static class Dialogs
{
    /// <summary>Evet/Hayır sorusu. <paramref name="danger"/> true ise onay düğmesi kırmızıdır.</summary>
    public static bool Confirm(IWin32Window owner, string title, string message, string okText, bool danger = false)
    {
        using var dialog = new SimpleDialog(title, message, okText, danger, inputLabel: null, initial: null, maxLength: 0);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    /// <summary>Tek metin sorar; iptalde ya da boş girişte null döner.</summary>
    public static string? Prompt(IWin32Window owner, string title, string label, string initial, int maxLength, string okText)
    {
        using var dialog = new SimpleDialog(title, message: null, okText, danger: false, label, initial, maxLength);
        return dialog.ShowDialog(owner) == DialogResult.OK && dialog.Value.Length > 0 ? dialog.Value : null;
    }

    private sealed class SimpleDialog : ThemedForm
    {
        private readonly TextBox? _input;

        public SimpleDialog(string title, string? message, string okText, bool danger, string? inputLabel, string? initial, int maxLength)
            : base(title)
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(18), Dock = DockStyle.Fill };

            if (message is not null)
            {
                layout.Controls.Add(new Label { Text = message, AutoSize = true, MaximumSize = new Size(Scaled(380), 0), Margin = new Padding(0, 0, 0, 14) });
            }

            if (inputLabel is not null)
            {
                layout.Controls.Add(new Label { Text = inputLabel, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 0, 4) });
                _input = new TextBox { Width = Scaled(320), MaxLength = maxLength, Text = initial ?? string.Empty, Margin = new Padding(0, 0, 0, 14) };
                layout.Controls.Add(_input);
            }

            Button ok = NewButton(okText, accent: !danger, danger: danger, minWidth: 100);
            Button cancel = NewButton("Vazgeç", minWidth: 80);
            ok.DialogResult = DialogResult.OK;
            cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok;
            CancelButton = cancel;

            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0) };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons);

            Controls.Add(layout);
            Theme.Apply(layout, Theme.Background);
            Shown += (_, _) =>
            {
                _input?.Focus();
                _input?.SelectAll();
            };
        }

        public string Value => _input?.Text.Trim() ?? string.Empty;
    }
}
