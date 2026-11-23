using ChatShared;

namespace ChatShared.Test;

public class PasswordValidatorTests
{
    [Theory]
    [InlineData("12345678")]
    [InlineData("şifre-çok-güzel")]
    [InlineData("boşluklu parola 1")]
    public void Validate_ReturnsNone_ForAcceptablePassword(string password)
    {
        Assert.Equal(ErrorCode.None, PasswordValidator.Validate(password));
    }

    [Fact]
    public void Validate_AcceptsExactBoundaryLengths()
    {
        Assert.Equal(ErrorCode.None, PasswordValidator.Validate(new string('a', ProtocolConstants.PasswordMinLength)));
        Assert.Equal(ErrorCode.None, PasswordValidator.Validate(new string('a', ProtocolConstants.PasswordMaxLength)));
    }

    [Fact]
    public void Validate_RejectsTooShortAndTooLong()
    {
        Assert.Equal(ErrorCode.PasswordInvalid, PasswordValidator.Validate(new string('a', ProtocolConstants.PasswordMinLength - 1)));
        Assert.Equal(ErrorCode.PasswordInvalid, PasswordValidator.Validate(new string('a', ProtocolConstants.PasswordMaxLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("        ")]   // yalnızca boşluk
    public void Validate_RejectsMissingOrBlank(string? password)
    {
        Assert.Equal(ErrorCode.PasswordInvalid, PasswordValidator.Validate(password));
    }

    [Fact]
    public void IsValid_MatchesValidate()
    {
        Assert.True(PasswordValidator.IsValid("geçerli-parola"));
        Assert.False(PasswordValidator.IsValid("kısa"));
    }
}
