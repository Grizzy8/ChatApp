using ChatShared;

namespace ChatClient;

/// <summary>Grup rollerinin Türkçe adları.</summary>
public static class RoleNames
{
    public static string Describe(GroupRole role) => role switch
    {
        GroupRole.Owner => "Kurucu",
        GroupRole.Admin => "Yönetici",
        _ => "Üye"
    };
}
