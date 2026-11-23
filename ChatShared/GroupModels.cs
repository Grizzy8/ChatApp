namespace ChatShared;

/// <summary>Kullanıcının üyesi olduğu bir grup ve o gruptaki rolü.</summary>
public sealed record GroupInfo(string Id, string Name, GroupRole Role);

/// <summary>Bir grubun üyesi.</summary>
public sealed record GroupMemberInfo(string UserId, string Username, GroupRole Role);

/// <summary>Kullanıcıya gelen, henüz yanıtlanmamış grup daveti.</summary>
public sealed record GroupInviteInfo(string GroupId, string GroupName, string InviterUsername);
