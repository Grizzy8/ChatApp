namespace ChatServer.Storage;

/// <summary>
/// Veritabanı şeması sürümleri. Her öğe bir sürümdür: indeks + 1 = şema sürümü (PRAGMA user_version).
/// Yayınlanmış bir sürüm asla değiştirilmez; yeni bir özellik yeni bir öğe ekler.
/// </summary>
internal static class SchemaMigrations
{
    internal static readonly IReadOnlyList<string> All =
    [
        // v1: hesaplar. UsernameKey, kullanıcı adının büyük/küçük harf duyarsız karşılığıdır
        // ("Ali" = "ali"); uygulamadaki OrdinalIgnoreCase kuralıyla aynı olsun diye kodda üretilir
        // (SQLite'ın NOCASE'i Türkçe harfleri katlamaz).
        """
        CREATE TABLE Users (
            Id           TEXT NOT NULL PRIMARY KEY,
            Username     TEXT NOT NULL,
            UsernameKey  TEXT NOT NULL UNIQUE,
            PasswordHash TEXT NOT NULL,
            CreatedAt    TEXT NOT NULL
        ) STRICT;
        """,

        // v2: kişi defteri. Tek yönlüdür: A, B'yi eklerse yalnızca A'nın listesinde B olur.
        // Hesap silinirse ona ait kişi kayıtları da silinir; kendini eklemek veritabanı düzeyinde de yasaktır.
        """
        CREATE TABLE Contacts (
            OwnerId   TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            ContactId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            AddedAt   TEXT NOT NULL,
            PRIMARY KEY (OwnerId, ContactId),
            CHECK (OwnerId <> ContactId)
        ) STRICT;
        """,

        // v3: çevrimdışı alıcıya bırakılan özel mesajlar. Yalnızca teslim bekleyen mesajlar tutulur; teslim edilince satır silinir
        // (sunucu sohbet geçmişi tutmaz). Payload sunucu için opak bir metindir: şimdilik düz metin, uçtan uca
        // şifreleme eklendiğinde şifreli veri olacak. AUTOINCREMENT: kimlikler asla yeniden kullanılmaz, sıra korunur.
        """
        CREATE TABLE PendingMessages (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            RecipientId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            SenderId    TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            Payload     TEXT NOT NULL,
            SentAt      TEXT NOT NULL
        ) STRICT;
        CREATE INDEX IX_PendingMessages_Recipient ON PendingMessages (RecipientId, Id);
        """
    ];

    internal static int LatestVersion => All.Count;
}
