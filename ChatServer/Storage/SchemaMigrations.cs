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
        """,

        // v4: gruplar, üyelikler ve bekleyen davetler. Rol metin olarak saklanır ve CHECK ile sınırlıdır.
        // Davet = Invites satırı; çevrimdışı kullanıcı girince görür, kabul/red ile satır silinir.
        // Hesap ya da grup silinince bağlı üyelik ve davetler de silinir.
        """
        CREATE TABLE Groups (
            Id        TEXT NOT NULL PRIMARY KEY,
            Name      TEXT NOT NULL,
            OwnerId   TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            CreatedAt TEXT NOT NULL
        ) STRICT;

        CREATE TABLE GroupMembers (
            GroupId  TEXT NOT NULL REFERENCES Groups(Id) ON DELETE CASCADE,
            UserId   TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            Role     TEXT NOT NULL CHECK (Role IN ('Owner', 'Admin', 'Member')),
            JoinedAt TEXT NOT NULL,
            PRIMARY KEY (GroupId, UserId)
        ) STRICT;
        CREATE INDEX IX_GroupMembers_User ON GroupMembers (UserId);

        CREATE TABLE GroupInvites (
            GroupId   TEXT NOT NULL REFERENCES Groups(Id) ON DELETE CASCADE,
            InviteeId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            InviterId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
            CreatedAt TEXT NOT NULL,
            PRIMARY KEY (GroupId, InviteeId)
        ) STRICT;
        CREATE INDEX IX_GroupInvites_Invitee ON GroupInvites (InviteeId);
        """,

        // v5: çevrimdışı grup üyeleri için üye başına kuyruk. GroupId doluysa kayıt bir grup mesajıdır (alıcı: RecipientId);
        // boşsa özel mesajdır. Grup silinince bekleyen grup mesajları da silinir.
        """
        ALTER TABLE PendingMessages ADD COLUMN GroupId TEXT REFERENCES Groups(Id) ON DELETE CASCADE;
        """
    ];

    internal static int LatestVersion => All.Count;
}
