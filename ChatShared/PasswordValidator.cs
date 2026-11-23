namespace ChatShared;

/// <summary>Parola kurallarını denetler: 8–64 karakter, yalnızca boşluklardan oluşamaz. İstemci ve sunucu aynı kuralı kullanır.</summary>
public static class PasswordValidator
{
    public static bool IsValid(string? password) => Validate(password) == ErrorCode.None;

    /// <summary>Geçerliyse ErrorCode.None, değilse ErrorCode.PasswordInvalid döner.</summary>
    public static ErrorCode Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)
            || password.Length < ProtocolConstants.PasswordMinLength
            || password.Length > ProtocolConstants.PasswordMaxLength)
        {
            return ErrorCode.PasswordInvalid;
        }

        return ErrorCode.None;
    }
}
