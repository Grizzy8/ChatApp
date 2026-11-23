using ChatServer.Storage;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer;

// Özel mesajlar: alıcı çevrimiçiyse doğrudan iletilir (veritabanına yazılmaz),
// çevrimdışıysa teslim bekleyen mesaj olarak kuyruğa alınır ve alıcı girince teslim edilip silinir.
public sealed partial class ChatServerHost
{
    /// <summary>
    /// Özel mesajı yalnızca alıcıya ve göndericiye iletir. Alıcı kullanıcı adı ya da ID ile verilebilir; hesap varsa
    /// çevrimdışı olsa da mesaj kabul edilir. İçerik, gizlilik için sunucu log'una yazılmaz.
    /// </summary>
    private async Task HandlePrivateAsync(ClientSession session, ChatMessage message, CancellationToken token)
    {
        ErrorCode problem = ValidateMessageText(message.Text);
        if (problem == ErrorCode.None
            && (string.IsNullOrWhiteSpace(message.Recipient) || message.Recipient.Length > ProtocolConstants.MaxContactQueryLength))
        {
            problem = ErrorCode.InvalidMessage;
        }

        if (problem != ErrorCode.None)
        {
            await RejectMessageAsync(session, problem, token).ConfigureAwait(false);
            return;
        }

        try
        {
            await RoutePrivateAsync(session, message.Recipient!, message.Text!, token).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            WriteLog($"{session.Username} için özel mesaj işlenirken veritabanı hatası: {ex.Message}", ServerLogLevel.Error);
            await SendErrorAsync(session, ErrorCode.ServerError, token).ConfigureAwait(false);
        }
    }

    private async Task RoutePrivateAsync(ClientSession session, string recipient, string text, CancellationToken token)
    {
        UserRecord? target = await _users.FindByUsernameOrIdAsync(recipient, token).ConfigureAwait(false);
        if (target is null)
        {
            await RejectMessageAsync(session, ErrorCode.RecipientNotFound, token).ConfigureAwait(false);
            return;
        }

        if (target.Id == session.UserId)
        {
            await RejectMessageAsync(session, ErrorCode.InvalidMessage, token).ConfigureAwait(false); // kendine özel mesaj
            return;
        }

        var delivery = new ChatMessage(
            MessageType.Private, Sender: session.Username, Recipient: target.Username, Text: text, Timestamp: DateTime.Now);

        if (_registry.TryGetActive(target.Username, out ClientSession? targetSession)
            && await _router.SendToAsync(targetSession, delivery, token).ConfigureAwait(false))
        {
            WriteLog($"{session.Username} → {target.Username}: [özel mesaj]");

            // Gönderen de aynı kopyayı alır: mesajın iletildiğini ve sunucu saatini görür.
            await _router.SendToAsync(session, delivery, token).ConfigureAwait(false);
            return;
        }

        // Alıcı çevrimdışı (ya da tam bu sırada düştü): mesaj bir sonraki girişinde teslim edilmek üzere bekletilir.
        if (!await _messages.EnqueueAsync(session.UserId!, target.Id, text, DateTime.UtcNow, token).ConfigureAwait(false))
        {
            await RejectMessageAsync(session, ErrorCode.RecipientInboxFull, token).ConfigureAwait(false);
            return;
        }

        WriteLog($"{session.Username} → {target.Username}: [özel mesaj, çevrimdışı alıcı için bekletiliyor]");
        await _router.SendToAsync(session, delivery with { Type = MessageType.PrivateQueued }, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Girişte, çevrimdışıyken gelen mesajları sırayla teslim eder ve teslim edilenleri sunucudan siler.
    /// Hata bağlantıyı düşürmez; teslim edilemeyen mesajlar bir sonraki girişe kalır.
    /// </summary>
    private async Task DeliverPendingMessagesAsync(ClientSession session, CancellationToken token)
    {
        try
        {
            IReadOnlyList<PendingMessage> pending = await _messages.ListPendingAsync(session.UserId!, token).ConfigureAwait(false);
            long lastDeliveredId = 0;
            int deliveredCount = 0;

            foreach (PendingMessage item in pending)
            {
                // Grup mesajında Recipient grup kimliğidir (istemci adı kendi grup listesinden bulur); özel mesajda kullanıcının adıdır.
                var message = new ChatMessage(
                    item.GroupId is null ? MessageType.Private : MessageType.GroupMessage,
                    Sender: item.SenderUsername,
                    Recipient: item.GroupId ?? session.Username,
                    Text: item.Text,
                    Timestamp: item.SentAtUtc.ToLocalTime());

                if (!await _router.SendToAsync(session, message, token).ConfigureAwait(false))
                {
                    break; // bağlantı koptu; kalanlar silinmez
                }

                lastDeliveredId = item.Id;
                deliveredCount++;
            }

            if (deliveredCount > 0)
            {
                await _messages.DeleteUpToAsync(session.UserId!, lastDeliveredId, token).ConfigureAwait(false);
                WriteLog($"{session.Username} için {deliveredCount} bekleyen mesaj teslim edildi.");
            }
        }
        catch (SqliteException ex)
        {
            WriteLog($"{session.Username} için bekleyen mesajlar teslim edilemedi: {ex.Message}", ServerLogLevel.Error);
        }
    }
}
