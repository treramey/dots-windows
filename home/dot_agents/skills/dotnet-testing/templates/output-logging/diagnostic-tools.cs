using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// XUnitLogger and CompositeLogger diagnostic tooling — combine behaviour
/// verification with live test output.
/// </summary>
public class DiagnosticToolsExample
{
    private readonly ITestOutputHelper _output;

    public DiagnosticToolsExample(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WithCompositeLogger_ShouldVerifyAndEmitOutput()
    {
        // Arrange — combine a mock logger with the xUnit logger
        var mockLogger = Substitute.For<AbstractLogger<OrderService>>();
        var xunitLogger = new XUnitLogger<OrderService>(_output);
        var compositeLogger = new CompositeLogger<OrderService>(mockLogger, xunitLogger);

        var service = new OrderService(compositeLogger);

        // Act
        service.ProcessOrder("ORD001", 1500);

        // Assert — verify against the mock; meanwhile the xUnit logger has
        // already emitted the real log timeline into the test output.
        mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<Exception>(),
            Arg.Is<string>(msg => msg.Contains("processing order"))
        );
    }
}

// ===== XUnitLogger =====

/// <summary>
/// Routes ILogger output into the xUnit test runner.
/// </summary>
public class XUnitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _categoryName;

    public XUnitLogger(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        _categoryName = typeof(T).Name;
    }

    public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (formatter == null)
        {
            return;
        }

        var message = formatter(state, exception);

        // Format: [time] [level] [category] message
        _testOutputHelper.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}"
        );

        if (exception != null)
        {
            _testOutputHelper.WriteLine($"Exception: {exception}");
        }
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}

// ===== CompositeLogger =====

/// <summary>
/// Composes multiple loggers — every record goes to every inner logger.
/// </summary>
public class CompositeLogger<T> : ILogger<T>
{
    private readonly ILogger<T>[] _loggers;

    public CompositeLogger(params ILogger<T>[] loggers)
    {
        _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        var disposables = _loggers.Select(logger => logger.BeginScope(state)).ToArray();
        return new CompositeDisposable(disposables);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _loggers.Any(logger => logger.IsEnabled(logLevel));
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        foreach (var logger in _loggers)
        {
            if (logger.IsEnabled(logLevel))
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

/// <summary>
/// Composes multiple IDisposable objects.
/// </summary>
public class CompositeDisposable : IDisposable
{
    private readonly IDisposable[] _disposables;

    public CompositeDisposable(params IDisposable[] disposables)
    {
        _disposables = disposables;
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
    }
}

// ===== TestLogger — for collecting log entries =====

/// <summary>
/// Test-only logger that captures all entries for later inspection.
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    private readonly ConcurrentBag<LogEntry> _logs = new ConcurrentBag<LogEntry>();

    public IReadOnlyCollection<LogEntry> Logs => _logs.ToList();

    public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        var message = formatter?.Invoke(state, exception) ?? state?.ToString();

        _logs.Add(new LogEntry
        {
            LogLevel = logLevel,
            Message = message,
            Exception = exception,
            Timestamp = DateTime.Now
        });
    }

    public bool HasLog(LogLevel level, string messageContains)
    {
        return _logs.Any(log =>
            log.LogLevel == level &&
            log.Message.Contains(messageContains, StringComparison.OrdinalIgnoreCase));
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}

/// <summary>
/// Captured log record.
/// </summary>
public class LogEntry
{
    public LogLevel LogLevel { get; set; }
    public string Message { get; set; }
    public Exception Exception { get; set; }
    public DateTime Timestamp { get; set; }
}

// ===== Service under test =====

public class OrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public void ProcessOrder(string orderId, decimal amount)
    {
        _logger.LogInformation($"processing order {orderId}, amount: ${amount}");

        // Simulated processing
        if (amount > 0)
        {
            _logger.LogInformation($"order {orderId} processed successfully");
        }
        else
        {
            _logger.LogError($"order {orderId} amount is invalid");
        }
    }
}

// ===== AbstractLogger (copied from the previous example) =====

public abstract class AbstractLogger<T> : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        Log(logLevel, exception, state?.ToString() ?? string.Empty);
    }

    public abstract void Log(LogLevel logLevel, Exception ex, string information);
}
