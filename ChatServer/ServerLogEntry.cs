namespace ChatServer;

/// <summary>Sunucu log satırının önem düzeyi.</summary>
public enum ServerLogLevel
{
    Info,
    Error
}

/// <summary>Sunucunun arayüze bildirdiği tek bir log kaydı.</summary>
public sealed record ServerLogEntry(DateTime Time, string Message, ServerLogLevel Level);
