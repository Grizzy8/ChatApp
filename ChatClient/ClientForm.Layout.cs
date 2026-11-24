using ChatShared;

namespace ChatClient
{
    // Denetimlerin tanımı ve yerleşimi. Davranış diğer ClientForm.*.cs dosyalarındadır.
    public partial class ClientForm
    {
        private const int SidebarWidth = 300;
        private const int ConversationRowHeight = 54;
        private const int ContactRowHeight = 38;
        private const int ContactsCardOpenHeight = 190;
        private const int ContactsCardClosedHeight = 36;
        private const int MembersPanelWidth = 210;

        // giriş ekranı
        private readonly Panel loginView = new() { Dock = DockStyle.Fill };
        private readonly TextBox hostTextBox = new() { Width = 200 };
        private readonly NumericUpDown portUpDown = new() { Width = 80 };
        private readonly TextBox usernameTextBox = new() { Width = 300 };
        private readonly TextBox passwordTextBox = new() { Width = 300, UseSystemPasswordChar = true };
        private readonly Button connectButton = new() { Text = "Giriş Yap", Tag = "accent", Height = 36 };
        private readonly Button registerButton = new() { Text = "Kayıt Ol", Height = 36 };
        private readonly Label loginErrorLabel = new() { AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Theme.ErrorText };

        // ana görünüm
        private readonly Panel mainView = new() { Dock = DockStyle.Fill, Visible = false };

        // sol panel
        private readonly TextBox searchBox = new() { Dock = DockStyle.Fill, PlaceholderText = "Kullanıcı adı veya ID ara", MaxLength = ProtocolConstants.MaxContactQueryLength };
        private readonly ThemedListBox conversationList = new() { Dock = DockStyle.Fill, RowHeight = ConversationRowHeight };
        private readonly ThemedListBox searchList = new() { Dock = DockStyle.Fill, RowHeight = ConversationRowHeight, Visible = false };
        private readonly Button newGroupButton = new() { Text = "+ Yeni grup", Dock = DockStyle.Fill, Height = 32 };
        private readonly Button invitesButton = new() { Text = "Davetler", Dock = DockStyle.Fill, Height = 32 };
        private readonly Panel contactsCard = new() { Dock = DockStyle.Bottom, Height = ContactsCardOpenHeight };
        private readonly Button contactsToggleButton = new() { Dock = DockStyle.Top, Height = ContactsCardClosedHeight, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ThemedListBox contactsList = new() { Dock = DockStyle.Fill, RowHeight = ContactRowHeight };
        private readonly Label contactsEmptyLabel = new() { Text = "Kişi yok. Yukarıdan kullanıcı adı ya da ID ile arayıp ekleyebilirsiniz.", Dock = DockStyle.Fill, ForeColor = Theme.Muted, Padding = new Padding(14, 8, 14, 0) };
        private readonly Panel profileStrip = new() { Dock = DockStyle.Bottom, Height = 60, BackColor = Theme.Darkest };
        // Sabit boyutlu ve AutoEllipsis'li: uzun ad ya da geniş harfli ID alt satıra taşıp kırpılmaz, "..." ile kısalır.
        private readonly Label profileNameLabel = new() { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Margin = Padding.Empty, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.White };
        private readonly Label profileIdLabel = new() { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, Margin = Padding.Empty, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) };
        private readonly Button copyIdButton = new() { Text = "ID kopyala", AutoSize = true, Height = 28 };
        private readonly Button disconnectButton = new() { Text = "Çıkış", AutoSize = true, Height = 28 };

        // sağ taraf
        private readonly Panel chatArea = new() { Dock = DockStyle.Fill, Visible = false };
        private readonly Label emptyLabel = new() { Dock = DockStyle.Fill, Text = "Bir sohbet seçin ya da yukarıdan birini arayın.", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 12f) };
        private readonly Panel headerPanel = new() { Dock = DockStyle.Top, Height = 58 };
        private readonly Label titleLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.White };
        private readonly Label subtitleLabel = new() { AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) };
        private readonly Button membersToggleButton = new() { Text = "Üyeler", AutoSize = true, Height = 30, Visible = false };
        private readonly Button settingsButton = new() { Text = "Ayarlar", AutoSize = true, Height = 30, Visible = false };
        private readonly RichTextBox messagesBox = new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = false, HideSelection = false, TabStop = false, Font = new Font("Segoe UI", 10.5f), ScrollBars = RichTextBoxScrollBars.Vertical };
        private readonly TextBox messageTextBox = new() { Dock = DockStyle.Fill, PlaceholderText = "Mesajınızı yazın", Font = new Font("Segoe UI", 11f) };
        private readonly Button sendButton = new() { Text = "Gönder", Tag = "accent", Width = 90, Dock = DockStyle.Fill };
        private readonly Panel membersPanel = new() { Dock = DockStyle.Right, Width = MembersPanelWidth, BackColor = Theme.Sidebar };
        private readonly Label onlineHeader = new() { Dock = DockStyle.Top, Height = 30, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = new Padding(14, 10, 0, 0) };
        private readonly Label offlineHeader = new() { Dock = DockStyle.Top, Height = 30, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = new Padding(14, 10, 0, 0) };
        private readonly ThemedListBox onlineMembersList = new() { Dock = DockStyle.Top, RowHeight = 38 };
        private readonly ThemedListBox offlineMembersList = new() { Dock = DockStyle.Fill, RowHeight = 38 };

        // durum çubuğu
        private readonly StatusStrip statusStrip = new() { SizingGrip = false };
        private readonly ToolStripStatusLabel statusLabel = new() { Text = "Bağlı değil" };
        private readonly ToolStripStatusLabel noticeLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };

        private void BuildLayout()
        {
            BuildLoginView();
            BuildMainView();

            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(noticeLabel);

            // Dock sırası: önce Fill, sonra kenarlar (Windows Forms ters sırayla yerleştirir).
            Controls.Add(loginView);
            Controls.Add(mainView);
            Controls.Add(statusStrip);

            Theme.Apply(this, Theme.Background);
            ApplyThemeOverrides();
        }

        private void BuildLoginView()
        {
            var card = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Anchor = AnchorStyles.None, Padding = new Padding(8) };
            var title = new Label { Text = "ChatApp", AutoSize = true, Font = new Font("Segoe UI", 24f, FontStyle.Bold), ForeColor = Color.White, Margin = new Padding(3, 3, 3, 2) };
            var subtitle = new Label { Text = "Giriş yapın ya da hesap oluşturun.", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(3, 0, 3, 18) };

            var serverRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            serverRow.Controls.Add(hostTextBox);
            serverRow.Controls.Add(portUpDown);
            hostTextBox.Margin = new Padding(3, 3, 12, 3);

            var buttons = new TableLayoutPanel { AutoSize = false, Size = new Size(300, 36), ColumnCount = 2, Margin = new Padding(3, 10, 3, 0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.Controls.Add(connectButton, 0, 0);
            buttons.Controls.Add(registerButton, 1, 0);
            connectButton.Dock = registerButton.Dock = DockStyle.Fill;

            AddField(card, null, title);
            AddField(card, null, subtitle);
            AddField(card, "SUNUCU VE PORT", serverRow);
            AddField(card, "KULLANICI ADI", usernameTextBox);
            AddField(card, "PAROLA", passwordTextBox);
            AddField(card, null, buttons);
            AddField(card, null, loginErrorLabel);

            connectButton.Click += ConnectButton_Click;
            registerButton.Click += RegisterButton_Click;
            passwordTextBox.KeyDown += (_, e) => SubmitOnEnter(e, () => ConnectButton_Click(this, EventArgs.Empty));
            usernameTextBox.KeyDown += (_, e) => SubmitOnEnter(e, () => passwordTextBox.Focus());

            loginView.Controls.Add(card);
            loginView.Resize += (_, _) => card.Location = new Point((loginView.Width - card.Width) / 2, Math.Max(10, (loginView.Height - card.Height) / 2));
            card.SizeChanged += (_, _) => card.Location = new Point((loginView.Width - card.Width) / 2, Math.Max(10, (loginView.Height - card.Height) / 2));
        }

        private static void AddField(TableLayoutPanel card, string? caption, Control control)
        {
            if (caption is not null)
            {
                card.Controls.Add(new Label { Text = caption, AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(3, 8, 3, 2) });
            }

            card.Controls.Add(control);
        }

        private void BuildMainView()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = SidebarWidth, BackColor = Theme.Sidebar };

            var searchPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(12, 12, 12, 8), ColumnCount = 1 };
            searchPanel.Controls.Add(searchBox);

            var actionRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(12, 8, 12, 8), ColumnCount = 2 };
            actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actionRow.Controls.Add(newGroupButton, 0, 0);
            actionRow.Controls.Add(invitesButton, 1, 0);
            newGroupButton.Margin = new Padding(0, 0, 4, 0);
            invitesButton.Margin = new Padding(4, 0, 0, 0);

            contactsCard.Controls.Add(contactsList);
            contactsCard.Controls.Add(contactsEmptyLabel);
            contactsCard.Controls.Add(contactsToggleButton);

            var profileLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent };
            profileLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            profileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
            profileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            profileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            profileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            var profileText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 8, 0, 4), BackColor = Color.Transparent };
            profileText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            profileText.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            profileText.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            profileText.Controls.Add(profileNameLabel, 0, 0);
            profileText.Controls.Add(profileIdLabel, 0, 1);
            profileLayout.Controls.Add(profileText, 1, 0);
            profileLayout.Controls.Add(copyIdButton, 2, 0);
            profileLayout.Controls.Add(disconnectButton, 3, 0);
            copyIdButton.Anchor = disconnectButton.Anchor = AnchorStyles.None;
            copyIdButton.AutoSize = disconnectButton.AutoSize = false;
            copyIdButton.Size = new Size(68, 30);
            disconnectButton.Size = new Size(54, 30);
            copyIdButton.Text = "Kopyala";
            profileStrip.Controls.Add(profileLayout);
            sidebar.Controls.Add(conversationList);
            sidebar.Controls.Add(searchList);
            sidebar.Controls.Add(searchPanel);
            sidebar.Controls.Add(actionRow);
            sidebar.Controls.Add(contactsCard);
            sidebar.Controls.Add(profileStrip);

            BuildChatArea();

            mainView.Controls.Add(emptyLabel);
            mainView.Controls.Add(chatArea);
            mainView.Controls.Add(sidebar);
        }

        private void BuildChatArea()
        {
            var headerButtons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 14, 14, 0) };
            headerButtons.Controls.Add(membersToggleButton);
            headerButtons.Controls.Add(settingsButton);

            titleLabel.Location = new Point(18, 10);
            subtitleLabel.Location = new Point(19, 34);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subtitleLabel);
            headerPanel.Controls.Add(headerButtons);

            var inputPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(16, 12, 16, 14), ColumnCount = 2 };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            inputPanel.Controls.Add(messageTextBox, 0, 0);
            inputPanel.Controls.Add(sendButton, 1, 0);
            sendButton.Margin = new Padding(8, 0, 0, 0);
            messageTextBox.MaxLength = ProtocolConstants.MaxMessageLength;

            membersPanel.Controls.Add(offlineMembersList);
            membersPanel.Controls.Add(offlineHeader);
            membersPanel.Controls.Add(onlineMembersList);
            membersPanel.Controls.Add(onlineHeader);

            chatArea.Controls.Add(messagesBox);
            chatArea.Controls.Add(membersPanel);
            chatArea.Controls.Add(headerPanel);
            chatArea.Controls.Add(inputPanel);
        }

        // Theme.Apply'ın genel kurallarının yetmediği yerler.
        private void ApplyThemeOverrides()
        {
            mainView.BackColor = Theme.Background;
            conversationList.BackColor = searchList.BackColor = contactsList.BackColor = Theme.Sidebar;
            onlineMembersList.BackColor = offlineMembersList.BackColor = Theme.Sidebar;
            contactsCard.BackColor = Theme.Sidebar;
            profileStrip.BackColor = Theme.Darkest;
            messagesBox.BackColor = Theme.Background;
            messagesBox.ForeColor = Theme.Text;
            headerPanel.BackColor = Theme.Background;
            profileNameLabel.ForeColor = Color.White;
            titleLabel.ForeColor = Color.White;

            contactsToggleButton.BackColor = Theme.Sidebar;
            contactsToggleButton.ForeColor = Theme.Muted;
            contactsToggleButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            contactsToggleButton.FlatAppearance.MouseOverBackColor = Theme.Hover;
            copyIdButton.Font = disconnectButton.Font = new Font("Segoe UI", 8.5f);
            loginView.BackColor = Theme.Background;
            statusStrip.BackColor = Theme.Darkest;
            statusLabel.ForeColor = noticeLabel.ForeColor = Theme.Muted;
            loginErrorLabel.ForeColor = Theme.ErrorText;
        }

        private static void SubmitOnEnter(KeyEventArgs e, Action submit)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Enter, bip sesi çıkarmasın
                submit();
            }
        }
    }
}
