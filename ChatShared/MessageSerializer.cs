using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatShared;

/// <summary>
/// ChatMessage nesnelerini tek satırlık JSON metnine çevirir ve geri çözer.
/// Bozuk girdide exception fırlatmaz, null döner.
/// </summary>
public static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Türkçe karakterler \uXXXX olarak değil, olduğu gibi yazılır.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    /// <summary>Mesajı tek satırlık JSON'a çevirir (satır sonu karakteri içermez).</summary>
    public static string Serialize(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.Serialize(message, Options);
    }

    /// <summary>Bir satırlık JSON'u mesaja çevirir; geçersizse null döner.</summary>
    public static ChatMessage? Deserialize(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > ProtocolConstants.MaxLineLength)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ChatMessage>(line, Options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
