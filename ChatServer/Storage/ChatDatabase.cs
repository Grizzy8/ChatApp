using Microsoft.Data.Sqlite;

namespace ChatServer.Storage;

/// <summary>
/// SQLite veritabanına bağlantı açar ve şemayı güncel sürüme getirir.
/// Her işlem kendi kısa ömürlü bağlantısını kullanır (bağlantı havuzu sayesinde ucuzdur), böylece
/// birden çok istemcinin eşzamanlı işlemleri paylaşılan bir bağlantı üzerinde birbirine karışmaz.
/// </summary>
public sealed class ChatDatabase
{
    private readonly string _connectionString;

    /// <param name="filePath">Veritabanı dosyası; yoksa oluşturulur. Klasörü yoksa oluşturulur.</param>
    public ChatDatabase(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        FilePath = Path.GetFullPath(filePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    /// <summary>Varsayılan konum: %LocalAppData%\ChatApp\server\chat.db.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatApp", "server", "chat.db");

    public string FilePath { get; }

    /// <summary>Bu uygulamanın beklediği şema sürümü.</summary>
    public static int LatestSchemaVersion => SchemaMigrations.LatestVersion;

    /// <summary>Yeni bir bağlantı açar. Çağıran kapatmakla (using) yükümlüdür.</summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Eksik şema sürümlerini sırayla uygular; her sürüm tek bir işlemdir, yarıda kalırsa geri alınır.
    /// Veritabanı bu uygulamadan daha yeni bir sürümdeyse veriyi bozmamak için InvalidOperationException fırlatır.
    /// Uygulama başlarken bir kez çağrılmalıdır.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // WAL, okuyucuların yazıcıyı engellemesini önler; ayar dosyada kalıcıdır.
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);

        int current = await GetSchemaVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        if (current > LatestSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Veritabanı şema sürümü ({current}) bu uygulamanınkinden ({LatestSchemaVersion}) yeni. " +
                "Uygulamayı güncelleyin; veritabanı değiştirilmedi.");
        }

        for (int version = current + 1; version <= LatestSchemaVersion; version++)
        {
            await ApplyMigrationAsync(connection, version, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Veritabanındaki geçerli şema sürümü (boş veritabanı için 0).</summary>
    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return await GetSchemaVersionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyMigrationAsync(SqliteConnection connection, int version, CancellationToken cancellationToken)
    {
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, SchemaMigrations.All[version - 1], cancellationToken, transaction).ConfigureAwait(false);
        // PRAGMA parametre almaz; version bir int olduğundan metne çevirmek güvenlidir.
        await ExecuteAsync(connection, $"PRAGMA user_version = {version};", cancellationToken, transaction).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection, string sql, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
