using ChatShared;

namespace ChatClient
{
    // Sol alttaki "Kişiler" kartı: kişi listesi, sohbet açma ve kişiyi silme.
    public partial class ClientForm
    {
        private readonly ContextMenuStrip _contactMenu = Theme.NewMenu();

        private IReadOnlyList<ContactInfo> _contacts = Array.Empty<ContactInfo>();

        // İlk liste girişte gelir ve duyurulmaz; sonraki değişiklikler bildirim olarak gösterilir.
        private bool _hasReceivedContacts;
        private bool _isContactsOpen = true;

        private void InitializeContacts()
        {
            contactsList.Painter = PaintContact;
            contactsList.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && contactsList.ItemAt(e.Location) is ContactInfo contact)
                {
                    OpenPrivate(contact.Username);
                }
            };
            contactsList.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Right && contactsList.ItemAt(e.Location) is ContactInfo)
                {
                    _contactMenu.Show(contactsList, e.Location);
                }
            };

            _contactMenu.Items.Add("Sohbeti aç").Click += (_, _) =>
            {
                if (contactsList.SelectedItem is ContactInfo contact)
                {
                    OpenPrivate(contact.Username);
                }
            };
            _contactMenu.Items.Add("Kişilerden sil").Click += async (_, _) => await RemoveSelectedContactAsync();

            contactsToggleButton.Click += (_, _) =>
            {
                _isContactsOpen = !_isContactsOpen;
                RefreshContactsUi();
            };
        }

        private async Task RemoveSelectedContactAsync()
        {
            if (contactsList.SelectedItem is ContactInfo contact
                && Dialogs.Confirm(this, "Kişiyi sil", $"{contact.Username} kişilerinizden silinsin mi? Sohbet geçmişiniz silinmez.", "Sil", danger: true))
            {
                await _client.RemoveContactAsync(contact.UserId);
            }
        }

        /// <summary>Sunucudan gelen kişi listesini uygular; ilk listeden sonraki farkları bildirir.</summary>
        private void SetContacts(IReadOnlyList<ContactInfo>? contacts)
        {
            IReadOnlyList<ContactInfo> updated = contacts ?? Array.Empty<ContactInfo>();

            if (_hasReceivedContacts)
            {
                AnnounceContactChanges(_contacts, updated);
            }

            _contacts = updated;
            _hasReceivedContacts = true;
            RefreshContactsUi();
            RebuildSearch();
            conversationList.Invalidate();
            UpdateHeader();

            // Aramadan eklenen kişinin sohbeti, eklendiği anlaşılınca açılır.
            if (_pendingOpenAfterAdd is { } pending
                && updated.FirstOrDefault(c => UsernameValidator.Comparer.Equals(c.Username, pending)) is { } added)
            {
                _pendingOpenAfterAdd = null;
                ClearSearch();
                OpenPrivate(added.Username);
            }
        }

        private void AnnounceContactChanges(IReadOnlyList<ContactInfo> before, IReadOnlyList<ContactInfo> after)
        {
            var oldIds = before.Select(c => c.UserId).ToHashSet();
            var newIds = after.Select(c => c.UserId).ToHashSet();

            foreach (ContactInfo added in after.Where(c => !oldIds.Contains(c.UserId)))
            {
                ShowNotice($"{added.Username} kişilerinize eklendi.");
            }

            foreach (ContactInfo removed in before.Where(c => !newIds.Contains(c.UserId)))
            {
                ShowNotice($"{removed.Username} kişilerinizden silindi.");
            }
        }

        private void ClearContacts()
        {
            _contacts = Array.Empty<ContactInfo>();
            _hasReceivedContacts = false;
            _pendingOpenAfterAdd = null;
            RefreshContactsUi();
        }

        /// <summary>Kişi listesini yeniden kurar: çevrimiçi olanlar üstte, seçim korunur.</summary>
        private void RefreshContactsUi()
        {
            string? selectedId = (contactsList.SelectedItem as ContactInfo)?.UserId;

            contactsList.BeginUpdate();
            contactsList.Items.Clear();
            foreach (ContactInfo contact in _contacts
                         .OrderByDescending(c => _online.Contains(c.Username))
                         .ThenBy(c => c.Username, StringComparer.OrdinalIgnoreCase))
            {
                int index = contactsList.Items.Add(contact);
                if (contact.UserId == selectedId)
                {
                    contactsList.SelectedIndex = index;
                }
            }

            contactsList.EndUpdate();

            contactsToggleButton.Text = $"{(_isContactsOpen ? "▾" : "▸")}  KİŞİLER — {_contacts.Count}";
            contactsCard.Height = _isContactsOpen ? ContactsCardOpenHeight : ContactsCardClosedHeight;
            contactsList.Visible = _isContactsOpen && _contacts.Count > 0;
            contactsEmptyLabel.Visible = _isContactsOpen && _contacts.Count == 0;
        }

        private void PaintContact(DrawItemEventArgs e, object item, bool hover)
        {
            var contact = (ContactInfo)item;
            Graphics g = e.Graphics;
            RowPainting.Background(e, hover);

            bool isOnline = _online.Contains(contact.Username);
            int size = g.S(28);
            var avatar = new Rectangle(e.Bounds.X + g.S(16), e.Bounds.Y + (e.Bounds.Height - size) / 2, size, size);
            RowPainting.Avatar(g, avatar, contact.Username, Theme.ColorFor(contact.Username), isOnline,
                RowPainting.IsSelected(e) ? Theme.Selected : hover ? Theme.Hover : Theme.Sidebar);

            int textX = avatar.Right + g.S(10);
            RowPainting.Text(g, contact.Username, contactsList.Font,
                new Rectangle(textX, e.Bounds.Y, e.Bounds.Right - textX - g.S(12), e.Bounds.Height), isOnline ? Theme.Text : Theme.Muted);
        }
    }
}