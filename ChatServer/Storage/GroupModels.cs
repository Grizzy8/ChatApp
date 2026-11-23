using ChatShared;

namespace ChatServer.Storage;

/// <summary>Bir işlemin sonucu. Ok dışındaki değerlerde veritabanında hiçbir değişiklik yapılmaz.</summary>
public enum GroupResult
{
    Ok,
    GroupNotFound,
    NotAuthorized,
    MemberNotFound,
    AlreadyMember,
    AlreadyInvited,
    GroupFull,
    InviteLimitReached,
    GroupLimitReached,
    InviteNotFound,
    OwnerCannotLeave
}

public sealed record GroupRecord(string Id, string Name, GroupRole Role);

public sealed record GroupMemberRecord(string UserId, string Username, GroupRole Role);

public sealed record GroupInviteRecord(string GroupId, string GroupName, string InviterUsername);
