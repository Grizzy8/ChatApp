namespace ChatServer;

/// <summary>Sunucu arayüzünde listelenen bir istemcinin temel bilgileri.</summary>
/// <remarks>Gizlilik için kullanıcı adı değil yalnızca hesap kimliği (UserId) gösterilir.</remarks>
public sealed record ClientInfo(string UserId, string Endpoint, DateTime ConnectedAt);
