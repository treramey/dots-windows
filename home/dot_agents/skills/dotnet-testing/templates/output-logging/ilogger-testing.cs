using System;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// ILogger testing strategies — shows how to verify ILogger behaviour from a test.
/// </summary>
public class ILoggerTestingExample
{
    // ===== Strategy 1: use AbstractLogger to simplify =====

    [Fact]
    public void WithAbstractLogger_WhenPaymentFails_ShouldLogError()
    {
        // Arrange
        var logger = Substitute.For<AbstractLogger<PaymentService>>();
        var paymentGateway = Substitute.For<IPaymentGateway>();
        paymentGateway.ProcessPayment(Arg.Any<decimal>()).Returns(new PaymentResult
        {
            Success = false,
            ErrorMessage = "Insufficient funds"
        });

        var service = new PaymentService(logger, paymentGateway);

        // Act
        service.ProcessPayment(1000);

        // Assert
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<Exception>(),
            Arg.Is<string>(msg => msg.Contains("payment failed") && msg.Contains("Insufficient funds"))
        );
    }

    // ===== Strategy 2: intercept Log<TState> directly (verbose but complete) =====

    [Fact]
    public void WithStandardILogger_WhenPaymentSucceeds_ShouldLogInformation()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PaymentService>>();
        var paymentGateway = Substitute.For<IPaymentGateway>();
        paymentGateway.ProcessPayment(Arg.Any<decimal>()).Returns(new PaymentResult
        {
            Success = true
        });

        var service = new PaymentService(logger, paymentGateway);

        // Act
        service.ProcessPayment(1000);

        // Assert — intercept the underlying Log<TState> method
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains("payment succeeded")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>()
        );
    }

    // ===== Strategy 3: anti-pattern — mocking extension methods directly =====

    [Fact]
    public void WrongApproach_ThisWillNotWork()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PaymentService>>();
        var service = new PaymentService(logger, null);

        // Act
        // service.ProcessPayment(1000);

        // Assert — DO NOT do this: LogError is an extension method.
        // logger.Received().LogError(Arg.Any<string>()); // will not intercept anything
    }
}

// ===== AbstractLogger abstraction =====

/// <summary>
/// Simplified ILogger abstraction that's easy to mock.
/// </summary>
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

    // Simplified abstract method — easier to verify in tests.
    public abstract void Log(LogLevel logLevel, Exception ex, string information);
}

// ===== Service under test =====

public class PaymentService
{
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentGateway _paymentGateway;

    public PaymentService(ILogger<PaymentService> logger, IPaymentGateway paymentGateway)
    {
        _logger = logger;
        _paymentGateway = paymentGateway;
    }

    public void ProcessPayment(decimal amount)
    {
        _logger.LogInformation($"Starting payment processing, amount: ${amount}");

        var result = _paymentGateway.ProcessPayment(amount);

        if (result.Success)
        {
            _logger.LogInformation($"payment succeeded, amount: ${amount}");
        }
        else
        {
            _logger.LogError($"payment failed: {result.ErrorMessage}");
        }
    }
}

// ===== Dependency interfaces =====

public interface IPaymentGateway
{
    PaymentResult ProcessPayment(decimal amount);
}

public class PaymentResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
}
