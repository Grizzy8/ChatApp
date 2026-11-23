using Microsoft.Data.Sqlite;

namespace ChatServer.Storage;

/// <summary>
/// Çevrimiçi durumun kimlere gösterileceğini belirler: kişiler ve ortak grup üyeleri.
/// Böylece sunucu bir kullanıcının çevrimiçi olduğunu tanımadığı kimselere duyurmaz.
/// </summary>
public sealed class PresenceRepository
{
    // İlişki: A'nın kişi defterinde B varsa A, B'nin durumunu görür; aynı grubun üyeleri birbirinin durumunu görür.
    private const string VisibleToMeSql =
        "SELECT u.Username FROM Users u WHERE u.Id IN (" +
        "SELECT ContactId FROM Contacts WHERE OwnerId = $u " +
        "UNION SELECT m2.UserId FROM GroupMembers m1 JOIN GroupMembers m2 ON m1.GroupId = m2.GroupId " +
        "WHERE m1.UserId = $u AND m2.UserId <> $u);";

    private const string WatchersOfMeSql =
        "SELECT u.Username FROM Users u WHERE u.Id IN (" +
        "SELECT OwnerId FROM Contacts WHERE ContactId = $u " +
        "UNION SELECT m2.UserId FROM GroupMembers m1 JOIN GroupMembers m2 ON m1.GroupId = m2.GroupId " +
        "WHERE m1.UserId = $u AND m2.UserId <> $u);";

    private readonly ChatDatabase _database;

    public PresenceRepository(ChatDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    /// <summary>Kullanıcının çevrimiçi durumunu görebildiği hesapların kullanıcı adları (kişileri + ortak grup üyeleri).</summary>
    public Task<IReadOnlyList<string>> ListVisibleToAsync(string userId, CancellationToken ct = default) =>
        QueryAsync(VisibleToMeSql, userId, ct);

    /// <summary>Kullanıcının çevrimiçi durumunu görebilen hesapların kullanıcı adları (onu kişi ekleyenler + ortak grup üyeleri).</summary>
    public Task<IReadOnlyList<string>> ListWatchersAsync(string userId, CancellationToken ct = default) =>
        QueryAsync(WatchersOfMeSql, userId, ct);

    private async Task<IReadOnlyList<string>> QueryAsync(string sql, string userId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using SqliteConnection connection = await _database.OpenAsync(ct).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$u", userId);

        var names = new List<string>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
