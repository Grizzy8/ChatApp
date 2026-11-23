namespace ChatShared;

/// <summary>Grup adı kuralları: kırpılmış hâliyle 1-30 karakter, kontrol karakteri içermez.</summary>
public static class GroupNameValidator
{
    /// <summary>Adı doğrular; geçerliyse kırpılmış adı <paramref name="normalized"/> olarak verir.</summary>
    public static bool TryNormalize(string? name, out string normalized)
    {
        normalized = name?.Trim() ?? string.Empty;

        return normalized.Length is >= 1 and <= ProtocolConstants.GroupNameMaxLength
            && !normalized.Any(char.IsControl);
    }
}
