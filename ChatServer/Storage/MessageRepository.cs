using System.Globalization;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer.Storage;

/// <summary>Çevrimdışı alıcıya bırakılan, henüz teslim edilmemiş özel mesaj.</summary>
public sealed record PendingMessage(long Id, string SenderUsername, string Text, DateTime SentAtUtc);

/// <summary>
/// Teslim bekleyen özel mesajların kuyruğu. Teslim edilen mesaj silinir; sunucu sohbet geçmişi tutmaz.
/// Tüm sorgular parametrelidir.
/// </summary>
public sealed class MessageRepository
{
    private readonly ChatDatabase _database;

    public MessageRepository(ChatDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    /// <summary>
    /// Mesajı alıcının kuyruğuna ekler. Alıcının kutusu doluysa false döner.
    /// Sayım ve ekleme tek işlemdedir; eşzamanlı gönderimlerle üst sınır aşılamaz.
    /// </summary>
    public async Task<bool> EnqueueAsync(
        string senderId, string recipientId, string text, DateTime sentAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(senderId);
        ArgumentException.ThrowIfNullOrEmpty(recipientId);
        ArgumentException.ThrowIfNullOrEmpty(text);

        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false); // BEGIN IMMEDIATE

        await using (SqliteCommand count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM PendingMessages WHERE RecipientId = $recipient;";
            count.Parameters.AddWithValue("$recipient", recipientId);
            long pending = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (pending >= ProtocolConstants.MaxPendingMessagesPerRecipient)
            {
                return false;
            }
        }

        await using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT INTO PendingMessages (RecipientId, SenderId, Payload, SentAt) VALUES ($recipient, $sender, $payload, $sentAt);";
            insert.Parameters.AddWithValue("$recipient", recipientId);
            insert.Parameters.AddWithValue("$sender", senderId);
            insert.Parameters.AddWithValue("$payload", text);
            insert.Parameters.AddWithValue("$sentAt", FormatTime(sentAtUtc));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Alıcının bekleyen mesajlarını gönderim sırasıyla döner. Önce süresi dolanlar silinir.</summary>
    public async Task<IReadOnlyList<PendingMessage>> ListPendingAsync(string recipientId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipientId);

        await PurgeExpiredAsync(recipientId, cancellationToken).ConfigureAwait(false);

        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT m.Id, u.Username, m.Payload, m.SentAt FROM PendingMessages m JOIN Users u ON u.Id = m.SenderId " +
            "WHERE m.RecipientId = $recipient ORDER BY m.Id;";
        command.Parameters.AddWithValue("$recipient", recipientId);

        var messages = new List<PendingMessage>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(new PendingMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime()));
        }

        return messages;
    }

    /// <summary>Teslim edilen mesajları siler: kimliği <paramref name="lastDeliveredId"/> ya da daha küçük olan tüm bekleyenler.</summary>
    public async Task<int> DeleteUpToAsync(string recipientId, long lastDeliveredId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipientId);

        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM PendingMessages WHERE RecipientId = $recipient AND Id <= $lastId;";
        command.Parameters.AddWithValue("$recipient", recipientId);
        command.Parameters.AddWithValue("$lastId", lastDeliveredId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Saklama süresini aşan mesajları siler. recipientId verilirse yalnızca o alıcınınkiler; verilmezse hepsi.</summary>
    public async Task<int> PurgeExpiredAsync(string? recipientId = null, CancellationToken cancellationToken = default)
    {
        string cutoff = FormatTime(DateTime.UtcNow - ProtocolConstants.PendingMessageRetention);

        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = recipientId is null
            ? "DELETE FROM PendingMessages WHERE SentAt < $cutoff;"
            : "DELETE FROM PendingMessages WHERE RecipientId = $recipient AND SentAt < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", cutoff);
        if (recipientId is not null)
        {
            command.Parameters.AddWithValue("$recipient", recipientId);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // Aynı biçim (UTC, "O") kullanıldığı için metin olarak karşılaştırma zaman sırasıyla aynıdır.
    private static string FormatTime(DateTime utc) => utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
