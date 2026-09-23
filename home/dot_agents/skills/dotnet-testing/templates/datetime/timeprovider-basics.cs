// =============================================================================
// TimeProvider basics — refactoring time-dependent code into a testable shape.
// =============================================================================

using System;

namespace TimeProviderExamples;

#region Problem: legacy DateTime is untestable

/// <summary>
/// Problem code: reads DateTime.Now directly, so time cannot be controlled from tests.
/// </summary>
public class LegacyOrderService
{
    public bool CanPlaceOrder()
    {
        // Uses static time — test outcome depends on wall-clock time.
        var now = DateTime.Now;
        var currentHour = now.Hour;

        // Business hours: 9am to 5pm
        return currentHour >= 9 && currentHour < 17;
    }

    public string GetTimeBasedDiscount()
    {
        var today = DateTime.Today;

        if (today.DayOfWeek == DayOfWeek.Friday)
        {
            return "Happy Friday: 10% off";
        }

        if (today.Month == 12 && today.Day == 25)
        {
            return "Christmas special: 20% off";
        }

        return "No discount";
    }
}

#endregion

#region Solution: inject TimeProvider

/// <summary>
/// Testable code — receives a TimeProvider via DI.
/// </summary>
public class OrderService
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Constructor-injected TimeProvider.
    /// Production: TimeProvider.System.
    /// Tests: FakeTimeProvider.
    /// </summary>
    public OrderService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Returns whether the SUT is allowed to place an order right now.
    /// </summary>
    public bool CanPlaceOrder()
    {
        var now = _timeProvider.GetLocalNow();
        var currentHour = now.Hour;

        // Business hours: 9am to 5pm
        return currentHour >= 9 && currentHour < 17;
    }

    /// <summary>
    /// Returns a discount label based on the current date.
    /// </summary>
    public string GetTimeBasedDiscount()
    {
        var today = _timeProvider.GetLocalNow().Date;

        if (today.DayOfWeek == DayOfWeek.Friday)
        {
            return "Happy Friday: 10% off";
        }

        if (today.Month == 12 && today.Day == 25)
        {
            return "Christmas special: 20% off";
        }

        return "No discount";
    }
}

#endregion

#region TimeProvider API reference

/// <summary>
/// TimeProvider core API reference.
/// </summary>
public static class TimeProviderApiReference
{
    public static void ShowTimeProviderUsage()
    {
        // 1. System time provider (production).
        TimeProvider systemProvider = TimeProvider.System;

        // 2. UTC time
        DateTimeOffset utcNow = systemProvider.GetUtcNow();
        Console.WriteLine($"UTC time: {utcNow}");

        // 3. Local time
        DateTimeOffset localNow = systemProvider.GetLocalNow();
        Console.WriteLine($"Local time: {localNow}");

        // 4. Local time zone
        TimeZoneInfo localTimeZone = systemProvider.LocalTimeZone;
        Console.WriteLine($"Local zone: {localTimeZone.DisplayName}");

        // 5. High-resolution timestamp (perf measurement).
        long timestamp = systemProvider.GetTimestamp();
        Console.WriteLine($"Timestamp: {timestamp}");

        // 6. Elapsed time between two timestamps.
        long startTimestamp = systemProvider.GetTimestamp();
        // ... do work ...
        long endTimestamp = systemProvider.GetTimestamp();
        TimeSpan elapsed = systemProvider.GetElapsedTime(startTimestamp, endTimestamp);
        Console.WriteLine($"Elapsed: {elapsed}");
    }
}

#endregion

#region DI registration

/// <summary>
/// DI registration example.
/// </summary>
public static class DependencyInjectionSetup
{
    /*
    // Program.cs or Startup.cs

    // Production — use the system clock.
    services.AddSingleton(TimeProvider.System);
    services.AddScoped<OrderService>();

    // Development override (if you need to demo a specific date)
    if (builder.Environment.IsDevelopment())
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 12, 25, 10, 0, 0)); // Christmas demo
        services.AddSingleton<TimeProvider>(fakeTimeProvider);
    }
    */
}

#endregion

#region Real business logic samples

/// <summary>
/// Schedule service — time-dependent scheduling logic.
/// </summary>
public class ScheduleService
{
    private readonly TimeProvider _timeProvider;

    public ScheduleService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns whether the job should execute now.
    /// </summary>
    public bool ShouldExecuteJob(JobSchedule schedule)
    {
        var now = _timeProvider.GetLocalNow();
        return schedule.NextExecutionTime <= now;
    }

    /// <summary>
    /// Calculates the next execution time based on a cron-like expression.
    /// </summary>
    public DateTime CalculateNextExecution(JobSchedule schedule)
    {
        var now = _timeProvider.GetLocalNow();

        return schedule.CronExpression switch
        {
            "0 0 * * *" => now.Date.AddDays(1),    // Every midnight
            "0 0 * * 1" => GetNextMonday(now),     // Every Monday midnight
            _ => now.DateTime.AddHours(1)          // Default: hourly
        };
    }

    private DateTime GetNextMonday(DateTimeOffset now)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        return now.Date.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday);
    }
}

public class JobSchedule
{
    public DateTime NextExecutionTime { get; set; }
    public string CronExpression { get; set; } = string.Empty;
}

/// <summary>
/// Trading service — time-window logic.
/// </summary>
public class TradingService
{
    private readonly TimeProvider _timeProvider;

    public TradingService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns whether the market is in a trading window.
    /// Windows: 9:00–11:30, 13:00–15:00.
    /// </summary>
    public bool IsInTradingHours()
    {
        var now = _timeProvider.GetLocalNow();
        var currentTime = now.TimeOfDay;

        return (currentTime >= TimeSpan.FromHours(9) && currentTime <= TimeSpan.FromHours(11.5)) ||
               (currentTime >= TimeSpan.FromHours(13) && currentTime <= TimeSpan.FromHours(15));
    }

    /// <summary>
    /// Returns a market multiplier based on weekday and time-of-day.
    /// </summary>
    public decimal GetMarketMultiplier()
    {
        var now = _timeProvider.GetLocalNow();

        return now.DayOfWeek switch
        {
            DayOfWeek.Saturday or DayOfWeek.Sunday => 0m,       // No trading on weekends
            DayOfWeek.Friday when now.Hour >= 14 => 1.1m,        // Higher volatility Fri afternoons
            _ => 1.0m
        };
    }
}

/// <summary>
/// Global time service — timezone handling.
/// </summary>
public class GlobalTimeService
{
    private readonly TimeProvider _timeProvider;

    public GlobalTimeService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns the current time in the specified time zone.
    /// </summary>
    public DateTimeOffset GetTimeInTimeZone(string timeZoneId)
    {
        var utcNow = _timeProvider.GetUtcNow();
        var targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo.ConvertTime(utcNow, targetTimeZone);
    }

    /// <summary>
    /// Returns the current local time as a string.
    /// </summary>
    public string GetCurrentTimeString()
    {
        return _timeProvider.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Returns the current local time.
    /// </summary>
    public DateTime GetCurrentTime()
    {
        return _timeProvider.GetLocalNow().DateTime;
    }
}

/// <summary>
/// Audit logger — stores UTC, displays local.
/// </summary>
public class AuditLogger
{
    private readonly TimeProvider _timeProvider;

    public AuditLogger(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Captures an audit log entry — UTC timestamp stored, local time for display.
    /// </summary>
    public AuditLog LogActivity(string activity)
    {
        var utcTimestamp = _timeProvider.GetUtcNow();
        var localTime = _timeProvider.GetLocalNow();

        return new AuditLog
        {
            Activity = activity,
            UtcTimestamp = utcTimestamp,
            LocalTimeDisplay = localTime.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }
}

public class AuditLog
{
    public string Activity { get; set; } = string.Empty;
    public DateTimeOffset UtcTimestamp { get; set; }
    public string LocalTimeDisplay { get; set; } = string.Empty;
}

#endregion
