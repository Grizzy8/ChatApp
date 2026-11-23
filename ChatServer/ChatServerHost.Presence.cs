using ChatServer.Storage;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer;

// Çevrimiçi durum yalnızca ilgili kişilere gösterilir (kişi ekleyenler ve ortak grup üyeleri); sunucu herkese yayın yapmaz.
// Kullanıcı arama yalnızca tam eşleşme verir; kullanıcı listesi gezilemez.
public sealed partial class ChatServerHost
{
    /// <summary>Kullanıcının durumunu görebildiği hesaplardan şu an çevrimiçi olanların adları. Veritabanı hatasında boş liste döner.</summary>
    private async Task<IReadOnlyList<string>> GetOnlineVisibleToAsync(string userId, CancellationToken token)
    {
        try
        {
            IReadOnlyList<string> visible = await _presence.ListVisibleToAsync(userId, token).ConfigureAwait(false);
            return visible.Where(name => _registry.TryGetActive(name, out _)).ToList();
        }
        catch (SqliteException ex)
        {
            WriteLog($"Çevrimiçi durum listesi okunamadı: {ex.Message}", ServerLogLevel.Error);
            return Array.Empty<string>();
        }
    }

    /// <summary>Bu kullanıcının durumunu görebilen çevrimiçi hesaplara mesajı iletir (UserJoined/UserLeft).</summary>
    private async Task NotifyWatchersAsync(string userId, ChatMessage message, CancellationToken token)
    {
        IReadOnlyList<string> watchers;
        try
        {
            watchers = await _presence.ListWatchersAsync(userId, token).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            WriteLog($"Çevrimiçi durum bildirimi için izleyiciler okunamadı: {ex.Message}", ServerLogLevel.Error);
            return;
        }

        foreach (string name in watchers)
        {
            if (_registry.TryGetActive(name, out ClientSession? target))
            {
                await _router.SendToAsync(target, message, token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Oturuma, durumunu görebildiği çevrimiçi kullanıcıların güncel listesini gönderir (kişi/grup değişikliklerinden sonra).</summary>
    private async Task SendPresenceListAsync(ClientSession session, CancellationToken token)
    {
        IReadOnlyList<string> online = await GetOnlineVisibleToAsync(session.UserId!, token).ConfigureAwait(false);
        await _router.SendToAsync(session, new ChatMessage(MessageType.PresenceList, Users: online), token).ConfigureAwait(false);
    }

    /// <summary>Tam kullanıcı adı ya da ID ile arama. Bulunamazsa ya da kendisiyse boş liste döner; böylece "var/yok" dışında bilgi sızmaz.</summary>
    private async Task HandleFindUserAsync(ClientSession session, ChatMessage message, CancellationToken token)
    {
        string? query = message.Text;
        if (string.IsNullOrWhiteSpace(query) || query.Length > ProtocolConstants.MaxContactQueryLength)
        {
            await RejectMessageAsync(session, ErrorCode.InvalidMessage, token).ConfigureAwait(false);
            return;
        }

        try
        {
            UserRecord? found = await _users.FindByUsernameOrIdAsync(query, token).ConfigureAwait(false);
            var result = found is null || found.Id == session.UserId
                ? new List<ContactInfo>()
                : new List<ContactInfo> { new(found.Id, found.Username) };

            await _router.SendToAsync(session, new ChatMessage(MessageType.UserFound, Text: query, Contacts: result), token).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            WriteLog($"{Describe(session)} için kullanıcı araması başarısız: {ex.Message}", ServerLogLevel.Error);
            await SendErrorAsync(session, ErrorCode.ServerError, token).ConfigureAwait(false);
        }
    }
}
