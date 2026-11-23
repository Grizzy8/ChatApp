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

            // Windows Forms, form oluşturulurken UI thread'ine ait bir SynchronizationContext kurar;
            // servis event'lerini bu context ile UI thread'inde tetikler.
            _client = new ChatClientService(SynchronizationContext.Current);
            _chatView = new ChatView(chatBox);

            hostTextBox.Text = ProtocolConstants.DefaultHost;
            portUpDown.Minimum = ProtocolConstants.MinPort;
            portUpDown.Maximum = ProtocolConstants.MaxPort;
            portUpDown.Value = ProtocolConstants.DefaultPort;
            usernameTextBox.MaxLength = ProtocolConstants.UsernameMaxLength;
            messageTextBox.MaxLength = ProtocolConstants.MaxMessageLength;
            onlineListBox.Sorted = true;

            _client.StateChanged += OnStateChanged;
            _client.Disconnected += OnDisconnected;
            _client.MessageReceived += OnMessageReceived;

            UpdateUiState(ConnectionState.Disconnected);
        }

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            string host = hostTextBox.Text.Trim();
            string username = usernameTextBox.Text.Trim();
            int port = (int)portUpDown.Value;

            string? validationError = ValidateInput(host, username);
            if (validationError is not null)
            {
                MessageBox.Show(this, validationError, "Eksik veya hatalı bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _chatView.AppendSystem($"{host}:{port} adresine bağlanılıyor...");

            ConnectResult result;
            try
            {
                result = await _client.ConnectAsync(host, port, username, _lifetimeCts.Token);
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
                _chatView.AppendError($"Bağlantı kurulamadı: {result.Error}");
                MessageBox.Show(this, result.Error, "Bağlantı kurulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Başarılıysa "bağlandınız" satırı ve kullanıcı listesi, servisin ilettiği
            // JoinAccepted olayıyla (OnMessageReceived) sırayla gösterilir.
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

            bool sent = await _client.SendChatAsync(text);
            if (!sent && !IsDisposed && messageTextBox.TextLength == 0)
            {
                messageTextBox.Text = text;
            }
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
                case MessageType.Chat:
                    bool isOwn = UsernameValidator.Comparer.Equals(message.Sender, _client.Username);
                    _chatView.AppendChat(message.Timestamp ?? DateTime.Now, message.Sender ?? "?", message.Text ?? string.Empty, isOwn);
                    break;
                case MessageType.Error:
                    _chatView.AppendError(ErrorMessages.Describe(message.ErrorCode));
                    break;
            }
        }

        private void SetOnlineUsers(IEnumerable<string> users)
        {
            onlineListBox.BeginUpdate();
            onlineListBox.Items.Clear();
            foreach (string user in users)
            {
                onlineListBox.Items.Add(user);
            }
            onlineListBox.EndUpdate();
            UpdateOnlineCount();
        }

        private void AddOnlineUser(string username)
        {
            if (FindOnlineUserIndex(username) < 0)
            {
                onlineListBox.Items.Add(username);
                UpdateOnlineCount();
            }
        }

        private void RemoveOnlineUser(string username)
        {
            int index = FindOnlineUserIndex(username);
            if (index >= 0)
            {
                onlineListBox.Items.RemoveAt(index);
                UpdateOnlineCount();
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

        private void UpdateOnlineCount() => onlineGroup.Text = $"Çevrimiçi ({onlineListBox.Items.Count})";

        private void OnDisconnected(object? sender, DisconnectInfo info)
        {
            if (IsDisposed)
            {
                return;
            }

            SetOnlineUsers(Array.Empty<string>());

            if (info.Reason == DisconnectReason.UserRequested)
            {
                _chatView.AppendSystem(info.Message);
            }
            else
            {
                _chatView.AppendError(info.Message);
            }
        }

        private static string? ValidateInput(string host, string username)
        {
            if (host.Length == 0)
            {
                return "Lütfen sunucu adresini girin.";
            }

            return UsernameValidator.IsValid(username)
                ? null
                : ErrorMessages.Describe(ErrorCode.UsernameInvalid);
        }

        private void UpdateUiState(ConnectionState state)
        {
            bool isDisconnected = state == ConnectionState.Disconnected;

            hostTextBox.Enabled = isDisconnected;
            portUpDown.Enabled = isDisconnected;
            usernameTextBox.Enabled = isDisconnected;
            connectButton.Enabled = isDisconnected;
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
                ConnectionState.Connected => $"Bağlı — {_client.Username} @ {hostTextBox.Text.Trim()}:{(int)portUpDown.Value}",
                _ => "Bağlı değil"
            };
        }
    }
}
