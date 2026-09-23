# Test output and logging

`ITestOutputHelper` is xUnit's per-test write surface. This file covers how to inject it, how to write usefully into it, and how to verify code that calls `ILogger` without coupling tests to log-string text.

Reminder from SKILL.md: **never assert on log message text as the primary verification.** Logging is diagnostic, not the gate. Everything below assumes that.

## `ITestOutputHelper` injection

Inject via the constructor. The instance is bound to a single test class instance and a single test method — do not stash it in a static field or call it from `Dispose`.

```csharp
public class MyTests
{
    private readonly ITestOutputHelper _output;

    public MyTests(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public void Method_Scenario_ShouldDoSomething()
    {
        _output.WriteLine("Setup complete.");
    }
}
```

Common mistakes:

- Static field (`private static ITestOutputHelper _output`) — throws when accessed from a method other than the one xUnit bound it to.
- Calling it from an async continuation after the test has returned.
- Calling it from `Dispose` — the test has already completed; output is discarded.

**Full example:** [templates/output-logging/itestoutputhelper.cs](../templates/output-logging/itestoutputhelper.cs)

## Structured output

Long unstructured `WriteLine` calls become a wall of text on failure. Use a small helper or a base class to add structure:

```csharp
private void LogSection(string title) => _output.WriteLine($"\n=== {title} ===");
private void LogKeyValue(string key, object value) => _output.WriteLine($"{key}: {value}");
private void LogTimestamp(DateTime time) => _output.WriteLine($"Timestamp: {time:yyyy-MM-dd HH:mm:ss.fff}");
```

Used in a test:

```csharp
[Fact]
public void DiscountForVip_ShouldBeTenPercent()
{
    LogSection("Setup");
    var customer = new { Name = "Jane", Level = "VIP" };
    LogKeyValue("Customer", customer.Name);
    LogKeyValue("Level", customer.Level);

    LogSection("Action");
    var discount = CalculateDiscount(customer.Level);
    LogKeyValue("Discount %", discount);

    LogSection("Verify");
    discount.Should().Be(10);
}
```

A reusable base class for diagnostic-heavy tests:

```csharp
public abstract class DiagnosticTestBase
{
    protected readonly ITestOutputHelper Output;
    protected DiagnosticTestBase(ITestOutputHelper output) => Output = output;

    protected void LogTestStart(string testName)
    {
        Output.WriteLine($"\n=== {testName} ===");
        Output.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
    }

    protected void LogAssertionFailure(string field, object expected, object actual)
    {
        Output.WriteLine("\n=== Assertion failure ===");
        Output.WriteLine($"Field: {field}");
        Output.WriteLine($"Expected: {expected}");
        Output.WriteLine($"Actual: {actual}");
    }
}
```

When to write output:

- **At test start** — inputs and setup.
- **Mid-test** — only at state transitions you'd want to see on failure.
- **Before an assertion** — `expected` vs `actual` for non-trivial values.
- **At test end** — duration, summary.

Do not narrate every line. Output is for *failures*; if the test passes, no one reads it.

## Diagnosing flaky tests

When a test fails only sometimes, output is the first tool. Record checkpoints with timing data so you can see which step varied:

```csharp
[Fact]
public async Task ProcessLargeDataSet_PerformanceTest()
{
    var stopwatch = Stopwatch.StartNew();
    _output.WriteLine("Starting...");

    await processor.LoadData(dataSet);
    _output.WriteLine($"Load complete: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

    await processor.ProcessData();
    _output.WriteLine($"Processing complete: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
}
```

If the flake is timing-driven, the checkpoints reveal which stage drifted. Once you know — replace timing-based waits with `TimeProvider`/`TaskCompletionSource`, per [reference/datetime.md](datetime.md).

## Testing `ILogger` calls

`ILogger.LogError(...)` and friends are extension methods, so NSubstitute cannot intercept them directly. You have two options.

### Option A: assert on the underlying `Log<TState>`

```csharp
// Won't work — extension method.
logger.Received().LogError(Arg.Any<string>());

// Works — intercepts the real interface method.
logger.Received().Log(
    LogLevel.Error,
    Arg.Any<EventId>(),
    Arg.Is<object>(o => o.ToString()!.Contains("expected fragment")),
    Arg.Any<Exception>(),
    Arg.Any<Func<object, Exception, string>>());
```

Verbose and couples the test to the formatter signature. Prefer Option B for production tests.

### Option B: introduce an `AbstractLogger<T>` seam

Wrap the awkward extension surface behind a method you can mock cleanly:

```csharp
public abstract class AbstractLogger<T> : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) => null!;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Log(logLevel, exception, state?.ToString() ?? string.Empty);
    }

    public abstract void Log(LogLevel logLevel, Exception? ex, string information);
}
```

In tests:

```csharp
var logger = Substitute.For<AbstractLogger<PaymentService>>();
// ...
logger.Received(1).Log(
    LogLevel.Error,
    Arg.Any<Exception>(),
    Arg.Is<string>(msg => msg.Contains("payment failed")));
```

Either way: **assert on level + key fragment, not the full message string** — small wording changes shouldn't fail tests.

**Full example:** [templates/output-logging/ilogger-testing.cs](../templates/output-logging/ilogger-testing.cs)

## Routing live log output through the test runner

To see real log output for a SUT during a test (useful for end-to-end integration tests), pipe `ILogger` calls into `ITestOutputHelper` with an `XUnitLogger<T>`. Output appears in the runner alongside the test name. Skeleton:

```csharp
public class XUnitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _categoryName = typeof(T).Name;

    public XUnitLogger(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _testOutputHelper.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}");
    }
    // ...IsEnabled, BeginScope, NoOpDisposable
}
```

## Combining verification with diagnostic output

`CompositeLogger<T>` lets you both assert on a mock *and* write to the test runner in the same test. Wire-up:

```csharp
var mockLogger = Substitute.For<AbstractLogger<OrderService>>();
var xunitLogger = new XUnitLogger<OrderService>(_output);
var compositeLogger = new CompositeLogger<OrderService>(mockLogger, xunitLogger);
var service = new OrderService(compositeLogger);

service.ProcessOrder("ORD001", 1500);

mockLogger.Received().Log(
    LogLevel.Information,
    Arg.Any<Exception>(),
    Arg.Is<string>(msg => msg.Contains("processing order")));
```

**Full example (XUnitLogger + CompositeLogger + TestLogger):** [templates/output-logging/diagnostic-tools.cs](../templates/output-logging/diagnostic-tools.cs)

## `TestLogger<T>` — collect entries for later inspection

When you want to inspect *all* the entries a SUT emitted (count by level, sequence ordering, etc.) without committing to specific messages, capture them in a list. Skeleton:

```csharp
public class TestLogger<T> : ILogger<T>
{
    private readonly ConcurrentBag<LogEntry> _logs = new();
    public IReadOnlyCollection<LogEntry> Logs => _logs.ToList();

    public bool HasLog(LogLevel level, string messageContains) =>
        _logs.Any(l => l.LogLevel == level &&
                       l.Message?.Contains(messageContains, StringComparison.OrdinalIgnoreCase) == true);
    // ...Log<TState>, IsEnabled, BeginScope
}
```

Use this when the verification *is* counts / level distribution, not text. Even here, keep `messageContains` to short fragments.

## Pitfalls

- **Output volume.** Heavy logging slows the suite and buries the useful lines. Add output where failure is plausible; skip it for trivial tests.
- **Sensitive data.** Never write tokens, passwords, connection strings, or PII into `ITestOutputHelper`. CI logs are searchable.
- **Async lifecycle.** `_output.WriteLine` from an async continuation that completes after the test method returned will throw. Await everything before the test exits.
- **Asserting on full log strings.** Brittle. Assert on level + a short fragment, or skip log verification entirely.
- **Over-specified call counts.** `Received(3)` couples to the implementation. Prefer `Received()` (at-least-once) unless the count itself is the contract.

## Checklist

- [ ] `ITestOutputHelper` injected via constructor; never static.
- [ ] Output uses section headers / timestamps; failures are readable on their own.
- [ ] `ILogger` assertions use an `AbstractLogger` seam or `Log<TState>`, not the extension methods.
- [ ] Log assertions check level + fragment, not full strings.
- [ ] `XUnitLogger` / `CompositeLogger` used when you want both diagnostics and verification in one test.
- [ ] No secrets or PII in output.
- [ ] Async tests await all logging before returning.

## Cross-links

- Output naming + AwesomeAssertions integration — [reference/awesome-assertions.md](awesome-assertions.md)
- Mocks/stubs (`Substitute.For`, `Received`) — [reference/nsubstitute.md](nsubstitute.md)
- Replacing flaky `Task.Delay` waits — [reference/datetime.md](datetime.md)
- ASP.NET integration tests with the same logger seams — [reference/aspnet-integration.md](aspnet-integration.md)
