using ChatShared;

namespace ChatShared.Test;

public class UsernameValidatorTests
{
    [Theory]
    [InlineData("ali")]
    [InlineData("Ayşe")]
    [InlineData("ÇağlaÖzgür")]
    [InlineData("user_01")]
    [InlineData("123")]
    [InlineData("abcdefghijklmnopqrst")] // tam 20 karakter
    public void IsValid_ReturnsTrue_ForAcceptableNames(string name)
    {
        Assert.True(UsernameValidator.IsValid(name));
        Assert.Equal(ErrorCode.None, UsernameValidator.Validate(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]                      // 2 karakter: çok kısa
    [InlineData("abcdefghijklmnopqrstu")]   // 21 karakter: çok uzun
    [InlineData("ali veli")]                // boşluk
    [InlineData(" ali")]                    // baştaki boşluk
    [InlineData("ali!")]                    // özel karakter
    [InlineData("ali-veli")]
    [InlineData("a@b.com")]
    [InlineData("ali\n")]                   // kontrol karakteri
    public void IsValid_ReturnsFalse_ForUnacceptableNames(string? name)
    {
        Assert.False(UsernameValidator.IsValid(name));
        Assert.Equal(ErrorCode.UsernameInvalid, UsernameValidator.Validate(name));
    }

    [Fact]
    public void IsValid_AcceptsBoundaryLengths()
    {
        Assert.True(UsernameValidator.IsValid(new string('a', ProtocolConstants.UsernameMinLength)));
        Assert.True(UsernameValidator.IsValid(new string('a', ProtocolConstants.UsernameMaxLength)));
        Assert.False(UsernameValidator.IsValid(new string('a', ProtocolConstants.UsernameMinLength - 1)));
        Assert.False(UsernameValidator.IsValid(new string('a', ProtocolConstants.UsernameMaxLength + 1)));
    }

    [Theory]
    [InlineData("ali", "ALI")]
    [InlineData("Ali", "aLi")]
    [InlineData("ayşe", "AYŞE")]
    public void Comparer_TreatsDifferentCasingAsSameName(string a, string b)
    {
        Assert.True(UsernameValidator.Comparer.Equals(a, b));
        Assert.Equal(UsernameValidator.Comparer.GetHashCode(a), UsernameValidator.Comparer.GetHashCode(b));
    }

    [Fact]
    public void Comparer_TreatsDifferentNamesAsDifferent()
    {
        Assert.False(UsernameValidator.Comparer.Equals("ali", "veli"));
    }
}
