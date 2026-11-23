using ChatShared;

namespace ChatShared.Test;

public class PasswordHasherTests
{
    // Testler hızlı çalışsın diye düşük yineleme sayısı kullanılır; gerçek değer DefaultIterations'tır.
    private const int FastIterations = 1000;

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        string stored = PasswordHasher.Hash("doğru-parola-1", FastIterations);

        Assert.True(PasswordHasher.Verify("doğru-parola-1", stored));
    }

    [Theory]
    [InlineData("yanlış-parola-1")]
    [InlineData("Doğru-parola-1")]
    [InlineData("doğru-parola-1 ")]
    [InlineData("")]
    public void Verify_ReturnsFalse_ForWrongPassword(string attempt)
    {
        string stored = PasswordHasher.Hash("doğru-parola-1", FastIterations);

        Assert.False(PasswordHasher.Verify(attempt, stored));
    }

    [Fact]
    public void Hash_ProducesDifferentOutput_ForSamePassword()
    {
        string first = PasswordHasher.Hash("aynı-parola-1", FastIterations);
        string second = PasswordHasher.Hash("aynı-parola-1", FastIterations);

        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("aynı-parola-1", first));
        Assert.True(PasswordHasher.Verify("aynı-parola-1", second));
    }

    [Fact]
    public void Hash_IsSelfDescribing_AndDoesNotContainThePassword()
    {
        string stored = PasswordHasher.Hash("gizli-parola-1", FastIterations);

        Assert.StartsWith($"pbkdf2-sha256${FastIterations}$", stored);
        Assert.DoesNotContain("gizli-parola-1", stored);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("düz-metin")]
    [InlineData("md5$1000$AAAA$AAAA")]                      // bilinmeyen şema
    [InlineData("pbkdf2-sha256$abc$AAAA$AAAA")]             // yineleme sayı değil
    [InlineData("pbkdf2-sha256$0$AAAA$AAAA")]               // sıfır yineleme
    [InlineData("pbkdf2-sha256$99999999$AAAA$AAAA")]        // aşırı yineleme (servis dışı bırakma girişimi)
    [InlineData("pbkdf2-sha256$1000$***$AAAA")]             // bozuk base64 salt
    [InlineData("pbkdf2-sha256$1000$AAAA$***")]             // bozuk base64 hash
    [InlineData("pbkdf2-sha256$1000$AAAA")]                 // eksik parça
    [InlineData("pbkdf2-sha256$1000$AAAA$AAAA$ekstra")]     // fazla parça
    public void Verify_ReturnsFalse_ForMalformedStoredValue(string? stored)
    {
        Assert.False(PasswordHasher.Verify("herhangi-parola", stored));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenStoredHashIsTampered()
    {
        string stored = PasswordHasher.Hash("doğru-parola-1", FastIterations);
        string[] parts = stored.Split('$');
        parts[3] = Convert.ToBase64String(new byte[32]);

        Assert.False(PasswordHasher.Verify("doğru-parola-1", string.Join('$', parts)));
    }

    [Fact]
    public void NeedsRehash_IsTrue_WhenIterationsAreLowerThanCurrent()
    {
        string stored = PasswordHasher.Hash("parola-12345", FastIterations);

        Assert.True(PasswordHasher.NeedsRehash(stored, FastIterations + 1));
        Assert.False(PasswordHasher.NeedsRehash(stored, FastIterations));
        Assert.False(PasswordHasher.NeedsRehash(stored, FastIterations - 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bozuk")]
    public void NeedsRehash_IsTrue_ForUnreadableValue(string? stored)
    {
        Assert.True(PasswordHasher.NeedsRehash(stored));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Hash_Throws_ForNonPositiveIterations(int iterations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordHasher.Hash("parola-12345", iterations));
    }

    [Fact]
    public void Hash_Throws_ForEmptyPassword()
    {
        Assert.Throws<ArgumentException>(() => PasswordHasher.Hash("", FastIterations));
    }
}
