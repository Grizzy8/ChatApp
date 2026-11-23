using ChatServer.Storage;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer;

// Kişi defteri işlemleri: ekleme, silme ve listeyi istemciye gönderme.
public sealed partial class ChatServerHost
{
    /// <summary>
    /// AddContact/RemoveContact isteğini işler. Başarıda güncel kişi listesinin tamamı gönderilir, böylece istemci
    /// kendi kopyasını yerelde tahmin etmek zorunda kalmaz. Hangi kişinin eklendiği log'a yazılmaz.
    /// </summary>
    private async Task HandleContactRequestAsync(ClientSession session, ChatMessage message, CancellationToken token)
    {
        try
        {
            ErrorCode problem = message.Type == MessageType.AddContact
                ? await TryAddContactAsync(session, message.Text, token).ConfigureAwait(false)
                : await TryRemoveContactAsync(session, message.Text, token).ConfigureAwait(false);

            if (problem != ErrorCode.None)
            {
                await RejectMessageAsync(session, problem, token).ConfigureAwait(false);
                return;
            }

            await SendContactListAsync(session, token).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            WriteLog($"{Describe(session)} için kişi işleminde veritabanı hatası: {ex.Message}", ServerLogLevel.Error);
            await SendErrorAsync(session, ErrorCode.ServerError, token).ConfigureAwait(false);
        }
    }

    /// <summary>Oturumun kişi listesini gönderir. Veritabanı hatası bağlantıyı düşürmez; kullanıcıya bildirilir.</summary>
    private async Task SendContactListAsync(ClientSession session, CancellationToken token)
    {
        try
        {
            IReadOnlyList<ContactRecord> contacts = await _contacts.ListAsync(session.UserId!, token).ConfigureAwait(false);
            var list = contacts.Select(c => new ContactInfo(c.UserId, c.Username)).ToList();

            await _router.SendToAsync(session, new ChatMessage(MessageType.ContactList, Contacts: list), token).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            WriteLog($"{Describe(session)} için kişi listesi okunamadı: {ex.Message}", ServerLogLevel.Error);
            await SendErrorAsync(session, ErrorCode.ServerError, token).ConfigureAwait(false);
        }
    }

    private async Task<ErrorCode> TryAddContactAsync(ClientSession session, string? query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > ProtocolConstants.MaxContactQueryLength)
        {
            return ErrorCode.InvalidMessage;
        }

        AddContactOutcome outcome = await _contacts.AddAsync(session.UserId!, query, token).ConfigureAwait(false);
        return outcome switch
        {
            AddContactOutcome.Added => ErrorCode.None,
            AddContactOutcome.AlreadyExists => ErrorCode.ContactAlreadyExists,
            AddContactOutcome.CannotAddSelf => ErrorCode.CannotAddSelf,
            AddContactOutcome.LimitReached => ErrorCode.ContactLimitReached,
            _ => ErrorCode.ContactNotFound
        };
    }

    private async Task<ErrorCode> TryRemoveContactAsync(ClientSession session, string? contactId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(contactId) || contactId.Length > ProtocolConstants.MaxContactQueryLength)
        {
            return ErrorCode.InvalidMessage;
        }

        return await _contacts.RemoveAsync(session.UserId!, contactId, token).ConfigureAwait(false)
            ? ErrorCode.None
            : ErrorCode.ContactNotFound;
    }
}
