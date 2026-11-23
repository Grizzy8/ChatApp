using ChatShared;

namespace ChatShared.Test;

public class UserIdTests
{
    [Fact]
    public void Generate_ReturnsValidFormat()
    {
        string id = UserId.Generate();

        Assert.True(UserId.IsValid(id));
        Assert.Equal(9, id.Length);
        Assert.Equal('-', id[4]);
    }

    [Fact]
    public void Generate_UsesOnlyUnambiguousCharacters()
    {
        for (int i = 0; i < 500; i++)
        {
            string id = UserId.Generate().Replace("-", string.Empty);
            Assert.DoesNotContain(id, c => "01ILO".Contains(c));
            Assert.All(id, c => Assert.True(char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)));
        }
    }

    [Fact]
    public void Generate_ProducesDifferentIds()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => UserId.Generate()).ToHashSet();

        Assert.Equal(1000, ids.Count);
    }

    [Theory]
    [InlineData("K7M2-9QXD")]
    [InlineData("AAAA-2222")]
    public void IsValid_AcceptsCanonicalForm(string id)
    {
        Assert.True(UserId.IsValid(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K7M29QXD")]      // tire yok
    [InlineData("k7m2-9qxd")]     // küçük harf kanonik değil
    [InlineData("K7M2-9QX")]      // kısa
    [InlineData("K7M2-9QXDD")]    // uzun
    [InlineData("K7M2-9QX0")]     // yasak karakter 0
    [InlineData("K7M2-9QXO")]     // yasak karakter O
    [InlineData("K7M2_9QXD")]     // yanlış ayraç
    [InlineData("ali#4821")]
    public void IsValid_RejectsOtherInput(string? id)
    {
        Assert.False(UserId.IsValid(id));
    }

    [Theory]
    [InlineData("k7m2-9qxd", "K7M2-9QXD")]
    [InlineData("  K7M2-9QXD  ", "K7M2-9QXD")]
    [InlineData("k7m29qxd", "K7M2-9QXD")]
    [InlineData("k7m2 9qxd", "K7M2-9QXD")]
    [InlineData("K7M2-9QXD", "K7M2-9QXD")]
    public void TryNormalize_AcceptsCommonTypingVariants(string input, string expected)
    {
        Assert.True(UserId.TryNormalize(input, out string? normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ali")]
    [InlineData("K7M2-9QX")]
    [InlineData("K7M2-9QX0")]
    [InlineData("K7M2--9QXD")]
    public void TryNormalize_RejectsInvalidInput(string? input)
    {
        Assert.False(UserId.TryNormalize(input, out string? normalized));
        Assert.Null(normalized);
    }
}
