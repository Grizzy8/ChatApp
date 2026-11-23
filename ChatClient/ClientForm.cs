using ChatShared;

namespace ChatClient
{
    /// <summary>İstemci ana penceresi: giriş, bağlantı durumu ve sunucudan gelen iletilerin dağıtımı.</summary>
    public partial class ClientForm : Form
    {
        private readonly ChatClientService _client;
        private readonly MessageView _view;
        private readonly CancellationTokenSource _lifetimeCts = new();
        private readonly System.Windows.Forms.Timer _noticeTimer = new() { Interval = ClientConstants.NoticeDurationMs };

        public ClientForm()
        {
            InitializeComponent();

            // Windows Forms, form oluşturulurken UI thread'ine ait bir SynchronizationContext kurar;
            // servis event'lerini bu context ile UI thread'inde tetikler.
            _client = new ChatClientService(SynchronizationContext.Current);
            _view = new MessageView(messagesBox);

            hostTextBox.Text = ProtocolConstants.DefaultHost;
            portUpDown.Minimum = ProtocolConstants.MinPort;
            portUpDown.Maximum = ProtocolConstants.MaxPort;
            portUpDown.Value = ProtocolConstants.DefaultPort;
            usernameTextBox.MaxLength = ProtocolConstants.UsernameMaxLength;
            passwordTextBox.MaxLength = ProtocolConstants.PasswordMaxLength;

            _noticeTimer.Tick += (_, _) =>
            {
                _noticeTimer.Stop();
                noticeLabel.Text = string.Empty;
            };

            InitializeConversations();
            InitializeSearch();
            InitializeContacts();
            InitializeGroups();

            _client.StateChanged += OnStateChanged;
            _client.Disconnected += OnDisconnected;
            _client.MessageReceived += OnMessageReceived;

            UpdateUiState(ConnectionState.Disconnected);
        }

        // Pencere ekrandan büyükse (küçük çözünürlük ya da yüksek ölçek) çalışma alanına sığdırılır.
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FitToWorkingArea();
            Resize += (_, _) => UpdateHeader();
        }

        private void FitToWorkingArea()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int width = Math.Min(Width, (int)(area.Width * 0.96));
            int height = Math.Min(Height, (int)(area.Height * 0.96));

            MinimumSize = new Size(Math.Min(MinimumSize.Width, width), Math.Min(MinimumSize.Height, height));
            Size = new Size(width, height);
            Location = new Point(
                Math.Clamp(Location.X, area.Left, Math.Max(area.Left, area.Right - width)),
                Math.Clamp(Location.Y, area.Top, Math.Max(area.Top, area.Bottom - height)));
        }

        /// <summary>Mantıksal (96 DPI) pikseli bu pencerenin DPI'ına göre ölçekler.</summary>
        private int Scale(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96.0);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.UseDarkTitleBar(Handle);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Kapanırken arayüze event göndermeyi bırak, devam eden bağlantı denemesini iptal et.
            _client.StateChanged -= OnStateChanged;
            _client.Disconnected -= OnDisconnected;
            _client.MessageReceived -= OnMessageReceived;
            _lifetimeCts.Cancel();
            _client.Dispose();
            base.OnFormClosing(e);
        }

        // ---- giriş ----

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            string username = usernameTextBox.Text.Trim();
            string password = passwordTextBox.Text;

            string? validationError = ValidateLogin(hostTextBox.Text.Trim(), username, password);
            if (validationError is not null)
            {
                loginErrorLabel.Text = validationError;
                return;
            }

            await ConnectAsync(username, password, AuthMode.Login);
        }

        private async void RegisterButton_Click(object? sender, EventArgs e)
        {
            if (hostTextBox.Text.Trim().Length == 0)
            {
                loginErrorLabel.Text = "Lütfen sunucu adresini girin.";
                return;
            }

            using var dialog = new RegisterDialog(usernameTextBox.Text.Trim());
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            usernameTextBox.Text = dialog.Username;
            passwordTextBox.Text = dialog.Password;
            await ConnectAsync(dialog.Username, dialog.Password, AuthMode.Register);
        }

        private async Task ConnectAsync(string username, string password, AuthMode mode)
        {
            loginErrorLabel.Text = string.Empty;

            ConnectResult result;
            try
            {
                result = await _client.ConnectAsync(
                    hostTextBox.Text.Trim(), (int)portUpDown.Value, username, password, mode, _lifetimeCts.Token);
            }
            catch (InvalidOperationException ex)
            {
                result = ConnectResult.Failed(ex.Message);
            }

            if (IsDisposed)
            {
                return;
            }

            if (!result.Success)
            {
                string title = mode == AuthMode.Register ? "Hesap oluşturulamadı" : "Giriş yapılamadı";
                loginErrorLabel.Text = $"{title}: {result.Error}";
                return;
            }

            // Parola işi bitince bellekte ve kutuda bırakılmaz. Sohbet ekranı JoinAccepted olayıyla kurulur.
            passwordTextBox.Clear();
            usernameTextBox.Text = result.Username;
        }

        private async void DisconnectButton_Click(object? sender, EventArgs e)
        {
            await _client.DisconnectAsync();
        }

        private static string? ValidateLogin(string host, string username, string password)
        {
            if (host.Length == 0)
            {
                return "Lütfen sunucu adresini girin.";
            }

            if (!UsernameValidator.IsValid(username))
            {
                return ErrorMessages.Describe(ErrorCode.UsernameInvalid);
            }

            return password.Length == 0 ? "Lütfen parolanızı girin." : null;
        }

        // ---- bağlantı olayları ----

        private void OnStateChanged(object? sender, ConnectionState state)
        {
            if (!IsDisposed)
            {
                UpdateUiState(state);
            }
        }

        private void OnDisconnected(object? sender, DisconnectInfo info)
        {
            if (IsDisposed)
            {
                return;
            }

            EndSession();
            loginErrorLabel.Text = info.Reason == DisconnectReason.UserRequested ? string.Empty : info.Message;
            ClearNotice();
        }

        private void UpdateUiState(ConnectionState state)
        {
            bool isDisconnected = state == ConnectionState.Disconnected;
            bool isConnected = state == ConnectionState.Connected;

            hostTextBox.Enabled = portUpDown.Enabled = usernameTextBox.Enabled = passwordTextBox.Enabled = isDisconnected;
            connectButton.Enabled = registerButton.Enabled = isDisconnected;

            if (!isConnected)
            {
                mainView.Visible = false;
                loginView.Visible = true;
            }

            messageTextBox.Enabled = sendButton.Enabled = isConnected;
            statusLabel.Text = state switch
            {
                ConnectionState.Connecting => "Bağlanıyor...",
                ConnectionState.Connected => $"Bağlı — {hostTextBox.Text.Trim()}:{(int)portUpDown.Value}",
                _ => "Bağlı değil"
            };
        }

        // ---- sunucudan gelen iletiler ----

        private void OnMessageReceived(object? sender, ChatMessage message)
        {
            if (IsDisposed)
            {
                return;
            }

            switch (message.Type)
            {
                case MessageType.JoinAccepted:
                    BeginSession(message);
                    break;
                case MessageType.UserJoined when message.Sender is not null:
                    SetPresence(message.Sender, isOnline: true);
                    break;
                case MessageType.UserLeft when message.Sender is not null:
                    SetPresence(message.Sender, isOnline: false);
                    break;
                case MessageType.PresenceList:
                    ReplacePresence(message.Users ?? Array.Empty<string>());
                    break;
                case MessageType.Private or MessageType.PrivateQueued:
                    HandlePrivateMessage(message);
                    break;
                case MessageType.GroupMessage:
                    HandleGroupMessage(message);
                    break;
                case MessageType.ContactList:
                    SetContacts(message.Contacts);
                    break;
                case MessageType.GroupList:
                    SetGroups(message.Groups);
                    break;
                case MessageType.InviteList:
                    SetInvites(message.Invites);
                    break;
                case MessageType.GroupMembers:
                    SetGroupMembers(message.Recipient, message.Members);
                    break;
                case MessageType.UserFound:
                    OnUserFound(message);
                    break;
                case MessageType.InviteSent:
                    ShowStatus($"{message.Text} gruba davet edildi.", isError: false);
                    break;
                case MessageType.Error:
                    _pendingOpenAfterAdd = null;
                    ShowStatus(ErrorMessages.Describe(message.ErrorCode), isError: true);
                    break;
            }
        }

        // ---- bildirimler ----

        /// <summary>
        /// Kısa bildirim: açık bir iletişim penceresi varsa orada, yoksa durum çubuğunda gösterilir
        /// (modal pencere açıkken ana penceredeki bildirim görünmez kalırdı).
        /// </summary>
        private void ShowStatus(string text, bool isError)
        {
            if (_settingsDialog is not null)
            {
                _settingsDialog.ShowStatus(text, isError);
            }
            else if (_invitesDialog is not null)
            {
                _invitesDialog.ShowStatus(text, isError);
            }
            else
            {
                ShowNotice(text, isError);
            }
        }

        private void ShowNotice(string text, bool isError = false)
        {
            noticeLabel.Text = text;
            noticeLabel.ForeColor = isError ? Theme.ErrorText : Theme.Muted;
            _noticeTimer.Stop();
            _noticeTimer.Start();
        }

        private void ClearNotice()
        {
            _noticeTimer.Stop();
            noticeLabel.Text = string.Empty;
        }
    }
}
