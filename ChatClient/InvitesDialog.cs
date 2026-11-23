using ChatShared;

namespace ChatClient;

/// <summary>Bekleyen grup davetleri: kabul ya da reddet. Sunucudan yeni liste geldikçe <see cref="SetInvites"/> ile güncellenir.</summary>
public sealed class InvitesDialog : ThemedForm
{
    private readonly ThemedListBox _list = new() { Dock = DockStyle.Fill, RowHeight = 54 };
    private readonly Label _emptyLabel = new() { Text = "Bekleyen davet yok.", AutoSize = true, ForeColor = Theme.Muted };
    private readonly Label _statusLabel = new() { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 8, 0, 0) };
    private readonly Button _acceptButton = NewButton("Kabul et", accent: true);
    private readonly Button _declineButton = NewButton("Reddet");
    private readonly Func<GroupInviteInfo, Task<bool>> _accept;
    private readonly Func<GroupInviteInfo, Task<bool>> _decline;

    public InvitesDialog(
        IReadOnlyList<GroupInviteInfo> invites,
        Func<GroupInviteInfo, Task<bool>> accept,
        Func<GroupInviteInfo, Task<bool>> decline)
        : base("Grup davetleri")
    {
        _accept = accept;
        _decline = decline;
        SetClientSizeLogical(420, 360);

        _list.Painter = PaintInvite;
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _acceptButton.Click += async (_, _) => await RunAsync(_accept);
        _declineButton.Click += async (_, _) => await RunAsync(_decline);

        var close = NewButton("Kapat");
        close.DialogResult = DialogResult.Cancel;
        CancelButton = close;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 6, 12, 10) };
        buttons.Controls.Add(close);
        buttons.Controls.Add(_declineButton);
        buttons.Controls.Add(_acceptButton);

        _statusLabel.Dock = DockStyle.Bottom;
        _statusLabel.Padding = new Padding(14, 0, 14, 0);

        _emptyLabel.Location = new Point(16, 14);

        Controls.Add(_list);
        Controls.Add(_emptyLabel);
        Controls.Add(_statusLabel);
        Controls.Add(buttons);
        Theme.Apply(this, Theme.Background);
        _list.BackColor = Theme.Sidebar;

        SetInvites(invites);
    }

    private GroupInviteInfo? Selected => _list.SelectedItem as GroupInviteInfo;

    public void SetInvites(IReadOnlyList<GroupInviteInfo> invites)
    {
        string? selectedId = Selected?.GroupId;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (GroupInviteInfo invite in invites)
        {
            int index = _list.Items.Add(invite);
            if (invite.GroupId == selectedId)
            {
                _list.SelectedIndex = index;
            }
        }

        _list.EndUpdate();
        _emptyLabel.Visible = invites.Count == 0;
        _list.Visible = invites.Count > 0;
        UpdateButtons();
    }

    public void ShowStatus(string text, bool isError)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError ? Theme.ErrorText : Theme.Muted;
    }

    private void UpdateButtons()
    {
        _acceptButton.Enabled = _declineButton.Enabled = Selected is not null;
    }

    private async Task RunAsync(Func<GroupInviteInfo, Task<bool>> action)
    {
        if (Selected is { } invite)
        {
            ShowStatus(string.Empty, isError: false);
            await action(invite);
        }
    }

    private void PaintInvite(DrawItemEventArgs e, object item, bool hover)
    {
        var invite = (GroupInviteInfo)item;
        Graphics g = e.Graphics;
        RowPainting.Background(e, hover);

        int size = g.S(36);
        var avatar = new Rectangle(e.Bounds.X + g.S(16), e.Bounds.Y + (e.Bounds.Height - size) / 2, size, size);
        RowPainting.Avatar(g, avatar, invite.GroupName, Theme.ColorFor(invite.GroupId), online: null, Theme.Sidebar);

        int textX = avatar.Right + g.S(12);
        int width = e.Bounds.Right - textX - g.S(14);
        int half = e.Bounds.Height / 2;
        RowPainting.Text(g, invite.GroupName, _list.Font, new Rectangle(textX, e.Bounds.Y + half - g.S(22), width, g.S(22)), Color.White);
        RowPainting.Text(g, $"Davet eden: {invite.InviterUsername}", _list.Font, new Rectangle(textX, e.Bounds.Y + half, width, g.S(20)), Theme.Muted);
    }
}