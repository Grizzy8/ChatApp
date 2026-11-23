using System.Globalization;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer.Storage;

/// <summary>Hesapların kalıcı kaydı. Tüm sorgular parametrelidir; kullanıcı girdisi SQL'e eklenmez.</summary>
public sealed class UserRepository
{
    // SQLite genişletilmiş hata kodları (sqlite3.h): birincil anahtar ve UNIQUE kısıtı ihlalleri.
    private const int PrimaryKeyViolation = 1555;
    private const int UniqueViolation = 2067;

    // Rastgele kimlik çakışması pratikte imkânsızdır (~8,5 trilyon olasılık); yine de sonsuz döngüye girmemek için sınır.
    private const int MaxIdAttempts = 5;

    private const string SelectColumns = "Id, Username, PasswordHash, CreatedAt";

    private readonly ChatDatabase _database;

    public UserRepository(ChatDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    /// <summary>
    /// Yeni hesap oluşturur ve benzersiz kimlik atar. Kullanıcı adı (büyük/küçük harf duyarsız) zaten varsa null döner.
    /// Geçersiz kullanıcı adı çağıranın hatasıdır (sunucu önce doğrulamalıdır): ArgumentException fırlatır.
    /// </summary>
    public async Task<UserRecord?> CreateAsync(string username, string passwordHash, CancellationToken cancellationToken = default)
    {
        if (UsernameValidator.Validate(username) != ErrorCode.None)
        {
            throw new ArgumentException("Geçersiz kullanıcı adı.", nameof(username));
        }

        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        for (int attempt = 0; attempt < MaxIdAttempts; attempt++)
        {
            var user = new UserRecord(UserId.Generate(), username, passwordHash, DateTime.UtcNow);

            try
            {
                await InsertAsync(connection, user, cancellationToken).ConfigureAwait(false);
                return user;
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == UniqueViolation)
            {
                return null; // kullanıcı adı alınmış
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == PrimaryKeyViolation)
            {
                // Kimlik çakışması: yeni bir kimlikle yeniden dene.
            }
        }

        throw new InvalidOperationException("Benzersiz kullanıcı kimliği üretilemedi.");
    }

    /// <summary>Kullanıcı adına göre hesabı bulur (büyük/küçük harf duyarsız); yoksa null.</summary>
    public Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        FindAsync("UsernameKey = $key", "$key", ToKey(username), cancellationToken);

    /// <summary>Kimliğe göre hesabı bulur; yoksa ya da biçim geçersizse null. "k7m29qxd" gibi yazımlar hoş görülür.</summary>
    public Task<UserRecord?> FindByIdAsync(string id, CancellationToken cancellationToken = default) =>
        UserId.TryNormalize(id, out string? normalized)
            ? FindAsync("Id = $id", "$id", normalized, cancellationToken)
            : Task.FromResult<UserRecord?>(null);

    /// <summary>
    /// Kişi eklerken yazılan metinden hesabı bulur. İçinde '-' olan metin kimlik (ID) sayılır (kullanıcı adında tire olamaz);
    /// diğerleri önce kullanıcı adı, bulunamazsa tiresiz yazılmış kimlik olarak denenir.
    /// </summary>
    public async Task<UserRecord?> FindByUsernameOrIdAsync(string? query, CancellationToken cancellationToken = default)
    {
        string text = query?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > ProtocolConstants.MaxContactQueryLength)
        {
            return null;
        }

        if (text.Contains('-'))
        {
            return await FindByIdAsync(text, cancellationToken).ConfigureAwait(false);
        }

        UserRecord? byName = UsernameValidator.IsValid(text)
            ? await FindByUsernameAsync(text, cancellationToken).ConfigureAwait(false)
            : null;

        return byName ?? await FindByIdAsync(text, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserRecord?> FindAsync(string where, string parameterName, string value, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Users WHERE {where} LIMIT 1;";
        command.Parameters.AddWithValue(parameterName, value);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Map(reader) : null;
    }

    private static async Task InsertAsync(SqliteConnection connection, UserRecord user, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Users (Id, Username, UsernameKey, PasswordHash, CreatedAt) " +
            "VALUES ($id, $username, $key, $hash, $createdAt);";
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$key", ToKey(user.Username));
        command.Parameters.AddWithValue("$hash", user.PasswordHash);
        command.Parameters.AddWithValue("$createdAt", user.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // UsernameValidator.Comparer (OrdinalIgnoreCase) ile aynı eşitlik kuralı.
    private static string ToKey(string username) => username.ToUpperInvariant();

    private static UserRecord Map(SqliteDataReader reader) => new(
        Id: reader.GetString(0),
        Username: reader.GetString(1),
        PasswordHash: reader.GetString(2),
        CreatedAtUtc: DateTime.Parse(
            reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime());
}
