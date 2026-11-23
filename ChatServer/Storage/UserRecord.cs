namespace ChatServer.Storage;

/// <summary>Veritabanındaki bir hesap. PasswordHash, PasswordHasher'ın ürettiği kendini tanımlayan metindir; asla log'a yazılmaz.</summary>
public sealed record UserRecord(string Id, string Username, string PasswordHash, DateTime CreatedAtUtc);
