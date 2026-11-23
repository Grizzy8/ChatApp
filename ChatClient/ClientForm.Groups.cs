using ChatShared;

namespace ChatClient
{
    // Gruplar ve davetler. Yetkiler sunucuda da denetlenir; arayüzdeki düğme durumları yalnızca yol göstericidir.
    public partial class ClientForm
    {
        private IReadOnlyList<GroupInfo> _groups = Array.Empty<GroupInfo>();
        private IReadOnlyList<GroupInviteInfo> _invites = Array.Empty<GroupInviteInfo>();
        private readonly Dictionary<string, IReadOnlyList<GroupMemberInfo>> _groupMembers = new();

        // İlk liste girişte gelir ve duyurulmaz (davetler hariç: bekleyen davet sayısı bildirilir).
        private bool _hasReceivedGroups;
        private bool _hasReceivedInvites;

        // Kurduğumuz ya da davetini kabul ettiğimiz grup listede belirince sohbeti otomatik açılır.
        private bool _openNextAddedGroup;

        private GroupSettingsDialog? _settingsDialog;
        private InvitesDialog? _invitesDialog;

        private void InitializeGroups()
        {
            newGroupButton.Click += async (_, _) => await CreateGroupAsync();
            invitesButton.Click += (_, _) => ShowInvites();
        }

        // ---- kullanıcı eylemleri ----

        private async Task CreateGroupAsync()
        {
            string? name = Dialogs.Prompt(this, "Yeni grup", "Grup adı", string.Empty, ProtocolConstants.GroupNameMaxLength, "Oluştur");
            if (name is null)
            {
                return;
            }

            _openNextAddedGroup = true;
            if (!await _client.CreateGroupAsync(name))
            {
                _openNextAddedGroup = false;
            }
        }

        private void ShowInvites()
        {
            if (_invitesDialog is not null)
            {
                return;
            }

            using var dialog = new InvitesDialog(_invites, AcceptInviteAsync, invite => _client.DeclineInviteAsync(invite.GroupId));
            _invitesDialog = dialog;
            try
            {
                dialog.ShowDialog(this);
            }
            finally
            {
                _invitesDialog = null;
            }
        }

        private async Task<bool> AcceptInviteAsync(GroupInviteInfo invite)
        {
            _openNextAddedGroup = true;
            bool sent = await _client.AcceptInviteAsync(invite.GroupId);
            if (!sent)
            {
                _openNextAddedGroup = false;
            }

            return sent;
        }

        private void OpenCurrentGroupSettings()
        {
            if (_current?.GroupId is not { } groupId || _settingsDialog is not null)
            {
                return;
            }

            GroupInfo? group = _groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null)
            {
                return;
            }

            if (!_groupMembers.ContainsKey(groupId))
            {
                _ = _client.RequestGroupMembersAsync(groupId);
            }

            using var dialog = new GroupSettingsDialog(
                _client, group, _groupMembers.GetValueOrDefault(groupId) ?? Array.Empty<GroupMemberInfo>(), IsOnline);
            _settingsDialog = dialog;
            try
            {
                dialog.ShowDialog(this);
            }
            finally
            {
                _settingsDialog = null;
            }
        }

        // ---- sunucudan gelen durum ----

        private void SetGroups(IReadOnlyList<GroupInfo>? groups)
        {
            IReadOnlyList<GroupInfo> updated = groups ?? Array.Empty<GroupInfo>();
            bool isFirstList = !_hasReceivedGroups;

            if (!isFirstList)
            {
                AnnounceGroupChanges(_groups, updated);
            }

            _groups = updated;
            _hasReceivedGroups = true;

            var ids = updated.Select(g => g.Id).ToHashSet();
            RemoveVanishedGroups(ids);

            Conversation? toOpen = null;
            foreach (GroupInfo group in updated)
            {
                string key = ConversationKey.ForGroup(group.Id);
                if (_conversations.TryGetValue(key, out Conversation? existing))
                {
                    existing.Title = group.Name;
                    continue;
                }

                StoredConversation? stored = _storedIndex.FirstOrDefault(s => s.Key == key);
                var created = new Conversation(key, ConversationKind.Group, group.Name, group.Id)
                {
                    // Girişteki ilk listede mesajsız gruplar altta kalır; oturum sırasında katılınan grup en üste çıkar.
                    LastActivity = stored?.LastTime ?? (isFirstList ? DateTime.MinValue : DateTime.Now),
                    Unread = stored?.Unread ?? 0
                };
                _conversations[key] = created;

                if (_openNextAddedGroup && !isFirstList)
                {
                    toOpen = created;
                    _openNextAddedGroup = false;
                }
            }

            if (_settingsDialog is { } dialog)
            {
                GroupInfo? shown = updated.FirstOrDefault(g => g.Id == dialog.GroupId);
                if (shown is null)
                {
                    dialog.Close();
                }
                else
                {
                    dialog.Refresh(shown, _groupMembers.GetValueOrDefault(shown.Id) ?? Array.Empty<GroupMemberInfo>());
                }
            }

            RefreshConversationList();
            RebuildSearch();
            if (toOpen is not null)
            {
                OpenConversation(toOpen);
            }
            else
            {
                UpdateHeader();
                UpdateMembersPanel();
            }
        }

        private void RemoveVanishedGroups(HashSet<string> currentIds)
        {
            foreach (Conversation gone in _conversations.Values.Where(c => c.IsGroup && !currentIds.Contains(c.GroupId!)).ToList())
            {
                _conversations.Remove(gone.Key);
                if (_current == gone)
                {
                    _current = null;
                    ShowNoConversation();
                }
            }

            foreach (string id in _groupMembers.Keys.Where(id => !currentIds.Contains(id)).ToList())
            {
                _groupMembers.Remove(id);
            }
        }

        private void AnnounceGroupChanges(IReadOnlyList<GroupInfo> before, IReadOnlyList<GroupInfo> after)
        {
            foreach (GroupInfo added in after.Where(a => before.All(b => b.Id != a.Id)))
            {
                ShowNotice($"\"{added.Name}\" grubuna katıldınız ({RoleNames.Describe(added.Role)}).");
            }

            foreach (GroupInfo removed in before.Where(b => after.All(a => a.Id != b.Id)))
            {
                ShowNotice($"\"{removed.Name}\" grubundan ayrıldınız ya da çıkarıldınız (grup silinmiş de olabilir).");
            }

            foreach (GroupInfo changed in after)
            {
                GroupInfo? old = before.FirstOrDefault(b => b.Id == changed.Id);
                if (old is not null && old.Name != changed.Name)
                {
                    ShowNotice($"\"{old.Name}\" grubunun adı \"{changed.Name}\" oldu.");
                }
                else if (old is not null && old.Role != changed.Role)
                {
                    ShowNotice($"\"{changed.Name}\" grubundaki rolünüz: {RoleNames.Describe(changed.Role)}.");
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
                    ShowNotice($"{updated.Count} bekleyen grup davetiniz var.");
                }
            }
            else
            {
                foreach (GroupInviteInfo added in updated.Where(a => _invites.All(b => b.GroupId != a.GroupId)))
                {
                    ShowNotice($"{added.InviterUsername} sizi \"{added.GroupName}\" grubuna davet etti.");
                }
            }

            _invites = updated;
            _hasReceivedInvites = true;
            UpdateInvitesButton();
            _invitesDialog?.SetInvites(updated);
        }

        private void UpdateInvitesButton()
        {
            bool hasInvites = _invites.Count > 0;
            invitesButton.Text = hasInvites ? $"Davetler ({_invites.Count})" : "Davetler";
            invitesButton.BackColor = hasInvites ? Theme.Accent : Theme.Input;
            invitesButton.FlatAppearance.MouseOverBackColor = hasInvites ? Theme.AccentHover : Theme.Selected;
        }

        private void SetGroupMembers(string? groupId, IReadOnlyList<GroupMemberInfo>? members)
        {
            if (groupId is null || members is null)
            {
                return;
            }

            _groupMembers[groupId] = members;

            if (_current?.GroupId == groupId)
            {
                UpdateHeader();
                UpdateMembersPanel();
            }

            if (_settingsDialog is { } dialog && dialog.GroupId == groupId
                && _groups.FirstOrDefault(g => g.Id == groupId) is { } group)
            {
                dialog.Refresh(group, members);
            }
        }

        private void ClearGroups()
        {
            _groups = Array.Empty<GroupInfo>();
            _invites = Array.Empty<GroupInviteInfo>();
            _groupMembers.Clear();
            _hasReceivedGroups = false;
            _hasReceivedInvites = false;
            _openNextAddedGroup = false;
            UpdateInvitesButton();
        }
    }
}
