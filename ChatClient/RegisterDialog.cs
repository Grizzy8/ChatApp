using ChatShared;

namespace ChatClient;

/// <summary>
/// Yeni hesap oluşturma penceresi: kullanıcı adı, parola ve parola tekrarı. Parola yanlış yazılıp hesabın
/// kilitli kalmaması için tekrar alanı zorunludur. Geçersiz girişte pencere kapanmaz, nedeni gösterilir.
/// </summary>
public sealed class RegisterDialog : ThemedForm
{
    private readonly TextBox _usernameBox = new() { Dock = DockStyle.Fill, MaxLength = ProtocolConstants.UsernameMaxLength };
    private readonly TextBox _passwordBox = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = ProtocolConstants.PasswordMaxLength };
    private readonly TextBox _confirmBox = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = ProtocolConstants.PasswordMaxLength };
    private readonly Label _errorLabel = new() { AutoSize = true, ForeColor = Theme.ErrorText, Margin = new Padding(3, 8, 3, 0) };

    public RegisterDialog(string suggestedUsername)
        : base("Yeni hesap oluştur")
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _usernameBox.Text = suggestedUsername;

        var okButton = new Button { Text = "Hesap oluştur", AutoSize = true, MinimumSize = new Size(110, 28) };
        var cancelButton = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 28) };
        okButton.Click += OkButton_Click;
        AcceptButton = okButton;
        CancelButton = cancelButton;

        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(Scaled(360), 0),
            ForeColor = Theme.Muted,
            Text = $"Kullanıcı adı {ProtocolConstants.UsernameMinLength}-{ProtocolConstants.UsernameMaxLength} karakter (harf, rakam, _). " +
                   $"Parola en az {ProtocolConstants.PasswordMinLength} karakter. Parolanızı unutursanız sıfırlanamaz."
        };

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(14),
            Dock = DockStyle.Fill
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Scaled(220)));

        AddRow(layout, "Kullanıcı adı:", _usernameBox);
        AddRow(layout, "Parola:", _passwordBox);
        AddRow(layout, "Parola (tekrar):", _confirmBox);
        layout.Controls.Add(hint, 0, layout.RowCount);
        layout.SetColumnSpan(hint, 2);
        layout.RowCount++;
        layout.Controls.Add(_errorLabel, 0, layout.RowCount);
        layout.SetColumnSpan(_errorLabel, 2);
        layout.RowCount++;
        layout.Controls.Add(buttons, 0, layout.RowCount);
        layout.SetColumnSpan(buttons, 2);
        layout.RowCount++;

        Controls.Add(layout);
        okButton.Tag = "accent";
        Theme.Apply(layout, Theme.Background);
    }

    public string Username => _usernameBox.Text.Trim();

    public string Password => _passwordBox.Text;

    private static void AddRow(TableLayoutPanel layout, string caption, TextBox box)
    {
        var label = new Label { Text = caption, AutoSize = true, Margin = new Padding(3, 7, 8, 0) };
        layout.Controls.Add(label, 0, layout.RowCount);
        layout.Controls.Add(box, 1, layout.RowCount);
        layout.RowCount++;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        string? error = FindInputError();
        if (error is not null)
        {
            _errorLabel.Text = error;
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private string? FindInputError()
    {
        if (!UsernameValidator.IsValid(Username))
        {
            return ErrorMessages.Describe(ErrorCode.UsernameInvalid);
        }

        if (!PasswordValidator.IsValid(Password))
        {
            return ErrorMessages.Describe(ErrorCode.PasswordInvalid);
        }

        return Password == _confirmBox.Text ? null : "Parolalar eşleşmiyor.";
    }
}
