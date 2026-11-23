using ChatShared;

namespace ChatShared.Test;

public class SlidingWindowRateLimiterTests
{
    /// <summary>Testlerin saati elle ilerletebilmesi için sahte zaman sağlayıcısı.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan amount) => _ticks += amount.Ticks;
    }

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    [Fact]
    public void TryAcquire_AllowsUpToLimit_ThenRejects()
    {
        var limiter = new SlidingWindowRateLimiter(3, Window, new ManualTimeProvider());

        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_AllowsAgain_AfterWindowPasses()
    {
        var time = new ManualTimeProvider();
        var limiter = new SlidingWindowRateLimiter(2, Window, time);
        limiter.TryAcquire();
        limiter.TryAcquire();
        Assert.False(limiter.TryAcquire());

        time.Advance(Window);

        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_SlidesWindow_FreeingOneSlotAtATime()
    {
        var time = new ManualTimeProvider();
        var limiter = new SlidingWindowRateLimiter(2, Window, time);
        limiter.TryAcquire();            // t = 0
        time.Advance(TimeSpan.FromSeconds(3));
        limiter.TryAcquire();            // t = 3

        time.Advance(TimeSpan.FromSeconds(2)); // t = 5: ilk istek penceren çıktı, ikincisi içeride

        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_RejectedAttempts_DoNotExtendTheBlock()
    {
        var time = new ManualTimeProvider();
        var limiter = new SlidingWindowRateLimiter(1, Window, time);
        limiter.TryAcquire();            // t = 0

        for (int i = 0; i < 4; i++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            Assert.False(limiter.TryAcquire()); // reddedilen denemeler sayılmaz
        }

        time.Advance(TimeSpan.FromSeconds(1)); // t = 5: ilk isteğin süresi doldu

        Assert.True(limiter.TryAcquire());
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(3, 0)]
    [InlineData(3, -5)]
    public void Constructor_Throws_ForInvalidArguments(int limit, int windowSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SlidingWindowRateLimiter(limit, TimeSpan.FromSeconds(windowSeconds)));
    }

    [Fact]
    public void TryAcquire_IsThreadSafe_NeverExceedsLimit()
    {
        var limiter = new SlidingWindowRateLimiter(50, TimeSpan.FromMinutes(1), new ManualTimeProvider());
        int allowed = 0;

        Parallel.For(0, 1000, _ =>
        {
            if (limiter.TryAcquire())
            {
                Interlocked.Increment(ref allowed);
            }
        });

        Assert.Equal(50, allowed);
    }
}
