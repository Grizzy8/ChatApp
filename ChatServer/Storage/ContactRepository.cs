using System.Globalization;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer.Storage;

/// <summary>Kişi defterinin kalıcı kaydı. Tüm sorgular parametrelidir.</summary>
public sealed class ContactRepository
{
    private const int PrimaryKeyViolation = 1555;

    private readonly ChatDatabase _database;
    private readonly UserRepository _users;

    public ContactRepository(ChatDatabase database, UserRepository users)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(users);
        _database = database;
        _users = users;
    }

    /// <summary>
    /// Kullanıcı adı ya da ID ile bulunan hesabı sahibin kişi defterine ekler.
    /// Sayım ve ekleme tek işlemdedir; eşzamanlı isteklerle üst sınır aşılamaz.
    /// </summary>
    public async Task<AddContactOutcome> AddAsync(string ownerId, string? query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        UserRecord? target = await _users.FindByUsernameOrIdAsync(query, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return AddContactOutcome.NotFound;
        }

        if (target.Id == ownerId)
        {
            return AddContactOutcome.CannotAddSelf;
        }

        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        // deferred: false = BEGIN IMMEDIATE: yazma kilidi en başta alınır; sayım ile ekleme arasına başka yazıcı giremez
        // ve iki işlemin okuma kilidinden yazmaya yükselirken kilitlenmesi (SQLITE_BUSY) yaşanmaz.
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);

        if (await CountAsync(connection, transaction, ownerId, cancellationToken).ConfigureAwait(false) >= ProtocolConstants.MaxContacts)
        {
            return AddContactOutcome.LimitReached;
        }

        try
        {
            await using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Contacts (OwnerId, ContactId, AddedAt) VALUES ($owner, $contact, $addedAt);";
            insert.Parameters.AddWithValue("$owner", ownerId);
            insert.Parameters.AddWithValue("$contact", target.Id);
            insert.Parameters.AddWithValue("$addedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == PrimaryKeyViolation)
        {
            return AddContactOutcome.AlreadyExists;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return AddContactOutcome.Added;
    }

    /// <summary>Sahibin kişilerini kullanıcı adına göre sıralı döner.</summary>
    public async Task<IReadOnlyList<ContactRecord>> ListAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT u.Id, u.Username, c.AddedAt FROM Contacts c JOIN Users u ON u.Id = c.ContactId " +
            "WHERE c.OwnerId = $owner ORDER BY u.UsernameKey;";
        command.Parameters.AddWithValue("$owner", ownerId);

        var contacts = new List<ContactRecord>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            contacts.Add(new ContactRecord(
                reader.GetString(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime()));
        }

        return contacts;
    }

    /// <summary>Kişiyi siler. Kişi listede değilse ya da kimlik geçersizse false döner. "k7m29qxd" gibi yazımlar kabul edilir.</summary>
    public async Task<bool> RemoveAsync(string ownerId, string? contactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        if (!UserId.TryNormalize(contactId, out string? normalized))
        {
            return false;
        }

        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Contacts WHERE OwnerId = $owner AND ContactId = $contact;";
        command.Parameters.AddWithValue("$owner", ownerId);
        command.Parameters.AddWithValue("$contact", normalized);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    private static async Task<long> CountAsync(
        SqliteConnection connection, SqliteTransaction transaction, string ownerId, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM Contacts WHERE OwnerId = $owner;";
        command.Parameters.AddWithValue("$owner", ownerId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }
}
