namespace ChatShared;

/// <summary>Bir üyenin gruptaki sabit rolü (Aşama A). Yetkiler <see cref="GroupPermissions"/> içinde tanımlıdır.</summary>
public enum GroupRole
{
    /// <summary>Grubu kuran kişi; her şeyi yapabilir, gruptan ayrılamaz (grubu silebilir).</summary>
    Owner,

    /// <summary>Üye davet edebilir ve (kurucu/yönetici olmayan) üyeleri çıkarabilir.</summary>
    Admin,

    /// <summary>Yalnızca mesaj yazar ve gruptan ayrılabilir.</summary>
    Member
}
