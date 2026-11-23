using System.Net.Sockets;
using ChatShared;

namespace ChatClient;

/// <summary>Sunucudan gelen hata kodlarını ve ağ hatalarını kullanıcıya gösterilecek Türkçe metne çevirir.</summary>
public static class ErrorMessages
{
    public static string Describe(ErrorCode? code) => code switch
    {
        ErrorCode.UsernameTaken => "Bu kullanıcı adı zaten kullanılıyor. Lütfen başka bir ad seçin.",
        ErrorCode.UsernameInvalid =>
            $"Kullanıcı adı {ProtocolConstants.UsernameMinLength}-{ProtocolConstants.UsernameMaxLength} karakter olmalı; " +
            "yalnızca harf, rakam ve _ içerebilir.",
        ErrorCode.RecipientNotFound => "Alıcı şu anda çevrimiçi değil.",
        ErrorCode.MessageTooLong => $"Mesaj en fazla {ProtocolConstants.MaxMessageLength} karakter olabilir.",
        ErrorCode.InvalidMessage => "Sunucu gönderilen mesajı anlayamadı.",
        ErrorCode.NotJoined => "Sohbete katılmadan işlem yapılamaz.",
        ErrorCode.ServerShuttingDown => "Sunucu kapatılıyor.",
        _ => "Bilinmeyen bir hata oluştu."
    };

    public static string Describe(SocketException exception, string host, int port) => exception.SocketErrorCode switch
    {
        SocketError.ConnectionRefused =>
            $"{host}:{port} adresine bağlanılamadı. Sunucunun çalıştığından ve port numarasının doğru olduğundan emin olun.",
        SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData =>
            $"'{host}' adresi bulunamadı. Sunucu adresini kontrol edin.",
        SocketError.NetworkUnreachable or SocketError.HostUnreachable =>
            "Sunucuya ulaşılamıyor. Ağ bağlantınızı ve IP adresini kontrol edin.",
        SocketError.TimedOut => TimeoutMessage,
        _ => $"Ağ hatası: {exception.Message}"
    };

    public const string TimeoutMessage = "Sunucu zamanında yanıt vermedi. Adresi ve ağ bağlantınızı kontrol edin.";
}
