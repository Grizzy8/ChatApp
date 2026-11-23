using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace ChatShared;

/// <summary>
/// Her hesaba sunucunun verdiği benzersiz kimlik, ör. "K7M2-9QXD". Kullanıcı adından bağımsızdır;
/// yanlış okunan karakterler (0, 1, I, L, O) kullanılmaz, böylece elle yazılıp paylaşılabilir.
/// </summary>
public static class UserId
{
    // 0/O ve 1/I/L karışmasın diye çıkarıldı: 31 karakter, 8 hane ≈ 8,5 trilyon olasılık.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int GroupLength = 4;
    private const int GroupCount = 2;
    private const char Separator = '-';
    private const int CompactLength = GroupLength * GroupCount;
    private const int CanonicalLength = CompactLength + GroupCount - 1;

    /// <summary>Kriptografik rastgelelikle yeni bir kimlik üretir (çakışma denetimi çağıranındır).</summary>
    public static string Generate()
    {
        string compact = new(RandomNumberGenerator.GetItems<char>(Alphabet, CompactLength));
        return compact.Insert(GroupLength, Separator.ToString());
    }

    /// <summary>Yalnızca kanonik biçimi ("XXXX-XXXX", büyük harf) geçerli sayar.</summary>
    public static bool IsValid([NotNullWhen(true)] string? id)
    {
        if (id is null || id.Length != CanonicalLength || id[GroupLength] != Separator)
        {
            return false;
        }

        for (int i = 0; i < id.Length; i++)
        {
            if (i != GroupLength && !Alphabet.Contains(id[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Kullanıcının yazdığı kimliği kanonik biçime getirir: büyük/küçük harf, boşluk ve eksik tire hoş görülür.
    /// Geçersizse false döner.
    /// </summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string text = new(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        text = text.ToUpperInvariant();

        if (text.Length == CompactLength)
        {
            text = text.Insert(GroupLength, Separator.ToString());
        }

        if (!IsValid(text))
        {
            return false;
        }

        normalized = text;
        return true;
    }
}
