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

            _client.StateChanged += OnStateChanged;
            _client.Disconnected += OnDisconnected;

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
                return;
            }

            _chatView.AppendSystem($"Sunucuya bağlandınız. Çevrimiçi: {string.Join(", ", result.Users ?? Array.Empty<string>())}");
        }

        private async void DisconnectButton_Click(object? sender, EventArgs e)
        {
            await _client.DisconnectAsync();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Kapanırken arayüze event göndermeyi bırak, devam eden bağlantı denemesini iptal et.
            _client.StateChanged -= OnStateChanged;
            _client.Disconnected -= OnDisconnected;
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

        private void OnDisconnected(object? sender, DisconnectInfo info)
        {
            if (IsDisposed)
            {
                return;
            }

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
            disconnectButton.Enabled = state == ConnectionState.Connected;

            statusLabel.Text = state switch
            {
                ConnectionState.Connecting => "Bağlanıyor...",
                ConnectionState.Connected => $"Bağlı — {_client.Username} @ {hostTextBox.Text.Trim()}:{(int)portUpDown.Value}",
                _ => "Bağlı değil"
            };
        }
    }
}
