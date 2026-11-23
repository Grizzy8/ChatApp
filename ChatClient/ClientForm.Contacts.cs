using System.Runtime.InteropServices;
using ChatShared;

namespace ChatClient
{
    // "Kişilerim" sekmesi: kişi ekleme/silme, kendi ID'ni kopyalama ve kişilerin çevrimiçi durumu.
    public partial class ClientForm
    {
        private readonly ListBox contactsListBox = new()
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle
        };

        private readonly TextBox contactInputTextBox = new()
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "Kullanıcı adı veya ID",
            MaxLength = ProtocolConstants.MaxContactQueryLength
        };

        // AutoSize + Anchor: Dock.Fill, otomatik boyutlanan tablo satırında düğmeyi kırpıyordu.
        private readonly Button addContactButton = NewContactButton("Kişi ekle");
        private readonly Button removeContactButton = NewContactButton("Kişiyi sil");
        private readonly Button copyIdButton = NewContactButton("ID'mi kopyala");

        private static Button NewContactButton(string text) => new()
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            MinimumSize = new Size(0, 28),
            UseVisualStyleBackColor = true
        };

        private readonly Label contactIdLabel = new()
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 6, 3, 0),
            Text = "Bir kişi seçin."
        };

        private IReadOnlyList<ContactInfo> _contacts = Array.Empty<ContactInfo>();

        // İlk liste girişte gelir ve duyurulmaz; sonraki değişiklikler "eklendi/silindi" olarak gösterilir.
        private bool _hasReceivedContacts;

        /// <summary>Listede gösterilen kişi: kişi bilgisi ve o anki çevrimiçi durumu.</summary>
        private sealed record ContactListItem(ContactInfo Contact, bool IsOnline)
        {
            public override string ToString() => $"{(IsOnline ? "●" : "○")} {Contact.Username}";
        }

        private void InitializeContactsTab()
        {
            var buttons = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, AutoSize = true };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.Controls.Add(addContactButton, 0, 0);
            buttons.Controls.Add(removeContactButton, 1, 0);

            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ColumnCount = 1,
                Padding = new Padding(0, 6, 0, 0)
            };
            bottom.Controls.Add(contactInputTextBox, 0, 0);
            bottom.Controls.Add(buttons, 0, 1);
            bottom.Controls.Add(copyIdButton, 0, 2);
            bottom.Controls.Add(contactIdLabel, 0, 3);

            // Dock sırası: önce Fill, sonra Bottom eklenir (Windows Forms ters sırayla yerleştirir).
            contactsTab.Controls.Add(contactsListBox);
            contactsTab.Controls.Add(bottom);

            addContactButton.Click += AddContactButton_Click;
            removeContactButton.Click += RemoveContactButton_Click;
            copyIdButton.Click += CopyIdButton_Click;
            contactsListBox.SelectedIndexChanged += ContactsListBox_SelectedIndexChanged;
            contactsListBox.DoubleClick += ContactsListBox_DoubleClick;
            contactInputTextBox.KeyDown += ContactInputTextBox_KeyDown;
        }

        private async void AddContactButton_Click(object? sender, EventArgs e)
        {
            string query = contactInputTextBox.Text.Trim();
            if (query.Length == 0)
            {
                contactInputTextBox.Focus();
                return;
            }

            contactInputTextBox.Clear();
            if (!await _client.AddContactAsync(query) && !IsDisposed)
            {
                contactInputTextBox.Text = query; // gönderilemedi: metin kaybolmasın
            }
        }

        private async void RemoveContactButton_Click(object? sender, EventArgs e)
        {
            if (contactsListBox.SelectedItem is ContactListItem item)
            {
                await _client.RemoveContactAsync(item.Contact.UserId);
            }
        }

        private void CopyIdButton_Click(object? sender, EventArgs e)
        {
            string? id = _client.UserId;
            if (id is null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(id);
                _chatView.AppendSystem($"ID'niz panoya kopyalandı: {id}");
            }
            catch (ExternalException)
            {
                // Pano başka bir uygulama tarafından kullanılıyor olabilir; ID yine de ekranda görünür.
                _chatView.AppendError($"Pano kullanılamadı. ID'niz: {id}");
            }
        }

        private void ContactInputTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Enter, varsayılan düğmeyi (Gönder) tetiklemesin
                AddContactButton_Click(sender, EventArgs.Empty);
            }
        }

        private void ContactsListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            contactIdLabel.Text = contactsListBox.SelectedItem is ContactListItem item
                ? $"ID: {item.Contact.UserId}"
                : "Bir kişi seçin.";
            removeContactButton.Enabled = _client.State == ConnectionState.Connected && contactsListBox.SelectedItem is not null;
        }

        // Çift tıklama: kişi alıcı olarak seçilir. Çevrimdışıysa da seçilir; mesaj sunucuda bekletilip kişi girince iletilir.
        private void ContactsListBox_DoubleClick(object? sender, EventArgs e)
        {
            if (contactsListBox.SelectedItem is not ContactListItem item)
            {
                return;
            }

            int index = FindOnlineUserIndex(item.Contact.Username);
            _isSyncingRecipient = true;
            try
            {
                if (index >= 0)
                {
                    onlineListBox.SelectedIndex = index;
                }
                else
                {
                    onlineListBox.ClearSelected();
                }
            }
            finally
            {
                _isSyncingRecipient = false;
            }

            SetRecipient(item.Contact.Username);
            messageTextBox.Focus();
        }

        /// <summary>Sunucudan gelen kişi listesini uygular; ilk listeden sonraki farkları sohbet ekranında duyurur.</summary>
        private void SetContacts(IReadOnlyList<ContactInfo>? contacts)
        {
            IReadOnlyList<ContactInfo> updated = contacts ?? Array.Empty<ContactInfo>();

            if (_hasReceivedContacts)
            {
                var oldIds = _contacts.Select(c => c.UserId).ToHashSet();
                var newIds = updated.Select(c => c.UserId).ToHashSet();

                foreach (ContactInfo added in updated.Where(c => !oldIds.Contains(c.UserId)))
                {
                    _chatView.AppendSystem($"{added.Username} kişilerinize eklendi.");
                }

                foreach (ContactInfo removed in _contacts.Where(c => !newIds.Contains(c.UserId)))
                {
                    _chatView.AppendSystem($"{removed.Username} kişilerinizden silindi.");
                }
            }

            _contacts = updated;
            _hasReceivedContacts = true;
            RefreshContactsList();
        }

        private void ClearContacts()
        {
            _contacts = Array.Empty<ContactInfo>();
            _hasReceivedContacts = false;
            RefreshContactsList();
        }

        /// <summary>Kişi listesini yeniden çizer: çevrimiçi olanlar işaretlenir, seçim korunur.</summary>
        private void RefreshContactsList()
        {
            string? selectedId = (contactsListBox.SelectedItem as ContactListItem)?.Contact.UserId;

            contactsListBox.BeginUpdate();
            contactsListBox.Items.Clear();
            foreach (ContactInfo contact in _contacts)
            {
                bool isOnline = FindOnlineUserIndex(contact.Username) >= 0;
                int index = contactsListBox.Items.Add(new ContactListItem(contact, isOnline));
                if (contact.UserId == selectedId)
                {
                    contactsListBox.SelectedIndex = index;
                }
            }
            contactsListBox.EndUpdate();

            contactsTab.Text = $"Kişilerim ({_contacts.Count})";
            ContactsListBox_SelectedIndexChanged(this, EventArgs.Empty);
        }

        private void UpdateContactsUi(bool isConnected)
        {
            contactInputTextBox.Enabled = isConnected;
            addContactButton.Enabled = isConnected;
            copyIdButton.Enabled = isConnected;
            removeContactButton.Enabled = isConnected && contactsListBox.SelectedItem is not null;
        }
    }
}
