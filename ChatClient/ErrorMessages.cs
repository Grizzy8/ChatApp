using System.Net.Sockets;
using ChatShared;

namespace ChatClient;

/// <summary>Sunucudan gelen hata kodlarını ve ağ hatalarını kullanıcıya gösterilecek Türkçe metne çevirir.</summary>
public static class ErrorMessages
{
    public static string Describe(ErrorCode? code) => code switch
    {
        ErrorCode.UsernameTaken => "Bu kullanıcı adı zaten kayıtlı. Lütfen başka bir ad seçin ya da giriş yapın.",
        ErrorCode.UsernameInvalid =>
            $"Kullanıcı adı {ProtocolConstants.UsernameMinLength}-{ProtocolConstants.UsernameMaxLength} karakter olmalı; " +
            "yalnızca harf, rakam ve _ içerebilir.",
        ErrorCode.RecipientNotFound => "Alıcı şu anda çevrimiçi değil.",
        ErrorCode.MessageTooLong => $"Mesaj en fazla {ProtocolConstants.MaxMessageLength} karakter olabilir.",
        ErrorCode.InvalidMessage => "Sunucu gönderilen mesajı anlayamadı.",
        ErrorCode.NotJoined => "Sohbete katılmadan işlem yapılamaz.",
        ErrorCode.ServerShuttingDown => "Sunucu kapatılıyor.",
        ErrorCode.PasswordInvalid =>
            $"Parola {ProtocolConstants.PasswordMinLength}-{ProtocolConstants.PasswordMaxLength} karakter olmalı " +
            "ve yalnızca boşluklardan oluşamaz.",
        ErrorCode.InvalidCredentials => "Kullanıcı adı veya parola hatalı.",
        ErrorCode.AccountAlreadyOnline => "Bu hesap şu anda başka bir yerde açık.",
        ErrorCode.TooManyAttempts => "Çok fazla deneme yaptınız. Lütfen bir dakika bekleyip tekrar deneyin.",
        ErrorCode.ServerError => "Sunucuda beklenmeyen bir sorun oluştu. Lütfen daha sonra tekrar deneyin.",
        ErrorCode.ContactNotFound => "Bu kullanıcı adı ya da ID ile bir hesap bulunamadı.",
        ErrorCode.ContactAlreadyExists => "Bu kişi zaten kişilerinizde.",
        ErrorCode.CannotAddSelf => "Kendinizi kişi olarak ekleyemezsiniz.",
        ErrorCode.ContactLimitReached => $"En fazla {ProtocolConstants.MaxContacts} kişi ekleyebilirsiniz.",
        ErrorCode.RateLimited =>
            $"Çok hızlı mesaj gönderiyorsunuz. {ProtocolConstants.RateLimitWindow.TotalSeconds:0} saniyede en fazla " +
            $"{ProtocolConstants.MaxMessagesPerWindow} mesaj gönderebilirsiniz.",
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
