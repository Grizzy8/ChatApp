namespace ChatServer;

/// <summary>Sunucu arayüzünde listelenen bir istemcinin temel bilgileri.</summary>
public sealed record ClientInfo(string Username, string Endpoint, DateTime ConnectedAt);
