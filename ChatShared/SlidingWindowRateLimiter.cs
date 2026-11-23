namespace ChatShared;

/// <summary>
/// Kayan pencere hız sınırlayıcı: herhangi bir pencere süresi içinde en fazla N isteğe izin verir.
/// Reddedilen denemeler sayılmaz, yani aşırı gönderen kişi engelini kendi uzatmaz.
/// Thread-safe'tir.
/// </summary>
public sealed class SlidingWindowRateLimiter
{
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly Queue<long> _timestamps = new();
    private readonly object _gate = new();

    public SlidingWindowRateLimiter(int limit, TimeSpan window, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        _limit = limit;
        _window = window;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Bir istek hakkı almaya çalışır; sınır aşılmışsa false döner.</summary>
    public bool TryAcquire()
    {
        long now = _timeProvider.GetTimestamp();

        lock (_gate)
        {
            while (_timestamps.Count > 0 && _timeProvider.GetElapsedTime(_timestamps.Peek(), now) >= _window)
            {
                _timestamps.Dequeue();
            }

            if (_timestamps.Count >= _limit)
            {
                return false;
            }

            _timestamps.Enqueue(now);
            return true;
        }
    }
}
