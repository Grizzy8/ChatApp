using ChatShared;

namespace ChatClient
{
    // "Gruplar" ve "Davetler" sekmeleri: grup kurma, davet, üye yönetimi ve grup mesajı hedefi.
    // Yetkiler sunucuda da denetlenir; buradaki düğme durumları yalnızca kullanıcıya yol göstermek içindir.
    public partial class ClientForm
    {
        private readonly TabPage groupsTab = new() { Text = "Gruplar (0)", Padding = new Padding(6), UseVisualStyleBackColor = true };
        private readonly TabPage invitesTab = new() { Text = "Davetler (0)", Padding = new Padding(6), UseVisualStyleBackColor = true };

        private readonly ListBox groupsListBox = NewList();
        private readonly ListBox membersListBox = NewList();
        private readonly ListBox invitesListBox = NewList();

        private readonly TextBox newGroupTextBox = new()
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "Yeni grup adı",
            MaxLength = ProtocolConstants.GroupNameMaxLength
        };

        private readonly TextBox inviteTextBox = new()
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "Davet: kullanıcı adı veya ID",
            MaxLength = ProtocolConstants.MaxContactQueryLength
        };

        private readonly Button createGroupButton = NewContactButton("Grup kur");
        private readonly Button inviteButton = NewContactButton("Davet et");
        private readonly Button writeToGroupButton = NewContactButton("Gruba yaz");
        private readonly Button removeMemberButton = NewContactButton("Üyeyi çıkar");
        private readonly Button toggleAdminButton = NewContactButton("Yönetici yap");
        private readonly Button leaveGroupButton = NewContactButton("Gruptan ayrıl");
        private readonly Button deleteGroupButton = NewContactButton("Grubu sil");
        private readonly Button acceptInviteButton = NewContactButton("Kabul et");
        private readonly Button declineInviteButton = NewContactButton("Reddet");

        private IReadOnlyList<GroupInfo> _groups = Array.Empty<GroupInfo>();
        private IReadOnlyList<GroupInviteInfo> _invites = Array.Empty<GroupInviteInfo>();
        private readonly Dictionary<string, IReadOnlyList<GroupMemberInfo>> _groupMembers = new();

        // İlk liste girişte gelir ve duyurulmaz (davetler hariç: bekleyen davet sayısı bildirilir).
        private bool _hasReceivedGroups;
        private bool _hasReceivedInvites;

        // Grup mesajının hedefi; kişi hedefiyle (_chosenRecipient) aynı anda yalnızca biri dolu olur.
        private string? _chosenGroupId;

        // Üyeler listesinde şu an hangi grubun üyelerinin gösterildiği.
        private string? _shownMembersGroupId;

        private sealed record GroupListItem(GroupInfo Group)
        {
            public override string ToString() => $"{Group.Name}  ·  {RoleNames.Describe(Group.Role)}";
        }

        private sealed record MemberListItem(GroupMemberInfo Member)
        {
            public override string ToString() => $"{Member.Username}  ·  {RoleNames.Describe(Member.Role)}";
        }

        private sealed record InviteListItem(GroupInviteInfo Invite)
        {
            public override string ToString() => $"{Invite.GroupName}  ←  {Invite.InviterUsername}";
        }

        private static ListBox NewList() => new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };

        private GroupInfo? SelectedGroup => (groupsListBox.SelectedItem as GroupListItem)?.Group;

        private GroupMemberInfo? SelectedMember => (membersListBox.SelectedItem as MemberListItem)?.Member;

        private string GroupNameOf(string groupId) => _groups.FirstOrDefault(g => g.Id == groupId)?.Name ?? "Grup";

        private void InitializeGroupsTab()
        {
            groupsTab.Controls.Add(BuildGroupsLayout());
            invitesTab.Controls.Add(BuildInvitesLayout());
            sideTabs.TabPages.Add(groupsTab);
            sideTabs.TabPages.Add(invitesTab);

            createGroupButton.Click += async (_, _) => await CreateGroupAsync();
            newGroupTextBox.KeyDown += (_, e) => SubmitOnEnter(e, () => _ = CreateGroupAsync());
            inviteButton.Click += async (_, _) => await InviteAsync();
            inviteTextBox.KeyDown += (_, e) => SubmitOnEnter(e, () => _ = InviteAsync());
            writeToGroupButton.Click += (_, _) => TargetSelectedGroup();
            groupsListBox.DoubleClick += (_, _) => TargetSelectedGroup();
            removeMemberButton.Click += async (_, _) => await RunMemberActionAsync((g, m) => _client.RemoveMemberAsync(g.Id, m.UserId));
            toggleAdminButton.Click += async (_, _) => await RunMemberActionAsync((g, m) => m.Role == GroupRole.Admin
                ? _client.DemoteMemberAsync(g.Id, m.UserId)
                : _client.PromoteMemberAsync(g.Id, m.UserId));
            leaveGroupButton.Click += async (_, _) => await RunGroupActionAsync(g => _client.LeaveGroupAsync(g.Id));
            deleteGroupButton.Click += OnDeleteGroupClick;
            acceptInviteButton.Click += async (_, _) => await RunInviteActionAsync(i => _client.AcceptInviteAsync(i.GroupId));
            declineInviteButton.Click += async (_, _) => await RunInviteActionAsync(i => _client.DeclineInviteAsync(i.GroupId));
            groupsListBox.SelectedIndexChanged += OnSelectedGroupChanged;
            membersListBox.SelectedIndexChanged += (_, _) => UpdateGroupButtons();
            invitesListBox.SelectedIndexChanged += (_, _) => UpdateGroupButtons();
        }

        private Control BuildGroupsLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var membersLabel = new Label { Text = "Seçili grubun üyeleri", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 6, 3, 2) };

            var inputs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inputs.Controls.Add(newGroupTextBox, 0, 0);
            inputs.Controls.Add(createGroupButton, 1, 0);
            inputs.Controls.Add(inviteTextBox, 0, 1);
            inputs.Controls.Add(inviteButton, 1, 1);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            foreach (Button button in new[] { writeToGroupButton, removeMemberButton, toggleAdminButton, leaveGroupButton, deleteGroupButton })
            {
                button.Anchor = AnchorStyles.None;
                button.Margin = new Padding(0, 0, 4, 2);
                actions.Controls.Add(button);
            }

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true };
            bottom.Controls.Add(inputs, 0, 0);
            bottom.Controls.Add(actions, 0, 1);

            var membersBlock = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            membersBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            membersBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            membersBlock.Controls.Add(membersLabel, 0, 0);
            membersBlock.Controls.Add(membersListBox, 0, 1);

            root.Controls.Add(groupsListBox, 0, 0);
            root.Controls.Add(membersBlock, 0, 1);
            root.SetRowSpan(membersBlock, 2);
            root.Controls.Add(bottom, 0, 3);
            return root;
        }

        private Control BuildInvitesLayout()
        {
            var buttons = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 2, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.Controls.Add(acceptInviteButton, 0, 0);
            buttons.Controls.Add(declineInviteButton, 1, 0);

            var panel = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add(invitesListBox);
            panel.Controls.Add(buttons);
            return panel;
        }

        private static void SubmitOnEnter(KeyEventArgs e, Action submit)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Enter, varsayılan düğmeyi (Gönder) tetiklemesin
                submit();
            }
        }

        // ---- kullanıcı eylemleri ----

        private async Task CreateGroupAsync()
        {
            string name = newGroupTextBox.Text.Trim();
            if (name.Length == 0)
            {
                newGroupTextBox.Focus();
                return;
            }

            newGroupTextBox.Clear();
            if (!await _client.CreateGroupAsync(name) && !IsDisposed)
            {
                newGroupTextBox.Text = name; // gönderilemedi: metin kaybolmasın
            }
        }

        private async Task InviteAsync()
        {
            GroupInfo? group = SelectedGroup;
            string query = inviteTextBox.Text.Trim();
            if (group is null || query.Length == 0)
            {
                inviteTextBox.Focus();
                return;
            }

            inviteTextBox.Clear();
            if (!await _client.InviteToGroupAsync(group.Id, query) && !IsDisposed)
            {
                inviteTextBox.Text = query;
            }
        }

        private async Task RunMemberActionAsync(Func<GroupInfo, GroupMemberInfo, Task<bool>> action)
        {
            if (SelectedGroup is { } group && SelectedMember is { } member)
            {
                await action(group, member);
            }
        }

        private async Task RunGroupActionAsync(Func<GroupInfo, Task<bool>> action)
        {
            if (SelectedGroup is { } group)
            {
                await action(group);
            }
        }

        private async Task RunInviteActionAsync(Func<GroupInviteInfo, Task<bool>> action)
        {
            if ((invitesListBox.SelectedItem as InviteListItem)?.Invite is { } invite)
            {
                await action(invite);
            }
        }

        private async void OnDeleteGroupClick(object? sender, EventArgs e)
        {
            if (SelectedGroup is not { } group)
            {
                return;
            }

            DialogResult answer = MessageBox.Show(this,
                $"\"{group.Name}\" grubu tüm üyeleri için silinecek. Bu işlem geri alınamaz. Devam edilsin mi?",
                "Grubu sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes)
            {
                await _client.DeleteGroupAsync(group.Id);
            }
        }

        private void TargetSelectedGroup()
        {
            if (SelectedGroup is not { } group)
            {
                return;
            }

            _isSyncingRecipient = true;
            try { onlineListBox.ClearSelected(); } finally { _isSyncingRecipient = false; }

            _chosenRecipient = null;
            _chosenGroupId = group.Id;
            UpdateRecipientUi();
            messageTextBox.Focus();
        }

        private void OnSelectedGroupChanged(object? sender, EventArgs e)
        {
            // Üye listesi yalnızca gösterilen grup değişince sıfırlanır; aynı grubun liste güncellemelerinde üye seçimi korunur.
            string? groupId = SelectedGroup?.Id;
            if (groupId != _shownMembersGroupId)
            {
                _shownMembersGroupId = groupId;
                membersListBox.Items.Clear();
                if (groupId is not null)
                {
                    if (_groupMembers.TryGetValue(groupId, out IReadOnlyList<GroupMemberInfo>? cached))
                    {
                        FillMembers(cached);
                    }
                    else
                    {
                        _ = _client.RequestGroupMembersAsync(groupId);
                    }
                }
            }

            UpdateGroupButtons();
        }

        // ---- sunucudan gelen durum ----

        private void SetGroups(IReadOnlyList<GroupInfo>? groups)
        {
            IReadOnlyList<GroupInfo> updated = groups ?? Array.Empty<GroupInfo>();

            if (_hasReceivedGroups)
            {
                AnnounceGroupChanges(_groups, updated);
            }

            _groups = updated;
            _hasReceivedGroups = true;

            foreach (string gone in _groupMembers.Keys.Where(id => updated.All(g => g.Id != id)).ToList())
            {
                _groupMembers.Remove(gone);
            }

            if (_chosenGroupId is not null && updated.All(g => g.Id != _chosenGroupId))
            {
                _chosenGroupId = null; // hedef grup artık yok
            }

            string? selectedId = SelectedGroup?.Id;
            groupsListBox.BeginUpdate();
            groupsListBox.Items.Clear();
            foreach (GroupInfo group in updated)
            {
                int index = groupsListBox.Items.Add(new GroupListItem(group));
                if (group.Id == selectedId)
                {
                    groupsListBox.SelectedIndex = index;
                }
            }
            groupsListBox.EndUpdate();

            groupsTab.Text = $"Gruplar ({updated.Count})";
            OnSelectedGroupChanged(this, EventArgs.Empty);
            UpdateRecipientUi();
        }

        private void AnnounceGroupChanges(IReadOnlyList<GroupInfo> before, IReadOnlyList<GroupInfo> after)
        {
            foreach (GroupInfo added in after.Where(a => before.All(b => b.Id != a.Id)))
            {
                _chatView.AppendSystem($"\"{added.Name}\" grubuna katıldınız ({RoleNames.Describe(added.Role)}).");
            }

            foreach (GroupInfo removed in before.Where(b => after.All(a => a.Id != b.Id)))
            {
                _chatView.AppendSystem($"\"{removed.Name}\" grubundan ayrıldınız ya da çıkarıldınız (grup silinmiş de olabilir).");
            }

            foreach (GroupInfo changed in after)
            {
                GroupInfo? old = before.FirstOrDefault(b => b.Id == changed.Id);
                if (old is not null && old.Role != changed.Role)
                {
                    _chatView.AppendSystem($"\"{changed.Name}\" grubundaki rolünüz: {RoleNames.Describe(changed.Role)}.");
                }
            }
        }

        private void SetInvites(IReadOnlyList<GroupInviteInfo>? invites)
        {
            IReadOnlyList<GroupInviteInfo> updated = invites ?? Array.Empty<GroupInviteInfo>();

            if (!_hasReceivedInvites)
            {
                if (updated.Count > 0)
                {
                    _chatView.AppendSystem($"{updated.Count} bekleyen grup davetiniz var. Davetler sekmesinden yanıtlayabilirsiniz.");
                }
            }
            else
            {
                foreach (GroupInviteInfo added in updated.Where(a => _invites.All(b => b.GroupId != a.GroupId)))
                {
                    _chatView.AppendSystem($"{added.InviterUsername} sizi \"{added.GroupName}\" grubuna davet etti. Davetler sekmesinden yanıtlayabilirsiniz.");
                }
            }

            _invites = updated;
            _hasReceivedInvites = true;

            string? selectedId = (invitesListBox.SelectedItem as InviteListItem)?.Invite.GroupId;
            invitesListBox.BeginUpdate();
            invitesListBox.Items.Clear();
            foreach (GroupInviteInfo invite in updated)
            {
                int index = invitesListBox.Items.Add(new InviteListItem(invite));
                if (invite.GroupId == selectedId)
                {
                    invitesListBox.SelectedIndex = index;
                }
            }
            invitesListBox.EndUpdate();

            invitesTab.Text = $"Davetler ({updated.Count})";
            UpdateGroupButtons();
        }

        private void SetGroupMembers(string? groupId, IReadOnlyList<GroupMemberInfo>? members)
        {
            if (groupId is null || members is null)
            {
                return;
            }

            _groupMembers[groupId] = members;
            if (SelectedGroup?.Id == groupId)
            {
                FillMembers(members);
            }
        }

        private void FillMembers(IReadOnlyList<GroupMemberInfo> members)
        {
            string? selectedId = SelectedMember?.UserId;
            membersListBox.BeginUpdate();
            membersListBox.Items.Clear();
            foreach (GroupMemberInfo member in members)
            {
                int index = membersListBox.Items.Add(new MemberListItem(member));
                if (member.UserId == selectedId)
                {
                    membersListBox.SelectedIndex = index;
                }
            }
            membersListBox.EndUpdate();
            UpdateGroupButtons();
        }

        private void ShowGroupMessage(ChatMessage message)
        {
            DateTime time = message.Timestamp ?? DateTime.Now;
            string sender = message.Sender ?? "?";
            string text = message.Text ?? string.Empty;
            string groupName = GroupNameOf(message.Recipient ?? string.Empty);
            bool isOwn = UsernameValidator.Comparer.Equals(message.Sender, _client.Username);

            _chatView.AppendGroup(time, groupName, sender, text, isOwn);
            _ = SaveToHistoryAsync(time, ChatHistoryStore.FormatGroup(time, groupName, sender, text));
        }

        private void ClearGroups()
        {
            _groups = Array.Empty<GroupInfo>();
            _invites = Array.Empty<GroupInviteInfo>();
            _groupMembers.Clear();
            _hasReceivedGroups = false;
            _hasReceivedInvites = false;
            _shownMembersGroupId = null;
            groupsListBox.Items.Clear();
            membersListBox.Items.Clear();
            invitesListBox.Items.Clear();
            groupsTab.Text = "Gruplar (0)";
            invitesTab.Text = "Davetler (0)";
            UpdateGroupButtons();
        }

        /// <summary>Düğmeleri rol kurallarına göre açar/kapatır (sunucu yine de her isteği denetler).</summary>
        private void UpdateGroupButtons()
        {
            bool connected = _client.State == ConnectionState.Connected;
            GroupInfo? group = SelectedGroup;
            GroupMemberInfo? member = SelectedMember;
            bool isOther = member is not null && member.UserId != _client.UserId;

            createGroupButton.Enabled = connected;
            newGroupTextBox.Enabled = connected;
            inviteTextBox.Enabled = connected && group is not null && GroupPermissions.CanInvite(group.Role);
            inviteButton.Enabled = inviteTextBox.Enabled;
            writeToGroupButton.Enabled = connected && group is not null;
            removeMemberButton.Enabled = connected && group is not null && isOther && GroupPermissions.CanRemove(group.Role, member!.Role);
            toggleAdminButton.Enabled = connected && group is not null && isOther
                && GroupPermissions.CanChangeRoles(group.Role) && member!.Role != GroupRole.Owner;
            toggleAdminButton.Text = member?.Role == GroupRole.Admin ? "Yöneticiliği al" : "Yönetici yap";
            leaveGroupButton.Enabled = connected && group is not null && GroupPermissions.CanLeave(group.Role);
            deleteGroupButton.Enabled = connected && group is not null && GroupPermissions.CanDeleteGroup(group.Role);

            bool hasInvite = connected && invitesListBox.SelectedItem is not null;
            acceptInviteButton.Enabled = hasInvite;
            declineInviteButton.Enabled = hasInvite;
        }
    }
}
