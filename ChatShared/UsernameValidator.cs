namespace ChatShared;

/// <summary>Kullanıcı adı kurallarını denetler: 3–20 karakter; harf (Türkçe dahil), rakam ve '_'.</summary>
public static class UsernameValidator
{
    /// <summary>İsim çakışması karşılaştırması: büyük/küçük harf ayrımı yoktur ("Ali" = "ali").</summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    public static bool IsValid(string? username) => Validate(username) == ErrorCode.None;

    /// <summary>Geçerliyse ErrorCode.None, değilse ErrorCode.UsernameInvalid döner.</summary>
    public static ErrorCode Validate(string? username)
    {
        if (string.IsNullOrEmpty(username)
            || username.Length < ProtocolConstants.UsernameMinLength
            || username.Length > ProtocolConstants.UsernameMaxLength)
        {
            return ErrorCode.UsernameInvalid;
        }

        return username.All(IsAllowedChar) ? ErrorCode.None : ErrorCode.UsernameInvalid;
    }

    private static bool IsAllowedChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
