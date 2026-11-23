using ChatShared;

namespace ChatClient
{
    // Sol üstteki arama: kişilerde ve gruplarda yazdıkça süzer; tam kullanıcı adı ya da ID yazılırsa sunucudan da sorar.
    public partial class ClientForm
    {
        private const int SearchRowHeight = 46;
        private readonly Font _smallBoldFont = new("Segoe UI", 8f, FontStyle.Bold);

        private enum SearchRowKind
        {
            Header,
            Contact,
            Group,
            Remote,
            Hint
        }

        private sealed record SearchRow(SearchRowKind Kind, string Text, ContactInfo? Contact = null, GroupInfo? Group = null);

        private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = ClientConstants.SearchDebounceMs };

        // Sunucudan gelen son arama sonucu (yalnızca o anki sorgu için geçerlidir) ve eklenince açılacak kişi.
        private ContactInfo? _remoteResult;
        private bool _hasRemoteAnswer;
        private string? _pendingOpenAfterAdd;

        private string SearchQuery => searchBox.Text.Trim();

        private void InitializeSearch()
        {
            searchList.RowHeight = SearchRowHeight;
            searchList.Painter = PaintSearchRow;
            searchList.IsSelectable = _ => false; // satırlar tıklanır ama seçili kalmaz
            searchList.MouseClick += async (_, e) => await OnSearchRowClickedAsync(searchList.ItemAt(e.Location) as SearchRow, e.Button);

            searchBox.TextChanged += (_, _) => OnSearchTextChanged();
            searchBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    searchBox.Clear();
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SendRemoteSearch();
                }
            };

            _searchTimer.Tick += (_, _) => SendRemoteSearch();
        }

        private bool CanSearchRemote =>
            _client.State == ConnectionState.Connected
            && SearchQuery.Length >= ProtocolConstants.UsernameMinLength
            && SearchQuery.Length <= ProtocolConstants.MaxContactQueryLength;

        private void OnSearchTextChanged()
        {
            _searchTimer.Stop();
            _remoteResult = null;
            _hasRemoteAnswer = false;
            RebuildSearch();

            if (CanSearchRemote)
            {
                _searchTimer.Start();
            }
        }

        private void SendRemoteSearch()
        {
            _searchTimer.Stop();
            if (CanSearchRemote)
            {
                _ = _client.FindUserAsync(SearchQuery);
            }
        }

        /// <summary>Sunucu yanıtı; yalnızca hâlâ aynı sorgu yazılıysa kullanılır (yazmaya devam edilmiş olabilir).</summary>
        private void OnUserFound(ChatMessage message)
        {
            if (!string.Equals(message.Text, SearchQuery, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _remoteResult = message.Contacts?.FirstOrDefault();
            _hasRemoteAnswer = true;
            RebuildSearch();
        }

        private void ClearSearch() => searchBox.Clear();

        private void RebuildSearch()
        {
            string query = SearchQuery;
            bool searching = query.Length > 0;
            searchList.Visible = searching;
            conversationList.Visible = !searching;
            if (!searching)
            {
                return;
            }

            var rows = new List<SearchRow>();

            var contactMatches = _contacts
                .Where(c => c.Username.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(c.UserId, query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (contactMatches.Count > 0)
            {
                rows.Add(new SearchRow(SearchRowKind.Header, "KİŞİLER"));
                rows.AddRange(contactMatches.Select(c => new SearchRow(SearchRowKind.Contact, c.Username, Contact: c)));
            }

            var groupMatches = _groups
                .Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (groupMatches.Count > 0)
            {
                rows.Add(new SearchRow(SearchRowKind.Header, "GRUPLAR"));
                rows.AddRange(groupMatches.Select(g => new SearchRow(SearchRowKind.Group, g.Name, Group: g)));
            }

            if (_remoteResult is { } found && _contacts.All(c => c.UserId != found.UserId))
            {
                rows.Add(new SearchRow(SearchRowKind.Header, "DİĞER KULLANICILAR"));
                rows.Add(new SearchRow(SearchRowKind.Remote, found.Username, Contact: found));
            }

            if (rows.Count == 0)
            {
                bool pending = CanSearchRemote && !_hasRemoteAnswer;
                rows.Add(new SearchRow(SearchRowKind.Hint, pending
                    ? "Aranıyor..."
                    : "Sonuç yok. Kişi bulmak için tam kullanıcı adı ya da ID yazın."));
            }

            searchList.BeginUpdate();
            searchList.Items.Clear();
            foreach (SearchRow row in rows)
            {
                searchList.Items.Add(row);
            }

            searchList.EndUpdate();
        }

        private async Task OnSearchRowClickedAsync(SearchRow? row, MouseButtons button)
        {
            if (row is null || button != MouseButtons.Left)
            {
                return;
            }

            switch (row.Kind)
            {
                case SearchRowKind.Contact:
                    ClearSearch();
                    OpenPrivate(row.Contact!.Username);
                    break;
                case SearchRowKind.Group:
                    if (_conversations.TryGetValue(ConversationKey.ForGroup(row.Group!.Id), out Conversation? group))
                    {
                        ClearSearch();
                        OpenConversation(group);
                    }

                    break;
                case SearchRowKind.Remote:
                    await ConfirmAddRemoteUserAsync(row.Contact!);
                    break;
            }
        }

        private async Task ConfirmAddRemoteUserAsync(ContactInfo user)
        {
            bool confirmed = Dialogs.Confirm(this, "Kişilere ekle",
                $"{user.Username} (ID: {user.UserId}) kişilerinize eklensin mi?", "Kişilere ekle");
            if (!confirmed)
            {
                return;
            }

            _pendingOpenAfterAdd = user.Username;
            if (!await _client.AddContactAsync(user.UserId))
            {
                _pendingOpenAfterAdd = null;
            }
        }

        private void PaintSearchRow(DrawItemEventArgs e, object item, bool hover)
        {
            var row = (SearchRow)item;
            Graphics g = e.Graphics;

            if (row.Kind == SearchRowKind.Header)
            {
                RowPainting.Text(g, row.Text, _smallBoldFont,
                    new Rectangle(e.Bounds.X + g.S(18), e.Bounds.Y + g.S(8), e.Bounds.Width - g.S(30), e.Bounds.Height - g.S(8)), Theme.Muted);
                return;
            }

            if (row.Kind == SearchRowKind.Hint)
            {
                TextRenderer.DrawText(g, row.Text, searchList.Font,
                    new Rectangle(e.Bounds.X + g.S(18), e.Bounds.Y + g.S(4), e.Bounds.Width - g.S(36), e.Bounds.Height * 2),
                    Theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top);
                return;
            }

            RowPainting.Background(e, hover);
            int size = g.S(32);
            var avatar = new Rectangle(e.Bounds.X + g.S(16), e.Bounds.Y + (e.Bounds.Height - size) / 2, size, size);
            bool? online = row.Kind == SearchRowKind.Contact ? IsOnline(row.Text) : null;
            RowPainting.Avatar(g, avatar, row.Text, Theme.ColorFor(row.Group?.Id ?? row.Text), online, hover ? Theme.Hover : Theme.Sidebar);

            int textX = avatar.Right + g.S(12);
            int width = e.Bounds.Right - textX - g.S(14);
            bool hasSubtitle = row.Kind == SearchRowKind.Remote;
            int half = e.Bounds.Height / 2;
            RowPainting.Text(g, row.Text, searchList.Font,
                hasSubtitle ? new Rectangle(textX, e.Bounds.Y + half - g.S(22), width, g.S(22)) : new Rectangle(textX, e.Bounds.Y, width, e.Bounds.Height),
                Theme.Text);
            if (hasSubtitle)
            {
                RowPainting.Text(g, "Kişilere eklemek için tıklayın", _smallFont, new Rectangle(textX, e.Bounds.Y + half, width, g.S(18)), Theme.Muted);
            }
        }
    }
}