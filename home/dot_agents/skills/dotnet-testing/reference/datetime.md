# Testing time-dependent code

`DateTime.Now` and `DateTimeOffset.UtcNow` are static. Once they appear in production code, the code is no longer *Repeatable* — different machine, different timezone, different second, different test outcome. The fix is to inject `System.TimeProvider` everywhere a clock is needed, and substitute `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider` in tests.

FIRST applies — see [reference/fundamentals.md](fundamentals.md).

## Refactor: replace `DateTime.Now` with `TimeProvider`

Before:

```csharp
public class OrderService
{
    public bool CanPlaceOrder()
    {
        var now = DateTime.Now;
        return now.Hour >= 9 && now.Hour < 17;
    }
}
```

After:

```csharp
public class OrderService
{
    private readonly TimeProvider _timeProvider;

    public OrderService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public bool CanPlaceOrder()
    {
        var now = _timeProvider.GetLocalNow();
        return now.Hour >= 9 && now.Hour < 17;
    }
}
```

DI registration in `Program.cs`:

```csharp
services.AddSingleton(TimeProvider.System);
services.AddScoped<OrderService>();
```

Production never sees `FakeTimeProvider`; tests never see `TimeProvider.System`.

**Full SUT + DI example:** [templates/datetime/timeprovider-basics.cs](../templates/datetime/timeprovider-basics.cs)

## `TimeProvider` core API

`TimeProvider.System` is the production implementation. The interface surface — same for `FakeTimeProvider` — lets you ask:

```csharp
TimeProvider clock = TimeProvider.System;

DateTimeOffset utcNow   = clock.GetUtcNow();        // UTC instant
DateTimeOffset localNow = clock.GetLocalNow();      // adjusted for LocalTimeZone
TimeZoneInfo zone       = clock.LocalTimeZone;      // currently configured local zone

long start    = clock.GetTimestamp();               // high-resolution counter ticks
// ... do work ...
long end      = clock.GetTimestamp();
TimeSpan span = clock.GetElapsedTime(start, end);   // ticks → TimeSpan
```

Use `GetTimestamp` / `GetElapsedTime` for perf-style measurements in production code instead of `Stopwatch` — same accuracy, same test seam.

## `FakeTimeProvider` API

From `Microsoft.Extensions.TimeProvider.Testing`.

| Member                           | Purpose                                          | Use when                                   |
| -------------------------------- | ------------------------------------------------ | ------------------------------------------ |
| `SetUtcNow(DateTimeOffset)`      | Pin the UTC clock to an exact instant.           | You need a deterministic UTC moment.       |
| `SetLocalTimeZone(TimeZoneInfo)` | Pin the simulated local zone.                    | Testing timezone-sensitive logic.          |
| `Advance(TimeSpan)`              | Move the clock forward instantly (non-blocking). | Cache expiry, token expiry, retry windows. |
| `GetUtcNow()`                    | Read the simulated UTC time.                     | Asserting on what the clock returned.      |
| `GetLocalNow()`                  | Read the simulated local time.                   | Asserting on what the SUT saw.             |

### Helper: `SetLocalNow`

A common need is "pretend it's exactly this local wall-clock time." There's no built-in single call for that; wrap it once:

```csharp
public static class FakeTimeProviderExtensions
{
    public static void SetLocalNow(this FakeTimeProvider fakeTimeProvider, DateTime localDateTime)
    {
        fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var utc = TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZoneInfo.Local);
        fakeTimeProvider.SetUtcNow(utc);
    }
}
```

## Each test owns its `FakeTimeProvider`

A shared `static readonly FakeTimeProvider` is a defect — tests will set the clock for each other and produce order-dependent failures. If you need shared setup, prefer the constructor-and-`Dispose` pattern so each test instance still owns a fresh provider.

```csharp
public class TimeServiceTests : IDisposable
{
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly GlobalTimeService _sut;

    public TimeServiceTests() => _sut = new GlobalTimeService(_fakeTimeProvider);

    public void Dispose() => _fakeTimeProvider.Dispose();
}
```

## Freezing time

When the SUT calls the clock more than once and you want every call to see the same instant, just `SetLocalNow` once and don't `Advance`. The clock is frozen by default.

## Fast-forwarding (cache expiry, token expiry, retries)

```csharp
[Fact]
public void Cache_AfterExpirationElapses_ShouldEvictItem()
{
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 10, 0, 0));

    var cache = new TimedCache<string>(fakeTimeProvider, TimeSpan.FromMinutes(5));
    cache.Set("key", "value");

    // 3 minutes — not expired
    fakeTimeProvider.Advance(TimeSpan.FromMinutes(3));
    cache.Get("key").Should().Be("value");

    // 6 minutes total — expired
    fakeTimeProvider.Advance(TimeSpan.FromMinutes(3));
    cache.Get("key").Should().BeNull();
}
```

`Advance()` is instant — no `Thread.Sleep`, no real wait. That's the whole point. The umbrella's absolute bans forbid sleeping in tests; `Advance()` is the alternative.

**Full examples (freeze, advance, token, boundary, schedule, trading, timezone):** [templates/datetime/faketimeprovider-examples.cs](../templates/datetime/faketimeprovider-examples.cs)

## Boundary parameterisation

Drive every boundary — the value just inside, the value just outside, and the corner cases.

```csharp
[Theory]
[InlineData(8, false)]   // before open
[InlineData(9, true)]    // open (inclusive)
[InlineData(17, false)]  // close (exclusive)
[InlineData(0, false)]   // midnight
public void CanPlaceOrder_AtVariousHours_ShouldReturnExpected(int hour, bool expected)
{
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, hour, 0, 0));

    var sut = new OrderService(fakeTimeProvider);

    sut.CanPlaceOrder().Should().Be(expected);
}
```

## Timezone-sensitive tests

When the behavior under test depends on the local zone, set it explicitly. Don't rely on the CI machine's zone:

```csharp
[Fact]
public void IsBusinessHours_InTokyoZone_ShouldUseTokyoLocalTime()
{
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo"));
    fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 3, 15, 5, 0, 0, TimeSpan.Zero)); // 14:00 in Tokyo

    var sut = new OrderService(fakeTimeProvider);

    sut.CanPlaceOrder().Should().BeTrue();
}
```

On Linux/macOS use IANA zone IDs (`"Asia/Tokyo"`); on Windows the Windows IDs also resolve. `TimeZoneInfo.FindSystemTimeZoneById` accepts both since .NET 6.

## Historical / time-rewind scenarios

Nothing stops you from setting the clock to *the past* and verifying that the SUT processes historical data correctly — `SetLocalNow(new DateTime(2020, ...))` is all that's needed.

## AutoFixture integration

Goal: `FakeTimeProvider` injected automatically wherever the SUT asks for a `TimeProvider`.

```csharp
public class FakeTimeProviderCustomization : ICustomization
{
    public void Customize(IFixture fixture) => fixture.Register(() => new FakeTimeProvider());
}

public class AutoDataWithCustomizationAttribute : AutoDataAttribute
{
    public AutoDataWithCustomizationAttribute() : base(CreateFixture) { }

    private static IFixture CreateFixture() =>
        new Fixture()
            .Customize(new AutoNSubstituteCustomization())
            .Customize(new FakeTimeProviderCustomization());
}
```

Used with `[Frozen(Matching.DirectBaseType)]` so the same `FakeTimeProvider` instance is injected wherever a `TimeProvider` is requested:

```csharp
[Theory]
[AutoDataWithCustomization]
public void GetTimeBasedDiscount_OnFriday_ShouldReturnTenPercent(
    [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
    OrderService sut)
{
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 14, 0, 0)); // a Friday

    sut.GetTimeBasedDiscount().Should().Be("Happy Friday: 10% off");
}
```

### Why `Matching.DirectBaseType` matters

`OrderService` takes `TimeProvider` (the abstract base). The test wants `FakeTimeProvider` (the derived testing implementation). Without `Matching.DirectBaseType`, AutoFixture sees a `[Frozen] FakeTimeProvider` request and freezes only `FakeTimeProvider` — when it then composes `OrderService`, it sees a `TimeProvider` parameter and creates *a different* provider. The two don't share state, and your `SetLocalNow` calls have no effect on the SUT.

`Matching.DirectBaseType` tells AutoFixture: "when something asks for the direct base type of this frozen instance (`TimeProvider`), hand it the same `FakeTimeProvider`." Now `SetLocalNow` reaches the SUT.

**Full AutoFixture + InlineAutoData examples:** [templates/datetime/autofixture-integration.cs](../templates/datetime/autofixture-integration.cs)

See [reference/autofixture.md](autofixture.md).

## Aspire note

When using .NET Aspire Testing, register `TimeProvider.System` in the host the same way you would in any other ASP.NET app and inject `TimeProvider` everywhere you'd previously used `DateTime.UtcNow`. The `FakeTimeProvider` substitution then works in the test project against the AppHost-orchestrated services. See [reference/aspire.md](aspire.md).

## Checklist

- [ ] Production code takes a `TimeProvider` constructor parameter — never reads `DateTime.Now` / `DateTimeOffset.UtcNow` directly.
- [ ] DI registers `TimeProvider.System` in production composition.
- [ ] Each test creates its own `FakeTimeProvider` (or owns one via constructor + Dispose).
- [ ] `SetLocalNow` / `SetUtcNow` to pin the start, `Advance` to fast-forward.
- [ ] Timezone-sensitive tests set the zone explicitly with `SetLocalTimeZone`.
- [ ] AutoFixture wiring uses `[Frozen(Matching.DirectBaseType)]` when freezing `FakeTimeProvider` to satisfy `TimeProvider` constructor params.
- [ ] No `Thread.Sleep`, no `Task.Delay` waiting for time to pass.

## Packages

- `Microsoft.Bcl.TimeProvider` — production-side polyfills (only needed pre-.NET 8).
- `Microsoft.Extensions.TimeProvider.Testing` — `FakeTimeProvider`, test projects only.

## Cross-links

- AutoFixture + `[Frozen]` — [reference/autofixture.md](autofixture.md)
- Sleeping in tests is banned — [reference/output-logging.md](output-logging.md) (use checkpoints + `Advance` instead)
- Aspire testing — [reference/aspire.md](aspire.md)
- Builder pattern for entities that carry timestamps — [reference/builder-pattern.md](builder-pattern.md)
