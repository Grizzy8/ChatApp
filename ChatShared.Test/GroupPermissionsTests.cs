using ChatShared;

namespace ChatShared.Test;

public class GroupPermissionsTests
{
    [Theory]
    [InlineData(GroupRole.Owner, true)]
    [InlineData(GroupRole.Admin, true)]
    [InlineData(GroupRole.Member, false)]
    public void CanInvite_OnlyOwnerAndAdmin(GroupRole role, bool expected) =>
        Assert.Equal(expected, GroupPermissions.CanInvite(role));

    [Theory]
    [InlineData(GroupRole.Owner, GroupRole.Owner, false)]
    [InlineData(GroupRole.Owner, GroupRole.Admin, true)]
    [InlineData(GroupRole.Owner, GroupRole.Member, true)]
    [InlineData(GroupRole.Admin, GroupRole.Owner, false)]
    [InlineData(GroupRole.Admin, GroupRole.Admin, false)]
    [InlineData(GroupRole.Admin, GroupRole.Member, true)]
    [InlineData(GroupRole.Member, GroupRole.Member, false)]
    [InlineData(GroupRole.Member, GroupRole.Admin, false)]
    [InlineData(GroupRole.Member, GroupRole.Owner, false)]
    public void CanRemove_FollowsHierarchy(GroupRole actor, GroupRole target, bool expected) =>
        Assert.Equal(expected, GroupPermissions.CanRemove(actor, target));

    [Theory]
    [InlineData(GroupRole.Owner, true)]
    [InlineData(GroupRole.Admin, false)]
    [InlineData(GroupRole.Member, false)]
    public void RoleChangeAndDelete_OnlyOwner(GroupRole role, bool expected)
    {
        Assert.Equal(expected, GroupPermissions.CanChangeRoles(role));
        Assert.Equal(expected, GroupPermissions.CanDeleteGroup(role));
        Assert.Equal(expected, GroupPermissions.CanRename(role));
    }

    [Theory]
    [InlineData(GroupRole.Owner, false)]
    [InlineData(GroupRole.Admin, true)]
    [InlineData(GroupRole.Member, true)]
    public void CanLeave_EveryoneButOwner(GroupRole role, bool expected) =>
        Assert.Equal(expected, GroupPermissions.CanLeave(role));
}

public class GroupNameValidatorTests
{
    [Theory]
    [InlineData("Arkadaşlar", "Arkadaşlar")]
    [InlineData("  Ders Grubu  ", "Ders Grubu")]
    [InlineData("a", "a")]
    public void TryNormalize_AcceptsAndTrims(string input, string expected)
    {
        Assert.True(GroupNameValidator.TryNormalize(input, out string name));
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("iki\nsatır")]
    [InlineData("sekme\tli")]
    public void TryNormalize_RejectsEmptyOrControlCharacters(string? input) =>
        Assert.False(GroupNameValidator.TryNormalize(input, out _));

    [Fact]
    public void TryNormalize_EnforcesMaxLengthAfterTrim()
    {
        Assert.True(GroupNameValidator.TryNormalize(new string('a', ProtocolConstants.GroupNameMaxLength), out _));
        Assert.False(GroupNameValidator.TryNormalize(new string('a', ProtocolConstants.GroupNameMaxLength + 1), out _));
        Assert.True(GroupNameValidator.TryNormalize(" " + new string('a', ProtocolConstants.GroupNameMaxLength) + " ", out _));
    }
}
