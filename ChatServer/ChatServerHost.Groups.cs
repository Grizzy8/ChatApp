using ChatServer.Storage;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer;

// Gruplar: kurma, davet, kabul/red, üye yönetimi, roller ve grup mesajı.
// Yetki ve sınır denetimleri GroupRepository içinde, değişiklikle aynı işlemde yapılır; burası yalnızca
// girdiyi doğrular, sonucu hata koduna çevirir ve ilgili çevrimiçi kullanıcılara güncel durumu gönderir.
// Grup mesajlarının içeriği log'a yazılmaz. Çevrimdışı üyeler için mesaj üye başına kuyruğa alınır ve girişte teslim edilip silinir.
public sealed partial class ChatServerHost
{
    private static bool IsGroupRequest(MessageType type) => type is
        MessageType.CreateGroup or MessageType.InviteToGroup or MessageType.AcceptInvite or MessageType.DeclineInvite
        or MessageType.GetGroupMembers or MessageType.GroupMessage or MessageType.LeaveGroup or MessageType.RemoveMember
        or MessageType.PromoteMember or MessageType.DemoteMember or MessageType.DeleteGroup or MessageType.RenameGroup;

    private async Task HandleGroupRequestAsync(ClientSession session, ChatMessage message, CancellationToken token)
    {
        try
        {
            ErrorCode problem = await ProcessGroupRequestAsync(session, message, token).ConfigureAwait(false);
            if (problem != ErrorCode.None)
            {
                await RejectMessageAsync(session, problem, token).ConfigureAwait(false);
            }
        }
        catch (SqliteException ex)
        {
            WriteLog($"{Describe(session)} için grup işleminde veritabanı hatası: {ex.Message}", ServerLogLevel.Error);
            await SendErrorAsync(session, ErrorCode.ServerError, token).ConfigureAwait(false);
        }
    }

    private async Task<ErrorCode> ProcessGroupRequestAsync(ClientSession session, ChatMessage message, CancellationToken token)
    {
        string userId = session.UserId!;

        if (message.Type == MessageType.CreateGroup)
        {
            return await CreateGroupAsync(session, message.Text, token).ConfigureAwait(false);
        }

        if (!UserId.TryNormalize(message.Recipient, out string? groupId))
        {
            return ErrorCode.InvalidMessage;
        }

        switch (message.Type)
        {
            case MessageType.GroupMessage:
                return await SendGroupMessageAsync(session, groupId, message.Text, token).ConfigureAwait(false);

            case MessageType.GetGroupMembers:
                IReadOnlyList<GroupMemberRecord>? members = await _groups.GetMembersIfMemberAsync(groupId, userId, token).ConfigureAwait(false);
                if (members is null)
                {
                    return ErrorCode.GroupNotFound;
                }

                await _router.SendToAsync(session, MembersMessage(groupId, members), token).ConfigureAwait(false);
                return ErrorCode.None;

            case MessageType.InviteToGroup:
                return await InviteAsync(session, groupId, message.Text, token).ConfigureAwait(false);

            case MessageType.AcceptInvite:
                GroupResult accepted = await _groups.AcceptInviteAsync(groupId, userId, token).ConfigureAwait(false);
                if (accepted == GroupResult.Ok)
                {
                    await SendInviteListAsync(session, token).ConfigureAwait(false);
                    await NotifyGroupChangedAsync(groupId, [], token).ConfigureAwait(false);
                }

                return ToErrorCode(accepted);

            case MessageType.DeclineInvite:
                GroupResult declined = await _groups.DeclineInviteAsync(groupId, userId, token).ConfigureAwait(false);
                if (declined == GroupResult.Ok)
                {
                    await SendInviteListAsync(session, token).ConfigureAwait(false);
                }

                return ToErrorCode(declined);

            case MessageType.LeaveGroup:
                GroupResult left = await _groups.LeaveAsync(groupId, userId, token).ConfigureAwait(false);
                if (left == GroupResult.Ok)
                {
                    await NotifyGroupChangedAsync(groupId, [userId], token).ConfigureAwait(false);
                }

                return ToErrorCode(left);

            case MessageType.RenameGroup:
                if (!GroupNameValidator.TryNormalize(message.Text, out string newName))
                {
                    return ErrorCode.GroupNameInvalid;
                }

                GroupResult renamed = await _groups.RenameAsync(groupId, userId, newName, token).ConfigureAwait(false);
                if (renamed == GroupResult.Ok)
                {
                    WriteLog($"{session.Username} bir grubun adını değiştirdi ({groupId}).");
                    await NotifyGroupChangedAsync(groupId, [], token).ConfigureAwait(false);
                }

                return ToErrorCode(renamed);

            case MessageType.DeleteGroup:
                return await DeleteGroupAsync(session, groupId, token).ConfigureAwait(false);

            case MessageType.RemoveMember or MessageType.PromoteMember or MessageType.DemoteMember:
                return await ChangeMemberAsync(session, message.Type, groupId, message.Text, token).ConfigureAwait(false);

            default:
                return ErrorCode.InvalidMessage;
        }
    }

    private async Task<ErrorCode> CreateGroupAsync(ClientSession session, string? name, CancellationToken token)
    {
        if (!GroupNameValidator.TryNormalize(name, out string normalized))
        {
            return ErrorCode.GroupNameInvalid;
        }

        (GroupResult result, GroupRecord? group) = await _groups.CreateAsync(session.UserId!, normalized, token).ConfigureAwait(false);
        if (result != GroupResult.Ok)
        {
            return ToErrorCode(result);
        }

        WriteLog($"{session.Username} bir grup kurdu ({group!.Id}).");
        await NotifyGroupChangedAsync(group.Id, [], token).ConfigureAwait(false);
        return ErrorCode.None;
    }

    private async Task<ErrorCode> InviteAsync(ClientSession session, string groupId, string? query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > ProtocolConstants.MaxContactQueryLength)
        {
            return ErrorCode.InvalidMessage;
        }

        UserRecord? invitee = await _users.FindByUsernameOrIdAsync(query, token).ConfigureAwait(false);
        if (invitee is null)
        {
            return ErrorCode.ContactNotFound;
        }

        GroupResult result = await _groups.InviteAsync(groupId, session.UserId!, invitee.Id, token).ConfigureAwait(false);
        if (result != GroupResult.Ok)
        {
            return ToErrorCode(result);
        }

        WriteLog($"{session.Username} gruba ({groupId}) bir kullanıcıyı davet etti.");
        await _router.SendToAsync(session, new ChatMessage(MessageType.InviteSent, Recipient: groupId, Text: invitee.Username), token).ConfigureAwait(false);

        if (_registry.TryGetActive(invitee.Username, out ClientSession? target))
        {
            await SendInviteListAsync(target, token).ConfigureAwait(false);
        }

        return ErrorCode.None;
    }

    private async Task<ErrorCode> ChangeMemberAsync(ClientSession session, MessageType type, string groupId, string? targetText, CancellationToken token)
    {
        if (!UserId.TryNormalize(targetText, out string? targetId))
        {
            return ErrorCode.InvalidMessage;
        }

        string actorId = session.UserId!;
        GroupResult result = type switch
        {
            MessageType.RemoveMember => await _groups.RemoveMemberAsync(groupId, actorId, targetId, token).ConfigureAwait(false),
            MessageType.PromoteMember => await _groups.SetRoleAsync(groupId, actorId, targetId, GroupRole.Admin, token).ConfigureAwait(false),
            _ => await _groups.SetRoleAsync(groupId, actorId, targetId, GroupRole.Member, token).ConfigureAwait(false)
        };

        if (result != GroupResult.Ok)
        {
            return ToErrorCode(result);
        }

        // Çıkarılan kişi artık üye değildir: kendi grup listesini yenilemesi için ayrıca bildirilir.
        await NotifyGroupChangedAsync(groupId, type == MessageType.RemoveMember ? [targetId] : [], token).ConfigureAwait(false);
        return ErrorCode.None;
    }

    private async Task<ErrorCode> DeleteGroupAsync(ClientSession session, string groupId, CancellationToken token)
    {
        // Silinince üyelik kayıtları da gider; bildirim için üyeler önceden alınır.
        IReadOnlyList<GroupMemberRecord>? members = await _groups.GetMembersIfMemberAsync(groupId, session.UserId!, token).ConfigureAwait(false);
        if (members is null)
        {
            return ErrorCode.GroupNotFound;
        }

        IReadOnlyList<string> invitees = await _groups.ListInviteeUsernamesAsync(groupId, token).ConfigureAwait(false);
        GroupResult result = await _groups.DeleteAsync(groupId, session.UserId!, token).ConfigureAwait(false);
        if (result != GroupResult.Ok)
        {
            return ToErrorCode(result);
        }

        WriteLog($"{session.Username} bir grubu sildi ({groupId}).");
        foreach (GroupMemberRecord member in members)
        {
            if (_registry.TryGetActive(member.Username, out ClientSession? target))
            {
                await SendGroupListAsync(target, token).ConfigureAwait(false);
            }
        }

        // Bekleyen davetler grupla birlikte silindi; çevrimiçi davetlilerin listesi yenilenir.
        foreach (string invitee in invitees)
        {
            if (_registry.TryGetActive(invitee, out ClientSession? target))
            {
                await SendInviteListAsync(target, token).ConfigureAwait(false);
            }
        }

        return ErrorCode.None;
    }

    private async Task<ErrorCode> SendGroupMessageAsync(ClientSession session, string groupId, string? text, CancellationToken token)
    {
        ErrorCode problem = ValidateMessageText(text);
        if (problem != ErrorCode.None)
        {
            return problem;
        }

        IReadOnlyList<GroupMemberRecord>? members = await _groups.GetMembersIfMemberAsync(groupId, session.UserId!, token).ConfigureAwait(false);
        if (members is null)
        {
            return ErrorCode.GroupNotFound;
        }

        WriteLog($"{session.Username} → grup {groupId}: [grup mesajı]");

        var delivery = new ChatMessage(MessageType.GroupMessage, Sender: session.Username, Recipient: groupId, Text: text, Timestamp: DateTime.Now);
        var offlineIds = new List<string>();
        foreach (GroupMemberRecord member in members)
        {
            bool delivered = _registry.TryGetActive(member.Username, out ClientSession? target)
                && await _router.SendToAsync(target, delivery, token).ConfigureAwait(false);

            if (!delivered && member.UserId != session.UserId)
            {
                offlineIds.Add(member.UserId); // çevrimdışı (ya da tam bu sırada düştü): girince teslim edilmek üzere bekletilir
            }
        }

        if (offlineIds.Count > 0)
        {
            int queued = await _messages.EnqueueGroupAsync(session.UserId!, groupId, offlineIds, text!, DateTime.UtcNow, token).ConfigureAwait(false);
            WriteLog($"Grup {groupId}: {queued}/{offlineIds.Count} çevrimdışı üye için mesaj bekletiliyor.");
        }
        return ErrorCode.None;
    }

    /// <summary>Girişte kullanıcıya grup listesini ve bekleyen davetleri (çevrimdışıyken gelenler dahil) gönderir.</summary>
    private async Task SendGroupStateAsync(ClientSession session, CancellationToken token)
    {
        await SendGroupListAsync(session, token).ConfigureAwait(false);
        await SendInviteListAsync(session, token).ConfigureAwait(false);
    }

    private async Task SendGroupListAsync(ClientSession session, CancellationToken token)
    {
        try
        {
            IReadOnlyList<GroupRecord> groups = await _groups.ListAsync(session.UserId!, token).ConfigureAwait(false);
            var list = groups.Select(g => new GroupInfo(g.Id, g.Name, g.Role)).ToList();
            await _router.SendToAsync(session, new ChatMessage(MessageType.GroupList, Groups: list), token).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            WriteLog($"{Describe(session)} için grup listesi okunamadı: {ex.Message}", ServerLogLevel.Error);
            await SendErrorAsync(session, ErrorCode.ServerError, token).ConfigureAwait(false);
        }
    }

    private async Task SendInviteListAsync(ClientSession session, CancellationToken token)
    {
        try
        {
            IReadOnlyList<GroupInviteRecord> invites = await _groups.ListInvitesAsync(session.UserId!, token).ConfigureAwait(false);
            var list = invites.Select(i => new GroupInviteInfo(i.GroupId, i.GroupName, i.InviterUsername)).ToList();
            await _router.SendToAsync(session, new ChatMessage(MessageType.InviteList, Invites: list), token).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            WriteLog($"{Describe(session)} için davet listesi okunamadı: {ex.Message}", ServerLogLevel.Error);
            await SendErrorAsync(session, ErrorCode.ServerError, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Grubun durumu değişti: çevrimiçi her üyeye kendi grup listesi ve güncel üye listesi gönderilir.
    /// <paramref name="formerMemberIds"/> artık üye olmayan (çıkarılan/ayrılan) kullanıcılardır; çevrimiçiyseler yalnızca grup listesi yenilenir.
    /// </summary>
    private async Task NotifyGroupChangedAsync(string groupId, IReadOnlyList<string> formerMemberIds, CancellationToken token)
    {
        foreach (string userId in formerMemberIds)
        {
            UserRecord? former = await _users.FindByIdAsync(userId, token).ConfigureAwait(false);
            if (former is not null && _registry.TryGetActive(former.Username, out ClientSession? formerSession))
            {
                await SendGroupListAsync(formerSession, token).ConfigureAwait(false);
            }
        }

        IReadOnlyList<GroupMemberRecord> members = await _groups.ListMembersAsync(groupId, token).ConfigureAwait(false);
        ChatMessage membersMessage = MembersMessage(groupId, members);

        foreach (GroupMemberRecord member in members)
        {
            if (_registry.TryGetActive(member.Username, out ClientSession? target))
            {
                await SendGroupListAsync(target, token).ConfigureAwait(false);
                await _router.SendToAsync(target, membersMessage, token).ConfigureAwait(false);
            }
        }
    }

    private static ChatMessage MembersMessage(string groupId, IReadOnlyList<GroupMemberRecord> members) =>
        new(MessageType.GroupMembers, Recipient: groupId, Members: members.Select(m => new GroupMemberInfo(m.UserId, m.Username, m.Role)).ToList());

    private static ErrorCode ToErrorCode(GroupResult result) => result switch
    {
        GroupResult.Ok => ErrorCode.None,
        GroupResult.GroupNotFound => ErrorCode.GroupNotFound,
        GroupResult.NotAuthorized => ErrorCode.NotAuthorized,
        GroupResult.MemberNotFound => ErrorCode.MemberNotFound,
        GroupResult.AlreadyMember => ErrorCode.AlreadyGroupMember,
        GroupResult.AlreadyInvited => ErrorCode.AlreadyInvited,
        GroupResult.GroupFull => ErrorCode.GroupFull,
        GroupResult.InviteLimitReached => ErrorCode.InviteLimitReached,
        GroupResult.GroupLimitReached => ErrorCode.GroupLimitReached,
        GroupResult.InviteNotFound => ErrorCode.InviteNotFound,
        GroupResult.OwnerCannotLeave => ErrorCode.OwnerCannotLeave,
        _ => ErrorCode.ServerError
    };
}
