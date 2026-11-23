namespace ChatServer.Storage;

/// <summary>Bir kullanıcının kişi defterindeki bir kişi.</summary>
public sealed record ContactRecord(string UserId, string Username, DateTime AddedAtUtc);

/// <summary>Kişi ekleme isteğinin sonucu.</summary>
public enum AddContactOutcome
{
    Added,
    AlreadyExists,
    NotFound,
    CannotAddSelf,
    LimitReached
}
