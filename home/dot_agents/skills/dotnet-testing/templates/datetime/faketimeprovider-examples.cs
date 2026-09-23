// =============================================================================
// FakeTimeProvider examples — time control inside tests.
// =============================================================================

using System;
using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace TimeProviderExamples.Tests;

#region FakeTimeProvider extensions

/// <summary>
/// FakeTimeProvider extensions — simplifies setting the clock.
/// </summary>
public static class FakeTimeProviderExtensions
{
    /// <summary>
    /// Sets the FakeTimeProvider to the given local wall-clock time using the local zone.
    /// </summary>
    /// <param name="fakeTimeProvider">The FakeTimeProvider instance.</param>
    /// <param name="localDateTime">Local time to pin.</param>
    public static void SetLocalNow(this FakeTimeProvider fakeTimeProvider, DateTime localDateTime)
    {
        fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var utcTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZoneInfo.Local);
        fakeTimeProvider.SetUtcNow(utcTime);
    }

    /// <summary>
    /// Convenience overload — set the clock to a specific year/month/day hour.
    /// </summary>
    public static void SetLocalNow(this FakeTimeProvider fakeTimeProvider, int year, int month, int day, int hour, int minute = 0, int second = 0)
    {
        var localDateTime = new DateTime(year, month, day, hour, minute, second);
        fakeTimeProvider.SetLocalNow(localDateTime);
    }
}

#endregion

#region Basic time-control tests

/// <summary>
/// OrderService unit tests — basic FakeTimeProvider usage.
/// </summary>
public class OrderServiceTests
{
    [Fact]
    public void CanPlaceOrder_WhenInsideBusinessHours_ShouldReturnTrue()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        // 2 PM — inside business hours
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 14, 0, 0));

        var sut = new OrderService(fakeTimeProvider);

        // Act
        var result = sut.CanPlaceOrder();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanPlaceOrder_WhenOutsideBusinessHours_ShouldReturnFalse()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        // 8 PM — after hours
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 20, 0, 0));

        var sut = new OrderService(fakeTimeProvider);

        // Act
        var result = sut.CanPlaceOrder();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetTimeBasedDiscount_OnFriday_ShouldReturnTenPercentOff()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        // 2024/3/15 is a Friday
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 14, 0, 0));

        var sut = new OrderService(fakeTimeProvider);

        // Act
        var discount = sut.GetTimeBasedDiscount();

        // Assert
        discount.Should().Be("Happy Friday: 10% off");
    }

    [Fact]
    public void GetTimeBasedDiscount_OnChristmasDay_ShouldReturnTwentyPercentOff()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        // Christmas day
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 12, 25, 10, 0, 0));

        var sut = new OrderService(fakeTimeProvider);

        // Act
        var discount = sut.GetTimeBasedDiscount();

        // Assert
        discount.Should().Be("Christmas special: 20% off");
    }

    [Fact]
    public void GetTimeBasedDiscount_OnOrdinaryDay_ShouldReturnNoDiscount()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        // 2024/3/11 is a Monday — no special discount
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 11, 14, 0, 0));

        var sut = new OrderService(fakeTimeProvider);

        // Act
        var discount = sut.GetTimeBasedDiscount();

        // Assert
        discount.Should().Be("No discount");
    }
}

#endregion

#region Boundary-parameterized tests

/// <summary>
/// Boundary tests — every edge of the business-hours window.
/// </summary>
public class OrderServiceBoundaryTests
{
    [Theory]
    [InlineData(8, false)]   // 8 AM — before open
    [InlineData(9, true)]    // 9 AM — just opened (boundary)
    [InlineData(12, true)]   // Noon
    [InlineData(16, true)]   // 4 PM
    [InlineData(17, false)]  // 5 PM — just closed (boundary)
    [InlineData(18, false)]  // 6 PM — after hours
    [InlineData(0, false)]   // Midnight
    [InlineData(23, false)]  // Late night
    public void CanPlaceOrder_AtVariousHours_ShouldReturnExpected(int hour, bool expected)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, hour, 0, 0));

        var sut = new OrderService(fakeTimeProvider);

        // Act
        var result = sut.CanPlaceOrder();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2024-03-15", "Happy Friday: 10% off")]    // Friday
    [InlineData("2024-03-11", "No discount")]               // Monday
    [InlineData("2024-12-25", "Christmas special: 20% off")] // Christmas
    [InlineData("2024-03-16", "No discount")]               // Saturday (not Friday)
    public void GetTimeBasedDiscount_OnVariousDates_ShouldReturnCorrectDiscount(string dateStr, string expected)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var date = DateTime.Parse(dateStr);
        fakeTimeProvider.SetLocalNow(date.AddHours(12)); // Noon

        var sut = new OrderService(fakeTimeProvider);

        // Act
        var discount = sut.GetTimeBasedDiscount();

        // Assert
        discount.Should().Be(expected);
    }
}

#endregion

#region Time-freeze tests

/// <summary>
/// Time-freeze tests — verify multiple operations happen "at the same instant".
/// </summary>
public class TimeFreezeTests
{
    [Fact]
    public void ProcessBatch_AtFixedInstant_ShouldProduceMatchingTimestamps()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var fixedTime = new DateTime(2024, 12, 25, 10, 30, 0);
        fakeTimeProvider.SetLocalNow(fixedTime);

        var processor = new BatchProcessor(fakeTimeProvider);

        // Act
        var result1 = processor.ProcessItem("Item1");
        var result2 = processor.ProcessItem("Item2");
        var result3 = processor.ProcessItem("Item3");

        // Assert — clock is frozen, all timestamps match
        result1.Timestamp.Should().Be(fixedTime);
        result2.Timestamp.Should().Be(fixedTime);
        result3.Timestamp.Should().Be(fixedTime);
    }
}

public class BatchProcessor
{
    private readonly TimeProvider _timeProvider;

    public BatchProcessor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public ProcessResult ProcessItem(string item)
    {
        return new ProcessResult
        {
            Item = item,
            Timestamp = _timeProvider.GetLocalNow().DateTime
        };
    }
}

public class ProcessResult
{
    public string Item { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

#endregion

#region Time-advance tests (Advance)

/// <summary>
/// Time-advance tests using Advance().
/// </summary>
public class TimeAdvanceTests
{
    [Fact]
    public void Cache_AfterExpirationElapses_ShouldEvictItem()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var startTime = new DateTime(2024, 3, 15, 10, 0, 0);
        fakeTimeProvider.SetLocalNow(startTime);

        var cache = new TimedCache<string>(fakeTimeProvider, TimeSpan.FromMinutes(5));

        // Act & Assert — set entry at 10:00
        cache.Set("key1", "value1");
        cache.Get("key1").Should().Be("value1");

        // 3 minutes in (10:03) — still valid (3 < 5)
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(3));
        cache.Get("key1").Should().Be("value1");

        // Another 3 minutes (10:06) — expired (6 > 5)
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(3));
        cache.Get("key1").Should().BeNull();
    }

    [Fact]
    public void Cache_AtBoundary_ShouldExpireAtTheExactSecond()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 10, 0, 0));

        var cache = new TimedCache<string>(fakeTimeProvider, TimeSpan.FromMinutes(5));
        cache.Set("key", "value");

        // 4 min 59 sec — still valid
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(59)));
        cache.Get("key").Should().Be("value");

        // 2 more seconds — gone
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(2));
        cache.Get("key").Should().BeNull();
    }

    [Fact]
    public void Token_UsingAdvance_ShouldValidateExpiry()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 10, 0, 0));

        var tokenService = new TokenService(fakeTimeProvider);
        var token = tokenService.GenerateToken("user123", TimeSpan.FromHours(1));

        // Immediately valid
        tokenService.ValidateToken(token).Should().BeTrue();

        // 30 minutes later — still valid
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(30));
        tokenService.ValidateToken(token).Should().BeTrue();

        // 31 more minutes (61 total) — expired
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(31));
        tokenService.ValidateToken(token).Should().BeFalse();
    }
}

/// <summary>
/// Generic timed cache.
/// </summary>
public class TimedCache<T>
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, CacheItem<T>> _cache = new();

    public TimeSpan DefaultExpiry { get; }

    public TimedCache(TimeProvider timeProvider, TimeSpan defaultExpiry)
    {
        _timeProvider = timeProvider;
        DefaultExpiry = defaultExpiry;
    }

    public void Set(string key, T value, TimeSpan? expiry = null)
    {
        var expiryTime = _timeProvider.GetUtcNow().Add(expiry ?? DefaultExpiry);
        _cache[key] = new CacheItem<T>(value, expiryTime);
    }

    public T? Get(string key)
    {
        if (!_cache.TryGetValue(key, out var item))
            return default;

        if (item.ExpiryTime <= _timeProvider.GetUtcNow())
        {
            _cache.Remove(key);
            return default;
        }

        return item.Value;
    }
}

public record CacheItem<T>(T Value, DateTimeOffset ExpiryTime);

/// <summary>
/// Token service sample.
/// </summary>
public class TokenService
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, TokenInfo> _tokens = new();

    public TokenService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string GenerateToken(string userId, TimeSpan validity)
    {
        var token = Guid.NewGuid().ToString();
        var expiryTime = _timeProvider.GetUtcNow().Add(validity);

        _tokens[token] = new TokenInfo(userId, expiryTime);
        return token;
    }

    public bool ValidateToken(string token)
    {
        if (!_tokens.TryGetValue(token, out var info))
            return false;

        return info.ExpiryTime > _timeProvider.GetUtcNow();
    }
}

public record TokenInfo(string UserId, DateTimeOffset ExpiryTime);

#endregion

#region Time-rewind tests

/// <summary>
/// Time-rewind tests — historical data processing.
/// </summary>
public class TimeRewindTests
{
    [Fact]
    public void HistoricalDataProcessor_WhenClockIsInThePast_ShouldStampWithPastTime()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        // Back to 2020
        var historicalTime = new DateTime(2020, 1, 15, 9, 0, 0);
        fakeTimeProvider.SetLocalNow(historicalTime);

        var processor = new HistoricalDataProcessor(fakeTimeProvider);

        // Act
        var result = processor.ProcessDataForDate(historicalTime.Date);

        // Assert
        result.Should().NotBeNull();
        result.ProcessedAt.Should().Be(historicalTime);
    }
}

public class HistoricalDataProcessor
{
    private readonly TimeProvider _timeProvider;

    public HistoricalDataProcessor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public HistoricalResult ProcessDataForDate(DateTime date)
    {
        return new HistoricalResult
        {
            Date = date,
            ProcessedAt = _timeProvider.GetLocalNow().DateTime
        };
    }
}

public class HistoricalResult
{
    public DateTime Date { get; set; }
    public DateTime ProcessedAt { get; set; }
}

#endregion

#region Schedule and trading-window tests

/// <summary>
/// Schedule service tests.
/// </summary>
public class ScheduleServiceTests
{
    [Theory]
    [InlineData("2024-03-15 14:30:00", "2024-03-15 14:00:00", true)]  // past due
    [InlineData("2024-03-15 13:30:00", "2024-03-15 14:00:00", false)] // not yet
    [InlineData("2024-03-15 14:00:00", "2024-03-15 14:00:00", true)]  // exact boundary
    public void ShouldExecuteJob_BasedOnClock_ShouldReturnExpected(
        string currentTimeStr,
        string scheduledTimeStr,
        bool expected)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var currentTime = DateTime.Parse(currentTimeStr);
        var scheduledTime = DateTime.Parse(scheduledTimeStr);

        fakeTimeProvider.SetLocalNow(currentTime);

        var schedule = new JobSchedule { NextExecutionTime = scheduledTime };
        var sut = new ScheduleService(fakeTimeProvider);

        // Act
        var result = sut.ShouldExecuteJob(schedule);

        // Assert
        result.Should().Be(expected);
    }
}

/// <summary>
/// Trading service tests.
/// </summary>
public class TradingServiceTests
{
    [Theory]
    [InlineData("09:30:00", true)]   // morning session
    [InlineData("11:15:00", true)]   // before morning close
    [InlineData("12:00:00", false)]  // lunch break
    [InlineData("14:30:00", true)]   // afternoon session
    [InlineData("15:30:00", false)]  // after afternoon close
    [InlineData("09:00:00", true)]   // morning open (boundary)
    [InlineData("15:00:00", true)]   // afternoon close (boundary)
    public void IsInTradingHours_AtVariousMoments_ShouldReturnExpected(string timeStr, bool expected)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var testTime = DateTime.Today.Add(TimeSpan.Parse(timeStr));
        fakeTimeProvider.SetLocalNow(testTime);

        var sut = new TradingService(fakeTimeProvider);

        // Act
        var result = sut.IsInTradingHours();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(DayOfWeek.Saturday, 0)]     // no trading on Saturday
    [InlineData(DayOfWeek.Sunday, 0)]       // no trading on Sunday
    [InlineData(DayOfWeek.Monday, 1.0)]     // normal Monday
    [InlineData(DayOfWeek.Friday, 1.1)]     // Friday afternoon volatility (hour >= 14)
    public void GetMarketMultiplier_OnVariousDays_ShouldReturnExpected(DayOfWeek dayOfWeek, decimal expected)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        var date = GetNextWeekday(new DateTime(2024, 3, 1), dayOfWeek);
        fakeTimeProvider.SetLocalNow(date.AddHours(15)); // 3 PM

        var sut = new TradingService(fakeTimeProvider);

        // Act
        var result = sut.GetMarketMultiplier();

        // Assert
        result.Should().Be(expected);
    }

    private static DateTime GetNextWeekday(DateTime start, DayOfWeek day)
    {
        int daysToAdd = ((int)day - (int)start.DayOfWeek + 7) % 7;
        return start.AddDays(daysToAdd == 0 && start.DayOfWeek != day ? 7 : daysToAdd);
    }
}

#endregion

#region Timezone tests

/// <summary>
/// Global time service tests — timezone handling.
/// </summary>
public class GlobalTimeServiceTests
{
    [Theory]
    [InlineData("UTC", "2024-03-15 10:00:00")]
    [InlineData("Tokyo Standard Time", "2024-03-15 19:00:00")]
    [InlineData("Eastern Standard Time", "2024-03-15 06:00:00")]
    public void GetTimeInTimeZone_ForVariousZones_ShouldReturnConvertedTime(string timeZoneId, string expectedTimeStr)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var baseUtcTime = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        fakeTimeProvider.SetUtcNow(baseUtcTime);

        var sut = new GlobalTimeService(fakeTimeProvider);
        var expectedTime = DateTime.Parse(expectedTimeStr);

        // Act
        var result = sut.GetTimeInTimeZone(timeZoneId);

        // Assert
        result.DateTime.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1));
    }
}

#endregion

#region Test-isolation strategy

/// <summary>
/// Demonstrates correct test-isolation strategy.
/// </summary>
public class TimeServiceTestsWithIsolation : IDisposable
{
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly GlobalTimeService _sut;

    public TimeServiceTestsWithIsolation()
    {
        // Each test instance owns its own FakeTimeProvider.
        _fakeTimeProvider = new FakeTimeProvider();
        _sut = new GlobalTimeService(_fakeTimeProvider);
    }

    public void Dispose()
    {
        // FakeTimeProvider implements IDisposable.
        _fakeTimeProvider?.Dispose();
    }

    [Fact]
    public void Test1_SetToJan1_ShouldShowJanuary()
    {
        _fakeTimeProvider.SetLocalNow(new DateTime(2024, 1, 1, 12, 0, 0));

        var result = _sut.GetCurrentTimeString();

        result.Should().Contain("2024-01-01");
    }

    [Fact]
    public void Test2_SetToDec31_ShouldShowDecember()
    {
        _fakeTimeProvider.SetLocalNow(new DateTime(2024, 12, 31, 12, 0, 0));

        var result = _sut.GetCurrentTimeString();

        result.Should().Contain("2024-12-31");
    }
}

#endregion
