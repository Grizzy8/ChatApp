using ChatShared;

namespace ChatClient
{
    /// <summary>İstemci arayüzü: bağlantı bilgileri, Bağlan/Bağlantıyı Kes ve durum gösterimi.</summary>
    public partial class ClientForm : Form
    {
        private readonly ChatClientService _client;
        private readonly ChatView _chatView;
        private readonly CancellationTokenSource _lifetimeCts = new();

        public ClientForm()
        {
            InitializeComponent();
            InitializeContactsTab();

            // Windows Forms, form oluşturulurken UI thread'ine ait bir SynchronizationContext kurar;
            // servis event'lerini bu context ile UI thread'inde tetikler.
            _client = new ChatClientService(SynchronizationContext.Current);
            _chatView = new ChatView(chatBox);

            hostTextBox.Text = ProtocolConstants.DefaultHost;
            portUpDown.Minimum = ProtocolConstants.MinPort;
            portUpDown.Maximum = ProtocolConstants.MaxPort;
            portUpDown.Value = ProtocolConstants.DefaultPort;
            usernameTextBox.MaxLength = ProtocolConstants.UsernameMaxLength;
            passwordTextBox.MaxLength = ProtocolConstants.PasswordMaxLength;
            messageTextBox.MaxLength = ProtocolConstants.MaxMessageLength;
            onlineListBox.Sorted = true;

            _client.StateChanged += OnStateChanged;
            _client.Disconnected += OnDisconnected;
            _client.MessageReceived += OnMessageReceived;

            UpdateUiState(ConnectionState.Disconnected);
        }

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            string username = usernameTextBox.Text.Trim();
            string password = passwordTextBox.Text;

            string? validationError = ValidateLogin(hostTextBox.Text.Trim(), username, password);
            if (validationError is not null)
            {
                MessageBox.Show(this, validationError, "Eksik veya hatalı bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await ConnectAsync(username, password, AuthMode.Login);
        }

        private async void RegisterButton_Click(object? sender, EventArgs e)
        {
            if (hostTextBox.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "Lütfen sunucu adresini girin.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            string host = hostTextBox.Text.Trim();
            int port = (int)portUpDown.Value;

            _chatView.AppendSystem($"{host}:{port} adresine bağlanılıyor...");

            ConnectResult result;
            try
            {
                result = await _client.ConnectAsync(host, port, username, password, mode, _lifetimeCts.Token);
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
                _chatView.AppendError($"{title}: {result.Error}");
                MessageBox.Show(this, result.Error, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Parola işi bitince bellekte ve kutuda bırakılmaz. Başarılıysa "bağlandınız" satırı ve kullanıcı
            // listesi, servisin ilettiği JoinAccepted olayıyla (OnMessageReceived) sırayla gösterilir.
            passwordTextBox.Clear();
            usernameTextBox.Text = result.Username;
        }

        private async void DisconnectButton_Click(object? sender, EventArgs e)
        {
            await _client.DisconnectAsync();
        }

        private async void SendButton_Click(object? sender, EventArgs e)
        {
            string text = messageTextBox.Text.Trim();
            if (text.Length == 0)
            {
                return;
            }

            // Kutu hemen boşaltılır ki kullanıcı beklemeden yazmaya devam edebilsin;
            // gönderme başarısız olursa metin geri yüklenir.
            messageTextBox.Clear();
            messageTextBox.Focus();

            // Alıcı gönderme anında okunur; beklerken seçim değişse bile mesaj niyet edilen kişiye gider.
            string? recipient = SelectedRecipient;
            bool sent = recipient is null
                ? await _client.SendChatAsync(text)
                : await _client.SendPrivateAsync(recipient, text);

            if (!sent && !IsDisposed && messageTextBox.TextLength == 0)
            {
                messageTextBox.Text = text;
            }
        }

        // Seçilen alıcı: çevrimiçi listesinden ya da kişilerden (çift tık) gelir; çevrimdışı olsa da geçerlidir,
        // çünkü mesaj sunucuda bekletilip alıcı girince teslim edilir. "Herkese dön" ile temizlenir.
        private string? _chosenRecipient;
        private bool _isSyncingRecipient; // kodla yapılan liste değişiklikleri alıcıyı silmesin

        private string? SelectedRecipient => _chosenRecipient;

        private void SetRecipient(string? username)
        {
            _chosenRecipient = username;
            UpdateRecipientUi();
        }

        private void ClearRecipientButton_Click(object? sender, EventArgs e)
        {
            _isSyncingRecipient = true;
            try { onlineListBox.ClearSelected(); } finally { _isSyncingRecipient = false; }
            SetRecipient(null);
            messageTextBox.Focus();
        }

        private void OnlineListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isSyncingRecipient)
            {
                return;
            }

            string? selected = onlineListBox.SelectedItem as string;

            // Kendine özel mesaj anlamsızdır: kendi adını seçmek seçimi kaldırır.
            if (UsernameValidator.Comparer.Equals(selected, _client.Username))
            {
                onlineListBox.ClearSelected();
                return;
            }

            SetRecipient(selected);
        }

        private void UpdateRecipientUi()
        {
            string? recipient = SelectedRecipient;
            bool isPrivate = recipient is not null;

            bool isOnline = isPrivate && FindOnlineUserIndex(recipient!) >= 0;
            recipientLabel.Text = isPrivate ? (isOnline ? $"→ {recipient} (özel mesaj)" : $"→ {recipient} (çevrimdışı — mesaj bekletilir)") : "Herkese";
            recipientLabel.ForeColor = isPrivate ? ClientConstants.PrivateMessageColor : SystemColors.GrayText;
            clearRecipientButton.Visible = isPrivate;
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

        private void OnStateChanged(object? sender, ConnectionState state)
        {
            if (!IsDisposed)
            {
                UpdateUiState(state);
            }
        }

        private void OnMessageReceived(object? sender, ChatMessage message)
        {
            if (IsDisposed)
            {
                return;
            }

            switch (message.Type)
            {
                case MessageType.JoinAccepted:
                    IReadOnlyList<string> users = message.Users ?? Array.Empty<string>();
                    SetOnlineUsers(users);
                    BeginHistorySession(_client.Username);
                    _chatView.AppendSystem($"Sunucuya bağlandınız. Çevrimiçi: {string.Join(", ", users)}");
                    break;
                case MessageType.UserJoined when message.Sender is not null:
                    AddOnlineUser(message.Sender);
                    _chatView.AppendLine(message.Timestamp ?? DateTime.Now, $"{message.Sender} sohbete katıldı.", ClientConstants.SystemMessageColor);
                    break;
                case MessageType.UserLeft when message.Sender is not null:
                    RemoveOnlineUser(message.Sender);
                    _chatView.AppendLine(message.Timestamp ?? DateTime.Now, $"{message.Sender} sohbetten ayrıldı.", ClientConstants.SystemMessageColor);
                    break;
                case MessageType.PrivateQueued:
                    ShowPrivate(message);
                    _chatView.AppendSystem($"{message.Recipient} çevrimdışı; mesajınız, giriş yaptığında ona iletilecek.");
                    break;
                case MessageType.Private:
                    ShowPrivate(message);
                    break;
                case MessageType.Chat:
                    DateTime chatTime = message.Timestamp ?? DateTime.Now;
                    string chatSender = message.Sender ?? "?";
                    string chatText = message.Text ?? string.Empty;
                    bool isOwn = UsernameValidator.Comparer.Equals(message.Sender, _client.Username);
                    _chatView.AppendChat(chatTime, chatSender, chatText, isOwn);
                    _ = SaveToHistoryAsync(chatTime, ChatHistoryStore.FormatChat(chatTime, chatSender, chatText));
                    break;
                case MessageType.ContactList:
                    SetContacts(message.Contacts);
                    break;
                case MessageType.Error:
                    _chatView.AppendError(ErrorMessages.Describe(message.ErrorCode));
                    break;
            }
        }

        private void ShowPrivate(ChatMessage message)
        {
            DateTime time = message.Timestamp ?? DateTime.Now;
            string sender = message.Sender ?? "?";
            string recipient = message.Recipient ?? "?";
            string text = message.Text ?? string.Empty;
            bool isSentByMe = UsernameValidator.Comparer.Equals(message.Sender, _client.Username);
            _chatView.AppendPrivate(time, sender, recipient, text, isSentByMe);
            _ = SaveToHistoryAsync(time, ChatHistoryStore.FormatPrivate(time, sender, recipient, text, isSentByMe));
        }

        private void SetOnlineUsers(IEnumerable<string> users)
        {
            _isSyncingRecipient = true;
            onlineListBox.BeginUpdate();
            onlineListBox.Items.Clear();
            foreach (string user in users)
            {
                onlineListBox.Items.Add(user);
            }
            onlineListBox.EndUpdate();
            _isSyncingRecipient = false;
            UpdateOnlineCount();
            UpdateRecipientUi();
        }

        private void AddOnlineUser(string username)
        {
            if (FindOnlineUserIndex(username) < 0)
            {
                onlineListBox.Items.Add(username);
                UpdateOnlineCount();
                UpdateRecipientUi(); // seçili alıcı çevrimiçi olduysa etiket güncellensin
            }
        }

        private void RemoveOnlineUser(string username)
        {
            int index = FindOnlineUserIndex(username);
            if (index >= 0)
            {
                _isSyncingRecipient = true; // alıcı çevrimdışı olsa da seçili kalır; mesaj bekletilir
                try { onlineListBox.Items.RemoveAt(index); } finally { _isSyncingRecipient = false; }
                UpdateOnlineCount();
                UpdateRecipientUi();
            }
        }

        private int FindOnlineUserIndex(string username)
        {
            for (int i = 0; i < onlineListBox.Items.Count; i++)
            {
                if (UsernameValidator.Comparer.Equals(onlineListBox.Items[i] as string, username))
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateOnlineCount()
        {
            onlineTab.Text = $"Çevrimiçi ({onlineListBox.Items.Count})";
            RefreshContactsList(); // kişilerin çevrimiçi durumu bu listeye bağlıdır
        }

        private void OnDisconnected(object? sender, DisconnectInfo info)
        {
            if (IsDisposed)
            {
                return;
            }

            _chosenRecipient = null;
            SetOnlineUsers(Array.Empty<string>());
            ClearContacts();
            _history = null;

            if (info.Reason == DisconnectReason.UserRequested)
            {
                _chatView.AppendSystem(info.Message);
            }
            else
            {
                _chatView.AppendError(info.Message);
            }
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

        private void UpdateUiState(ConnectionState state)
        {
            bool isDisconnected = state == ConnectionState.Disconnected;

            hostTextBox.Enabled = isDisconnected;
            portUpDown.Enabled = isDisconnected;
            usernameTextBox.Enabled = isDisconnected;
            passwordTextBox.Enabled = isDisconnected;
            connectButton.Enabled = isDisconnected;
            registerButton.Enabled = isDisconnected;
            UpdateContactsUi(state == ConnectionState.Connected);
            bool isConnected = state == ConnectionState.Connected;
            disconnectButton.Enabled = isConnected;
            messageTextBox.Enabled = isConnected;
            sendButton.Enabled = isConnected;

            // Enter tuşu bağlı değilken Bağlan'a, bağlıyken Gönder'e basar.
            AcceptButton = isConnected ? sendButton : connectButton;
            if (isConnected)
            {
                messageTextBox.Focus();
            }

            statusLabel.Text = state switch
            {
                ConnectionState.Connecting => "Bağlanıyor...",
                ConnectionState.Connected =>
                    $"Bağlı — {_client.Username} (ID: {_client.UserId}) @ {hostTextBox.Text.Trim()}:{(int)portUpDown.Value}",
                _ => "Bağlı değil"
            };
        }
    }
}
