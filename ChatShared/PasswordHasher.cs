using System.Security.Cryptography;

namespace ChatShared;

/// <summary>
/// Parolaları PBKDF2-SHA256 ile hash'ler. Sonuç kendini tanımlayan tek bir metindir:
/// "pbkdf2-sha256$yineleme$tuz$hash" (tuz ve hash base64). Yineleme sayısı metinde saklandığı için
/// ileride artırılabilir; eski hesaplar <see cref="NeedsRehash"/> ile fark edilip girişte yenilenir.
/// </summary>
public static class PasswordHasher
{
    /// <summary>OWASP'ın PBKDF2-HMAC-SHA256 için önerdiği yineleme sayısı.</summary>
    public const int DefaultIterations = 600_000;

    private const string Scheme = "pbkdf2-sha256";
    private const char Separator = '$';
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // Bozulmuş ya da kurcalanmış bir kayıt sunucuyu uzun süre meşgul etmesin diye kabul edilen üst sınırlar.
    private const int MaxAcceptedIterations = 10_000_000;
    private const int MaxAcceptedHashSize = 1024;

    /// <summary>Parolayı rastgele bir tuzla hash'ler. Her çağrı farklı bir sonuç üretir.</summary>
    public static string Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

        return string.Join(Separator, Scheme, iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>Parolanın saklanan hash ile eşleşip eşleşmediğini sabit sürede denetler. Bozuk kayıt için false döner.</summary>
    public static bool Verify(string password, string? stored)
    {
        if (!TryParse(stored, out int iterations, out byte[] salt, out byte[] expectedHash))
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password ?? string.Empty, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        // Karşılaştırma süresi eşleşen bayt sayısına bağlı olmasın (zamanlama saldırılarına karşı).
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>Kayıt okunamıyorsa ya da güncel yineleme sayısından azıyla üretilmişse true döner.</summary>
    public static bool NeedsRehash(string? stored, int iterations = DefaultIterations) =>
        !TryParse(stored, out int storedIterations, out _, out _) || storedIterations < iterations;

    private static bool TryParse(string? stored, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        string[] parts = stored.Split(Separator);
        if (parts.Length != 4
            || parts[0] != Scheme
            || !int.TryParse(parts[1], out iterations)
            || iterations < 1
            || iterations > MaxAcceptedIterations)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0 && hash.Length <= MaxAcceptedHashSize;
    }
}
