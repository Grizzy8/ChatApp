using System.Runtime.InteropServices;
using ChatShared;

namespace ChatClient
{
    // Sohbetler: sol listedeki sohbetler, seçili sohbetin gösterimi, mesaj gönderme/alma ve yerel geçmiş.
    public partial class ClientForm
    {
        private readonly Dictionary<string, Conversation> _conversations = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _online = new(StringComparer.OrdinalIgnoreCase);
        private readonly Font _boldFont = new("Segoe UI", 10f, FontStyle.Bold);
        private readonly Font _smallFont = new("Segoe UI", 8.5f);
        private readonly ContextMenuStrip _conversationMenu = Theme.NewMenu();

        private Conversation? _current;
        private ConversationStore? _store;
        private IReadOnlyList<StoredConversation> _storedIndex = Array.Empty<StoredConversation>();
        private string _myName = string.Empty;
        private bool _isSyncingList;
        private bool _isMembersPanelOpen = true;

        private bool IsMe(string? username) => UsernameValidator.Comparer.Equals(username, _myName);

        private void InitializeConversations()
        {
            conversationList.Painter = PaintConversation;
            conversationList.SelectedIndexChanged += OnConversationSelected;
            conversationList.MouseUp += OnConversationListMouseUp;

            ToolStripItem settingsItem = _conversationMenu.Items.Add("Grup ayarları");
            settingsItem.Click += (_, _) => OpenCurrentGroupSettings();

            sendButton.Click += async (_, _) => await SendCurrentAsync();
            messageTextBox.KeyDown += (_, e) => SubmitOnEnter(e, () => _ = SendCurrentAsync());
            settingsButton.Click += (_, _) => OpenCurrentGroupSettings();
            membersToggleButton.Click += (_, _) =>
            {
                _isMembersPanelOpen = !_isMembersPanelOpen;
                UpdateHeader();
                UpdateMembersPanel();
            };

            headerPanel.MouseUp += OnHeaderMouseUp;
            titleLabel.MouseUp += OnHeaderMouseUp;
            subtitleLabel.MouseUp += OnHeaderMouseUp;

            onlineMembersList.Painter = (e, item, hover) => PaintMember(e, item, hover, onlineList: true);
            offlineMembersList.Painter = (e, item, hover) => PaintMember(e, item, hover, onlineList: false);
            onlineMembersList.IsSelectable = _ => false;
            offlineMembersList.IsSelectable = _ => false;
            membersPanel.Resize += (_, _) => UpdateMembersPanel();

            copyIdButton.Click += CopyIdButton_Click;
            disconnectButton.Click += DisconnectButton_Click;
            profileStrip.Paint += (_, e) =>
            {
                int size = e.Graphics.S(36);
                var bounds = new Rectangle(e.Graphics.S(14), (profileStrip.Height - size) / 2, size, size);
                RowPainting.Avatar(e.Graphics, bounds, _myName, Theme.ColorFor(_myName), online: true, Theme.Darkest);
            };
        }

        // ---- oturum ----

        /// <summary>Giriş kabul edildi: yerel geçmişi aç, sol listeyi kur ve sohbet ekranını göster.</summary>
        private void BeginSession(ChatMessage joined)
        {
            EndSession();

            _myName = joined.Sender ?? _client.Username ?? string.Empty;
            _online.UnionWith(joined.Users ?? Array.Empty<string>());
            profileNameLabel.Text = _myName;
            profileIdLabel.Text = $"ID: {joined.UserId ?? _client.UserId}";

            OpenStore();
            foreach (StoredConversation stored in _storedIndex.Where(s => s.Key.StartsWith("p_", StringComparison.Ordinal)))
            {
                _conversations[stored.Key] = new Conversation(stored.Key, ConversationKind.Private, stored.Key[2..])
                {
                    LastActivity = stored.LastTime,
                    Unread = stored.Unread
                };
            }

            loginView.Visible = false;
            mainView.Visible = true;
            ShowNoConversation();
            RefreshConversationList();
            RefreshContactsUi();
        }

        private void OpenStore()
        {
            try
            {
                _store = new ConversationStore(_myName);
                _storedIndex = _store.LoadIndex();
            }
            catch (Exception ex) when (ConversationStore.IsFileError(ex))
            {
                _store = null;
                _storedIndex = Array.Empty<StoredConversation>();
                ShowNotice($"Sohbet geçmişi açılamadı, mesajlar kaydedilmeyecek: {ex.Message}", isError: true);
            }
        }

        /// <summary>Bağlantı bitti: tüm oturum durumunu sıfırlar (yerel geçmiş diskte kalır).</summary>
        private void EndSession()
        {
            _settingsDialog?.Close();
            _invitesDialog?.Close();

            _current = null;
            _conversations.Clear();
            _online.Clear();
            _store = null;
            _storedIndex = Array.Empty<StoredConversation>();
            _myName = string.Empty;

            _view.Clear();
            conversationList.Items.Clear();
            onlineMembersList.Items.Clear();
            offlineMembersList.Items.Clear();
            searchBox.Clear();
            messageTextBox.Clear();
            ClearContacts();
            ClearGroups();
            ShowNoConversation();
            mainView.Visible = false;
        }

        private void ShowNoConversation()
        {
            chatArea.Visible = false;
            emptyLabel.Visible = true;
        }

        // ---- çevrimiçi durum ----

        private void SetPresence(string username, bool isOnline)
        {
            if (isOnline ? _online.Add(username) : _online.Remove(username))
            {
                OnPresenceChanged();
            }
        }

        private void ReplacePresence(IEnumerable<string> users)
        {
            _online.Clear();
            _online.UnionWith(users);
            OnPresenceChanged();
        }

        private bool IsOnline(string username) => IsMe(username) || _online.Contains(username);

        private void OnPresenceChanged()
        {
            conversationList.Invalidate();
            contactsList.Invalidate();
            searchList.Invalidate();
            UpdateHeader();
            UpdateMembersPanel();
            _settingsDialog?.RefreshPresence();
        }

        // ---- sohbet listesi ----

        private Conversation GetOrCreatePrivate(string peer)
        {
            string key = ConversationKey.ForPrivate(peer);
            if (!_conversations.TryGetValue(key, out Conversation? conversation))
            {
                conversation = new Conversation(key, ConversationKind.Private, peer);
                _conversations[key] = conversation;
            }

            return conversation;
        }

        private void RefreshConversationList()
        {
            _isSyncingList = true;
            conversationList.BeginUpdate();
            conversationList.Items.Clear();
            foreach (Conversation conversation in _conversations.Values
                         .OrderByDescending(c => c.LastActivity)
                         .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase))
            {
                int index = conversationList.Items.Add(conversation);
                if (conversation == _current)
                {
                    conversationList.SelectedIndex = index;
                }
            }

            conversationList.EndUpdate();
            _isSyncingList = false;
        }

        private void OnConversationSelected(object? sender, EventArgs e)
        {
            if (!_isSyncingList && conversationList.SelectedItem is Conversation conversation)
            {
                OpenConversation(conversation);
            }
        }

        private void OnConversationListMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && conversationList.ItemAt(e.Location) is Conversation { IsGroup: true })
            {
                _conversationMenu.Show(conversationList, e.Location);
            }
        }

        private void OnHeaderMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _current is { IsGroup: true })
            {
                OpenCurrentGroupSettings();
            }
        }

        private void PaintConversation(DrawItemEventArgs e, object item, bool hover)
        {
            var conversation = (Conversation)item;
            Graphics g = e.Graphics;
            bool unread = conversation.Unread > 0;
            RowPainting.Background(e, hover);

            Color ring = RowPainting.IsSelected(e) ? Theme.Selected : hover ? Theme.Hover : Theme.Sidebar;
            int avatarSize = g.S(36);
            var avatar = new Rectangle(e.Bounds.X + g.S(16), e.Bounds.Y + (e.Bounds.Height - avatarSize) / 2, avatarSize, avatarSize);
            bool? online = conversation.IsGroup ? null : KnownOnlineState(conversation.Title);
            RowPainting.Avatar(g, avatar, conversation.Title, Theme.ColorFor(conversation.GroupId ?? conversation.Title), online, ring);

            int textX = avatar.Right + g.S(12);
            int width = e.Bounds.Right - textX - g.S(30);
            int half = e.Bounds.Height / 2;
            RowPainting.Text(g, conversation.Title, unread ? _boldFont : conversationList.Font,
                new Rectangle(textX, e.Bounds.Y + half - g.S(22), width, g.S(22)), unread ? Color.White : Theme.Text);

            (string subtitle, Color color) = unread
                ? ("Yeni mesaj", Color.White)
                : conversation.IsGroup ? ("Grup", Theme.Muted) : (OnlineText(conversation.Title), Theme.Muted);
            RowPainting.Text(g, subtitle, _smallFont, new Rectangle(textX, e.Bounds.Y + half, width, g.S(20)), color);

            if (unread)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var white = new SolidBrush(Color.White);
                int dot = g.S(10);
                g.FillEllipse(white, e.Bounds.Right - g.S(28), e.Bounds.Y + (e.Bounds.Height - dot) / 2, dot, dot);
                g.FillRectangle(white, e.Bounds.X, e.Bounds.Y + g.S(12), g.S(3), e.Bounds.Height - g.S(24));
            }
        }
        /// <summary>Çevrimiçi/çevrimdışı noktası yalnızca durumu bilinen kişiler için gösterilir; ilişkisiz kişi için null.</summary>
        private bool? KnownOnlineState(string username)
        {
            if (_online.Contains(username))
            {
                return true;
            }

            return _contacts.Any(c => UsernameValidator.Comparer.Equals(c.Username, username)) ? false : null;
        }

        private string OnlineText(string username) => KnownOnlineState(username) switch
        {
            true => "Çevrimiçi",
            false => "Çevrimdışı",
            _ => string.Empty
        };

        // ---- sohbeti açma ve gösterme ----

        private void OpenPrivate(string peer) => OpenConversation(GetOrCreatePrivate(peer));

        private void OpenConversation(Conversation conversation)
        {
            if (_current == conversation)
            {
                messageTextBox.Focus();
                return;
            }

            Conversation? previous = _current;
            _current = conversation;
            DiscardIfEmpty(previous);

            EnsureLoaded(conversation);
            MarkRead(conversation);

            chatArea.Visible = true;
            emptyLabel.Visible = false;
            RenderCurrent();
            UpdateHeader();

            if (conversation.GroupId is { } groupId && !_groupMembers.ContainsKey(groupId))
            {
                _ = _client.RequestGroupMembersAsync(groupId);
            }

            UpdateMembersPanel();
            RefreshConversationList();
            messageTextBox.Focus();
        }

        // Hiç mesajı olmayan özel sohbet (yalnızca açılmış, yazılmamış) listede kalmaz.
        private void DiscardIfEmpty(Conversation? conversation)
        {
            if (conversation is { IsGroup: false, Messages.Count: 0 })
            {
                _conversations.Remove(conversation.Key);
            }
        }

        private void EnsureLoaded(Conversation conversation)
        {
            if (conversation.Messages is not null)
            {
                return;
            }

            var messages = new List<ChatEntry>();
            try
            {
                foreach (StoredMessage stored in _store?.ReadMessages(conversation.Key, ClientConstants.MaxMessagesInMemory) ?? Array.Empty<StoredMessage>())
                {
                    messages.Add(new ChatEntry(stored.Time, stored.Sender, stored.Text, IsMe(stored.Sender)));
                }
            }
            catch (Exception ex) when (ConversationStore.IsFileError(ex))
            {
                ShowNotice($"Sohbet geçmişi okunamadı: {ex.Message}", isError: true);
            }

            conversation.Messages = messages;
        }

        private void RenderCurrent()
        {
            if (_current?.Messages is not { } messages)
            {
                return;
            }

            _view.BeginBulk();
            _view.Clear();
            ChatEntry? previous = null;
            foreach (ChatEntry entry in messages)
            {
                _view.AppendMessage(entry, previous, _current.IsGroup);
                previous = entry;
            }

            _view.EndBulk();
        }

        private void UpdateHeader()
        {
            if (_current is not { } conversation)
            {
                return;
            }

            titleLabel.Text = conversation.Title;
            subtitleLabel.Text = conversation.GroupId is { } groupId
                ? GroupSubtitle(groupId)
                : OnlineText(conversation.Title);

            membersToggleButton.Visible = settingsButton.Visible = conversation.IsGroup;
            membersToggleButton.Text = _isMembersPanelOpen ? "Üyeleri gizle" : "Üyeleri göster";
            // Dar pencerede üyeler paneli mesaj alanını ezmesin diye kendiliğinden gizlenir.
            membersPanel.Visible = conversation.IsGroup && _isMembersPanelOpen && ClientSize.Width >= Scale(900);
        }

        private string GroupSubtitle(string groupId)
        {
            if (!_groupMembers.TryGetValue(groupId, out IReadOnlyList<GroupMemberInfo>? members))
            {
                return "Grup";
            }

            int online = members.Count(m => IsOnline(m.Username));
            return $"{members.Count} üye · {online} çevrimiçi";
        }

        // ---- grup üyeleri paneli ----

        private void UpdateMembersPanel()
        {
            if (_current?.GroupId is not { } groupId || !membersPanel.Visible)
            {
                return;
            }

            IReadOnlyList<GroupMemberInfo> members = _groupMembers.GetValueOrDefault(groupId) ?? Array.Empty<GroupMemberInfo>();
            var ordered = members
                .OrderByDescending(m => m.Role)
                .ThenBy(m => m.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var online = ordered.Where(m => IsOnline(m.Username)).ToList();
            var offline = ordered.Where(m => !IsOnline(m.Username)).ToList();

            FillMemberList(onlineMembersList, online);
            FillMemberList(offlineMembersList, offline);
            onlineHeader.Text = $"ÇEVRİMİÇİ — {online.Count}";
            offlineHeader.Text = $"ÇEVRİMDIŞI — {offline.Count}";
            onlineMembersList.Height = Math.Min(online.Count * onlineMembersList.ItemHeight, Math.Max(40, membersPanel.Height * 55 / 100));
            subtitleLabel.Text = GroupSubtitle(groupId);
        }

        private static void FillMemberList(ThemedListBox list, List<GroupMemberInfo> members)
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (GroupMemberInfo member in members)
            {
                list.Items.Add(member);
            }

            list.EndUpdate();
        }

        private void PaintMember(DrawItemEventArgs e, object item, bool hover, bool onlineList)
        {
            var member = (GroupMemberInfo)item;
            Graphics g = e.Graphics;
            int size = g.S(28);
            var avatar = new Rectangle(e.Bounds.X + g.S(14), e.Bounds.Y + (e.Bounds.Height - size) / 2, size, size);
            RowPainting.Avatar(g, avatar, member.Username, Theme.ColorFor(member.Username), onlineList, Theme.Sidebar);

            int textX = avatar.Right + g.S(10);
            bool hasRole = member.Role != GroupRole.Member;
            int roleWidth = g.S(64);
            int nameWidth = e.Bounds.Right - textX - (hasRole ? roleWidth + g.S(8) : g.S(8));
            RowPainting.Text(g, IsMe(member.Username) ? $"{member.Username} (siz)" : member.Username, onlineMembersList.Font,
                new Rectangle(textX, e.Bounds.Y, nameWidth, e.Bounds.Height), onlineList ? Theme.Text : Theme.Muted);
            if (hasRole)
            {
                RowPainting.Text(g, RoleNames.Describe(member.Role), _smallFont,
                    new Rectangle(e.Bounds.Right - roleWidth - g.S(8), e.Bounds.Y, roleWidth, e.Bounds.Height), Theme.Muted, alignRight: true);
            }
        }
        // ---- mesajlar ----

        private void HandlePrivateMessage(ChatMessage message)
        {
            string? peer = IsMe(message.Sender) ? message.Recipient : message.Sender;
            if (peer is null)
            {
                return;
            }

            Conversation conversation = GetOrCreatePrivate(peer);
            AddMessage(conversation, message.Timestamp ?? DateTime.Now, message.Sender ?? "?", message.Text ?? string.Empty);

            if (message.Type == MessageType.PrivateQueued && _current == conversation)
            {
                _view.AppendNotice($"{peer} çevrimdışı; mesajınız giriş yaptığında ona iletilecek.");
                _view.ScrollToEnd();
            }
        }

        private void HandleGroupMessage(ChatMessage message)
        {
            if (message.Recipient is { } groupId
                && _conversations.TryGetValue(ConversationKey.ForGroup(groupId), out Conversation? conversation))
            {
                AddMessage(conversation, message.Timestamp ?? DateTime.Now, message.Sender ?? "?", message.Text ?? string.Empty);
            }
        }

        private void AddMessage(Conversation conversation, DateTime time, string sender, string text)
        {
            EnsureLoaded(conversation); // dosyaya yazmadan önce okunur; böylece mesaj iki kez görünmez
            var entry = new ChatEntry(time, sender, text, IsMe(sender));
            List<ChatEntry> messages = conversation.Messages!;
            ChatEntry? previous = messages.Count > 0 ? messages[^1] : null;

            messages.Add(entry);
            if (messages.Count > ClientConstants.MaxMessagesInMemory)
            {
                messages.RemoveRange(0, messages.Count - ClientConstants.MaxMessagesInMemory);
            }

            try
            {
                _store?.Append(conversation.Key, new StoredMessage(time, sender, text));
            }
            catch (Exception ex) when (ConversationStore.IsFileError(ex))
            {
                ShowNotice($"Mesaj yerel geçmişe kaydedilemedi: {ex.Message}", isError: true);
            }

            conversation.LastActivity = time > conversation.LastActivity ? time : conversation.LastActivity;

            if (conversation == _current)
            {
                _view.AppendMessage(entry, previous, conversation.IsGroup);
                _view.ScrollToEnd();
                MarkRead(conversation);
            }
            else if (!entry.IsOwn)
            {
                conversation.Unread++;
            }

            RefreshConversationList();
        }

        private void MarkRead(Conversation conversation)
        {
            conversation.Unread = 0;
            DateTime lastMessage = conversation.Messages is { Count: > 0 } messages ? messages[^1].Time : DateTime.MinValue;
            DateTime readUpTo = lastMessage > DateTime.Now ? lastMessage : DateTime.Now; // sunucu saati ileride olabilir

            try
            {
                _store?.MarkRead(conversation.Key, readUpTo);
            }
            catch (Exception ex) when (ConversationStore.IsFileError(ex))
            {
                ShowNotice($"Okundu bilgisi kaydedilemedi: {ex.Message}", isError: true);
            }
        }

        private async Task SendCurrentAsync()
        {
            Conversation? conversation = _current;
            string text = messageTextBox.Text.Trim();
            if (conversation is null || text.Length == 0)
            {
                return;
            }

            // Kutu hemen boşaltılır ki kullanıcı beklemeden yazmaya devam edebilsin; gönderme başarısızsa metin geri yüklenir.
            messageTextBox.Clear();
            messageTextBox.Focus();

            bool sent = conversation.GroupId is { } groupId
                ? await _client.SendGroupMessageAsync(groupId, text)
                : await _client.SendPrivateAsync(conversation.Title, text);

            if (!sent && !IsDisposed && messageTextBox.TextLength == 0)
            {
                messageTextBox.Text = text;
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
                ShowNotice($"ID'niz panoya kopyalandı: {id}");
            }
            catch (ExternalException)
            {
                // Pano başka bir uygulama tarafından kullanılıyor olabilir; ID yine de ekranda görünür.
                ShowNotice($"Pano kullanılamadı. ID'niz: {id}", isError: true);
            }
        }
    }
}
