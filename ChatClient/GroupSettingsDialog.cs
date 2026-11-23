using ChatShared;

namespace ChatClient;

/// <summary>
/// Grup ayarları: üyeler, roller, davet, ad değiştirme, gruptan ayrılma ve silme.
/// Hangi düğmenin açık olduğu role göre belirlenir (yetkiler sunucuda da denetlenir). Grup durumu değiştikçe
/// <see cref="Refresh"/> ile güncellenir.
/// </summary>
public sealed class GroupSettingsDialog : ThemedForm
{
    private readonly ChatClientService _client;
    private readonly string _myUserId;
    private readonly Func<string, bool> _isOnline;

    private readonly Label _nameLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Color.White, MaximumSize = new Size(300, 0) };
    private readonly Label _roleLabel = new() { AutoSize = true, ForeColor = Theme.Muted };
    private readonly Button _renameButton = NewButton("Adı değiştir");
    private readonly ThemedListBox _members = new() { Dock = DockStyle.Fill, RowHeight = 44 };
    private readonly Button _roleButton = NewButton("Yönetici yap", minWidth: 120);
    private readonly Button _removeButton = NewButton("Gruptan çıkar", minWidth: 120);
    private readonly TextBox _inviteBox = new() { Width = 230, PlaceholderText = "Kullanıcı adı veya ID", MaxLength = ProtocolConstants.MaxContactQueryLength };
    private readonly Button _inviteButton = NewButton("Davet et", accent: true);
    private readonly Button _leaveButton = NewButton("Gruptan ayrıl");
    private readonly Button _deleteButton = NewButton("Grubu sil", danger: true);
    private readonly Label _statusLabel = new() { AutoSize = true, MaximumSize = new Size(440, 0) };

    private GroupInfo _group;
    private IReadOnlyList<GroupMemberInfo> _memberInfos = Array.Empty<GroupMemberInfo>();

    public GroupSettingsDialog(ChatClientService client, GroupInfo group, IReadOnlyList<GroupMemberInfo> members, Func<string, bool> isOnline)
        : base("Grup ayarları")
    {
        _client = client;
        _myUserId = client.UserId ?? string.Empty;
        _isOnline = isOnline;
        _group = group;
        SetClientSizeLogical(480, 560);

        BuildLayout();
        _members.Painter = PaintMember;
        _members.SelectedIndexChanged += (_, _) => UpdateButtons();
        _renameButton.Click += async (_, _) => await RenameAsync();
        _roleButton.Click += async (_, _) => await ToggleRoleAsync();
        _removeButton.Click += async (_, _) => await RemoveAsync();
        _inviteButton.Click += async (_, _) => await InviteAsync();
        _inviteBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = InviteAsync();
            }
        };
        _leaveButton.Click += async (_, _) => await LeaveAsync();
        _deleteButton.Click += async (_, _) => await DeleteAsync();

        Refresh(group, members);
    }

    public string GroupId => _group.Id;

    private GroupMemberInfo? Selected => _members.SelectedItem as GroupMemberInfo;

    /// <summary>Grup ya da üye bilgisi değişince çağrılır; seçim korunur.</summary>
    public void Refresh(GroupInfo group, IReadOnlyList<GroupMemberInfo> members)
    {
        _group = group;
        _memberInfos = members;
        _nameLabel.Text = group.Name;
        _roleLabel.Text = $"Rolünüz: {RoleNames.Describe(group.Role)} · {members.Count} üye";

        string? selectedId = Selected?.UserId;
        _members.BeginUpdate();
        _members.Items.Clear();
        foreach (GroupMemberInfo member in members.OrderByDescending(m => m.Role).ThenBy(m => m.Username, StringComparer.OrdinalIgnoreCase))
        {
            int index = _members.Items.Add(member);
            if (member.UserId == selectedId)
            {
                _members.SelectedIndex = index;
            }
        }

        _members.EndUpdate();
        UpdateButtons();
    }

    /// <summary>Çevrimiçi durum değişince satır noktalarını yeniler.</summary>
    public void RefreshPresence() => _members.Invalidate();

    public void ShowStatus(string text, bool isError)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError ? Theme.ErrorText : Theme.Online;
    }

    private void BuildLayout()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(16, 14, 16, 8) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(_nameLabel, 0, 0);
        header.Controls.Add(_roleLabel, 0, 1);
        header.Controls.Add(_renameButton, 1, 0);
        header.SetRowSpan(_renameButton, 2);

        var memberActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(12, 4, 12, 0) };
        memberActions.Controls.Add(_roleButton);
        memberActions.Controls.Add(_removeButton);

        var invite = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(12, 6, 12, 0) };
        invite.Controls.Add(_inviteBox);
        invite.Controls.Add(_inviteButton);
        _inviteBox.Margin = new Padding(4, 6, 0, 0);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 6, 12, 10) };
        Button close = NewButton("Kapat");
        close.DialogResult = DialogResult.Cancel;
        CancelButton = close;
        footer.Controls.Add(close);
        footer.Controls.Add(_deleteButton);
        footer.Controls.Add(_leaveButton);

        _statusLabel.Dock = DockStyle.Bottom;
        _statusLabel.Padding = new Padding(16, 6, 16, 0);

        // Dock sırası: Fill önce, kenarlar sonra eklenir (Windows Forms ters sırayla yerleştirir).
        Controls.Add(_members);
        Controls.Add(memberActions);
        Controls.Add(invite);
        Controls.Add(_statusLabel);
        Controls.Add(footer);
        Controls.Add(header);
        Theme.Apply(this, Theme.Background);
        _members.BackColor = Theme.Sidebar;
        _nameLabel.ForeColor = Color.White;
        _roleLabel.ForeColor = Theme.Muted;
    }

    private void UpdateButtons()
    {
        GroupMemberInfo? member = Selected;
        bool isOther = member is not null && member.UserId != _myUserId;

        _renameButton.Visible = GroupPermissions.CanRename(_group.Role);
        _inviteBox.Enabled = _inviteButton.Enabled = GroupPermissions.CanInvite(_group.Role);
        _removeButton.Enabled = isOther && GroupPermissions.CanRemove(_group.Role, member!.Role);
        _roleButton.Enabled = isOther && GroupPermissions.CanChangeRoles(_group.Role) && member!.Role != GroupRole.Owner;
        _roleButton.Text = member?.Role == GroupRole.Admin ? "Yöneticiliği al" : "Yönetici yap";
        _leaveButton.Visible = GroupPermissions.CanLeave(_group.Role);
        _deleteButton.Visible = GroupPermissions.CanDeleteGroup(_group.Role);
    }

    private async Task RenameAsync()
    {
        string? name = Dialogs.Prompt(this, "Grup adını değiştir", "Yeni grup adı", _group.Name, ProtocolConstants.GroupNameMaxLength, "Kaydet");
        if (name is not null && name != _group.Name)
        {
            ShowStatus(string.Empty, false);
            await _client.RenameGroupAsync(_group.Id, name);
        }
    }

    private async Task ToggleRoleAsync()
    {
        if (Selected is { } member)
        {
            ShowStatus(string.Empty, false);
            await (member.Role == GroupRole.Admin
                ? _client.DemoteMemberAsync(_group.Id, member.UserId)
                : _client.PromoteMemberAsync(_group.Id, member.UserId));
        }
    }

    private async Task RemoveAsync()
    {
        if (Selected is { } member
            && Dialogs.Confirm(this, "Üyeyi çıkar", $"{member.Username} gruptan çıkarılsın mı?", "Çıkar", danger: true))
        {
            ShowStatus(string.Empty, false);
            await _client.RemoveMemberAsync(_group.Id, member.UserId);
        }
    }

    private async Task InviteAsync()
    {
        string query = _inviteBox.Text.Trim();
        if (query.Length == 0)
        {
            _inviteBox.Focus();
            return;
        }

        _inviteBox.Clear();
        ShowStatus(string.Empty, false);
        if (!await _client.InviteToGroupAsync(_group.Id, query) && !IsDisposed)
        {
            _inviteBox.Text = query;
        }
    }

    private async Task LeaveAsync()
    {
        if (Dialogs.Confirm(this, "Gruptan ayrıl", $"\"{_group.Name}\" grubundan ayrılmak istiyor musunuz?", "Ayrıl", danger: true))
        {
            await _client.LeaveGroupAsync(_group.Id);
        }
    }

    private async Task DeleteAsync()
    {
        if (Dialogs.Confirm(this, "Grubu sil", $"\"{_group.Name}\" grubu tüm üyeler için silinecek. Bu işlem geri alınamaz.", "Grubu sil", danger: true))
        {
            await _client.DeleteGroupAsync(_group.Id);
        }
    }

    private void PaintMember(DrawItemEventArgs e, object item, bool hover)
    {
        var member = (GroupMemberInfo)item;
        Graphics g = e.Graphics;
        RowPainting.Background(e, hover);

        bool isMe = member.UserId == _myUserId;
        int size = g.S(32);
        var avatar = new Rectangle(e.Bounds.X + g.S(16), e.Bounds.Y + (e.Bounds.Height - size) / 2, size, size);
        RowPainting.Avatar(g, avatar, member.Username, Theme.ColorFor(member.Username), isMe || _isOnline(member.Username), Theme.Sidebar);

        int textX = avatar.Right + g.S(12);
        int roleWidth = g.S(90);
        string name = isMe ? $"{member.Username} (siz)" : member.Username;
        RowPainting.Text(g, name, _members.Font, new Rectangle(textX, e.Bounds.Y, e.Bounds.Right - textX - roleWidth - g.S(20), e.Bounds.Height), Color.White);
        RowPainting.Text(g, RoleNames.Describe(member.Role), _members.Font,
            new Rectangle(e.Bounds.Right - roleWidth - g.S(14), e.Bounds.Y, roleWidth, e.Bounds.Height),
            member.Role == GroupRole.Member ? Theme.Muted : Theme.ColorFor(RoleNames.Describe(member.Role)), alignRight: true);
    }
}