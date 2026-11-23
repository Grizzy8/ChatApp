namespace ChatShared;

/// <summary>
/// Sabit rollerin yetki kuralları. Sunucu her işlemden önce bunlara danışır; istemci de düğmeleri
/// aynı kurallarla açıp kapatır. Aşama C'de serbest rollere geçildiğinde yalnızca bu sınıf değişir.
/// </summary>
public static class GroupPermissions
{
    /// <summary>Üye davet edebilir: kurucu ve yönetici.</summary>
    public static bool CanInvite(GroupRole role) => role is GroupRole.Owner or GroupRole.Admin;

    /// <summary>
    /// Hedef üyeyi gruptan çıkarabilir: kurucu kurucu dışındakileri, yönetici yalnızca sıradan üyeleri çıkarır.
    /// Kimse kurucuyu çıkaramaz.
    /// </summary>
    public static bool CanRemove(GroupRole actor, GroupRole target) => actor switch
    {
        GroupRole.Owner => target != GroupRole.Owner,
        GroupRole.Admin => target == GroupRole.Member,
        _ => false
    };

    /// <summary>Üyeyi yönetici yapabilir / yöneticiliğini alabilir: yalnızca kurucu.</summary>
    public static bool CanChangeRoles(GroupRole role) => role == GroupRole.Owner;

    /// <summary>Grubun adını değiştirebilir: yalnızca kurucu.</summary>
    public static bool CanRename(GroupRole role) => role == GroupRole.Owner;

    /// <summary>Grubu silebilir: yalnızca kurucu.</summary>
    public static bool CanDeleteGroup(GroupRole role) => role == GroupRole.Owner;

    /// <summary>Gruptan kendi isteğiyle ayrılabilir: kurucu hariç herkes (kurucu grubu silmelidir).</summary>
    public static bool CanLeave(GroupRole role) => role != GroupRole.Owner;
}
