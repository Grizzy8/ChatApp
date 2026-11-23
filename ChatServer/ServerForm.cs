using System.Net.Sockets;
using ChatShared;

namespace ChatServer
{
    /// <summary>Sunucu izleme arayüzü. Yalnızca başlatır/durdurur ve log gösterir.</summary>
    public partial class ServerForm : Form
    {
        private static readonly Color ErrorColor = Color.Firebrick;
        private static readonly Color InfoColor = Color.FromArgb(40, 40, 40);

        private readonly ChatServerHost _host = new();

        public ServerForm()
        {
            InitializeComponent();

            portUpDown.Minimum = ProtocolConstants.MinPort;
            portUpDown.Maximum = ProtocolConstants.MaxPort;
            portUpDown.Value = ProtocolConstants.DefaultPort;

            // ListView sütun genişlikleri ekran DPI'ına göre kendiliğinden ölçeklenmez.
            userColumn.Width = LogicalToDeviceUnits(120);
            addressColumn.Width = LogicalToDeviceUnits(130);
            connectedColumn.Width = LogicalToDeviceUnits(90);

            _host.LogWritten += OnLogWritten;
            _host.Stopped += OnHostStopped;
            _host.ClientsChanged += OnClientsChanged;
            UpdateUiState(isRunning: false);
        }

        private void StartButton_Click(object? sender, EventArgs e)
        {
            int port = (int)portUpDown.Value;
            try
            {
                _host.Start(port);
                UpdateUiState(isRunning: true);
            }
            catch (SocketException ex)
            {
                ReportStartFailure(DescribeSocketError(ex, port));
            }
            catch (InvalidOperationException ex)
            {
                ReportStartFailure(ex.Message);
            }
        }

        private void StopButton_Click(object? sender, EventArgs e)
        {
            _host.Stop();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Kapanırken arayüze event göndermeyi bırak, sonra dinleyiciyi kapat.
            _host.LogWritten -= OnLogWritten;
            _host.Stopped -= OnHostStopped;
            _host.ClientsChanged -= OnClientsChanged;
            _host.Dispose();
            base.OnFormClosing(e);
        }

        private void OnLogWritten(object? sender, ServerLogEntry entry)
        {
            RunOnUiThread(() => AppendLog(entry));
        }

        private void OnHostStopped(object? sender, EventArgs e)
        {
            RunOnUiThread(() => UpdateUiState(isRunning: false));
        }

        private void OnClientsChanged(object? sender, EventArgs e)
        {
            // Liste UI thread'inde okunur; böylece sıra karışsa bile her zaman güncel durum gösterilir.
            RunOnUiThread(() => RefreshClients(_host.GetClients()));
        }

        private void RefreshClients(IReadOnlyList<ClientInfo> clients)
        {
            clientsListView.BeginUpdate();
            clientsListView.Items.Clear();
            foreach (ClientInfo client in clients)
            {
                clientsListView.Items.Add(new ListViewItem(new[]
                {
                    client.Username,
                    client.Endpoint,
                    client.ConnectedAt.ToString("HH:mm:ss")
                }));
            }
            clientsListView.EndUpdate();
            clientsGroup.Text = $"Bağlı istemciler ({clients.Count})";
        }

        private void ReportStartFailure(string message)
        {
            AppendLog(new ServerLogEntry(DateTime.Now, $"Sunucu başlatılamadı: {message}", ServerLogLevel.Error));
            MessageBox.Show(this, message, "Sunucu başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateUiState(isRunning: false);
        }

        private static string DescribeSocketError(SocketException ex, int port) => ex.SocketErrorCode switch
        {
            SocketError.AddressAlreadyInUse => $"{port} numaralı port başka bir uygulama tarafından kullanılıyor. Lütfen farklı bir port deneyin.",
            SocketError.AccessDenied => $"{port} numaralı porta erişim izni yok. Lütfen farklı bir port deneyin.",
            _ => $"Ağ hatası: {ex.Message}"
        };

        private void UpdateUiState(bool isRunning)
        {
            startButton.Enabled = !isRunning;
            stopButton.Enabled = isRunning;
            portUpDown.Enabled = !isRunning;
            statusLabel.Text = isRunning
                ? $"Dinleniyor — port {_host.Port}"
                : "Durduruldu";
        }

        private void AppendLog(ServerLogEntry entry)
        {
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = entry.Level == ServerLogLevel.Error ? ErrorColor : InfoColor;
            logBox.AppendText($"[{entry.Time:HH:mm:ss}] {entry.Message}{Environment.NewLine}");
            logBox.SelectionColor = logBox.ForeColor;
            logBox.ScrollToCaret();
        }

        /// <summary>Ağ thread'inden gelen çağrıları UI thread'ine taşır.</summary>
        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (!InvokeRequired)
            {
                action();
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // Form tam bu sırada kapanıyor; güncellenecek arayüz kalmadı.
            }
        }
    }
}
