namespace ChatShared;

/// <summary>Kişi listesindeki bir kişi: benzersiz kimliği ve kullanıcı adı. Çevrimiçi durumu istemci ayrıca bilir.</summary>
public sealed record ContactInfo(string UserId, string Username);
