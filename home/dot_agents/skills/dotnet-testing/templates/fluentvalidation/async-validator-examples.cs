// Async-validator testing template.
// Shows how to test validators that depend on external services via `MustAsync`.

using FluentValidation;
using FluentValidation.TestHelper;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FluentValidationAsyncExample;

// ==================== External service interface ====================

public interface IUserService
{
    Task<bool> IsUsernameAvailableAsync(string username);
    Task<bool> IsEmailRegisteredAsync(string email);
}

// ==================== Test model ====================

public class UserRegistrationRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// ==================== Async validator under test ====================

public class UserRegistrationAsyncValidator : AbstractValidator<UserRegistrationRequest>
{
    private readonly IUserService _userService;

    public UserRegistrationAsyncValidator(IUserService userService)
    {
        _userService = userService;
        SetupValidationRules();
    }

    private void SetupValidationRules()
    {
        // Username basic checks.
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username must not be null or empty")
            .Length(3, 20).WithMessage("Username length must be between 3 and 20");

        // Username uniqueness (async). Gated so the basic rule has a chance first.
        RuleFor(x => x.Username)
            .MustAsync(async (username, _) =>
                await _userService.IsUsernameAvailableAsync(username))
            .WithMessage("Username is already taken")
            .When(x => !string.IsNullOrWhiteSpace(x.Username));

        // Email basic checks.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email must not be null or empty")
            .EmailAddress().WithMessage("Email is not in a valid format");

        // Email uniqueness (async). Gated by basic validity.
        RuleFor(x => x.Email)
            .MustAsync(async (email, _) =>
                !await _userService.IsEmailRegisteredAsync(email))
            .WithMessage("This email is already registered")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

// ==================== Tests ====================

public class UserRegistrationAsyncValidatorTests
{
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly UserRegistrationAsyncValidator _sut;

    public UserRegistrationAsyncValidatorTests() =>
        _sut = new UserRegistrationAsyncValidator(_users);

    // ==================== Username availability ====================

    [Fact]
    public async Task ValidateAsync_UsernameAvailable_ShouldPass()
    {
        var request = new UserRegistrationRequest
        {
            Username = "newuser123",
            Email = "new@example.com",
            Password = "Password123"
        };

        _users.IsUsernameAvailableAsync("newuser123").Returns(Task.FromResult(true));
        _users.IsEmailRegisteredAsync("new@example.com").Returns(Task.FromResult(false));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Username);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);

        await _users.Received(1).IsUsernameAvailableAsync("newuser123");
        await _users.Received(1).IsEmailRegisteredAsync("new@example.com");
    }

    [Fact]
    public async Task ValidateAsync_UsernameTaken_ShouldFail()
    {
        var request = new UserRegistrationRequest
        {
            Username = "existinguser",
            Email = "new@example.com",
            Password = "Password123"
        };

        _users.IsUsernameAvailableAsync("existinguser").Returns(Task.FromResult(false));
        _users.IsEmailRegisteredAsync("new@example.com").Returns(Task.FromResult(false));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username is already taken");

        await _users.Received(1).IsUsernameAvailableAsync("existinguser");
    }

    [Fact]
    public async Task ValidateAsync_EmptyUsername_ShouldSkipAsyncCheck()
    {
        var request = new UserRegistrationRequest
        {
            Username = "",
            Email = "test@example.com",
            Password = "Password123"
        };

        _users.IsUsernameAvailableAsync(Arg.Any<string>()).Returns(Task.FromResult(true));
        _users.IsEmailRegisteredAsync("test@example.com").Returns(Task.FromResult(false));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username must not be null or empty");

        // Basic rule already failed — async lookup must not fire.
        await _users.DidNotReceive().IsUsernameAvailableAsync(Arg.Any<string>());
    }

    // ==================== Email availability ====================

    [Fact]
    public async Task ValidateAsync_EmailNotRegistered_ShouldPass()
    {
        var request = new UserRegistrationRequest
        {
            Username = "testuser",
            Email = "available@example.com",
            Password = "Password123"
        };

        _users.IsUsernameAvailableAsync("testuser").Returns(Task.FromResult(true));
        _users.IsEmailRegisteredAsync("available@example.com").Returns(Task.FromResult(false));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
        await _users.Received(1).IsEmailRegisteredAsync("available@example.com");
    }

    [Fact]
    public async Task ValidateAsync_EmailAlreadyRegistered_ShouldFail()
    {
        var request = new UserRegistrationRequest
        {
            Username = "testuser",
            Email = "existing@example.com",
            Password = "Password123"
        };

        _users.IsUsernameAvailableAsync("testuser").Returns(Task.FromResult(true));
        _users.IsEmailRegisteredAsync("existing@example.com").Returns(Task.FromResult(true));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("This email is already registered");

        await _users.Received(1).IsEmailRegisteredAsync("existing@example.com");
    }

    // ==================== Exception propagation ====================

    [Fact]
    public async Task ValidateAsync_ServiceThrows_ShouldPropagate()
    {
        var request = new UserRegistrationRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "Password123"
        };

        _users.IsUsernameAvailableAsync("testuser")
              .Throws(new TimeoutException("Database connection timed out"));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await _sut.TestValidateAsync(request));
    }

    [Fact]
    public async Task ValidateAsync_ServiceTemporarilyUnavailable_ShouldPropagate()
    {
        var request = new UserRegistrationRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "Password123"
        };

        _users.IsUsernameAvailableAsync("testuser")
              .Returns(Task.FromException<bool>(new InvalidOperationException("Service unavailable")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _sut.TestValidateAsync(request));

        await _users.Received(1).IsUsernameAvailableAsync("testuser");
    }

    // ==================== CancellationToken propagation ====================

    [Fact]
    public async Task ValidateAsync_WithCancellationToken_ShouldFlowThrough()
    {
        var request = new UserRegistrationRequest
        {
            Username = "testuser",
            Email = "test@example.com",
            Password = "Password123"
        };

        var cts = new CancellationTokenSource();

        _users.IsUsernameAvailableAsync("testuser").Returns(Task.FromResult(true));
        _users.IsEmailRegisteredAsync("test@example.com").Returns(Task.FromResult(false));

        var result = await _sut.TestValidateAsync(request, strategy =>
        {
            strategy.IncludeAllRuleSets();
        }, cts.Token);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ==================== Integration check ====================

    [Fact]
    public async Task ValidateAsync_BothUsernameAndEmailTaken_ShouldReportBoth()
    {
        var request = new UserRegistrationRequest
        {
            Username = "existinguser",
            Email = "existing@example.com",
            Password = "Password123"
        };

        _users.IsUsernameAvailableAsync("existinguser").Returns(Task.FromResult(false));
        _users.IsEmailRegisteredAsync("existing@example.com").Returns(Task.FromResult(true));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username is already taken");

        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("This email is already registered");

        await _users.Received(1).IsUsernameAvailableAsync("existinguser");
        await _users.Received(1).IsEmailRegisteredAsync("existing@example.com");
    }
}

// ==================== Conditional / multi-dependency async validator ====================

public class OrderValidator : AbstractValidator<OrderRequest>
{
    private readonly IInventoryService _inventory;
    private readonly IPaymentService _payments;

    public OrderValidator(IInventoryService inventory, IPaymentService payments)
    {
        _inventory = inventory;
        _payments = payments;
        SetupValidationRules();
    }

    private void SetupValidationRules()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("ProductId is required");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be > 0");

        // Stock check gated by basic rules.
        RuleFor(x => x)
            .MustAsync(async (order, _) =>
                await _inventory.IsStockAvailableAsync(order.ProductId, order.Quantity))
            .WithMessage("Insufficient stock")
            .When(x => !string.IsNullOrEmpty(x.ProductId) && x.Quantity > 0);

        // Payment-method validity gated by presence of a method.
        RuleFor(x => x.PaymentMethod)
            .MustAsync(async (order, method, _) =>
                await _payments.IsPaymentMethodValidAsync(method, order.Amount))
            .WithMessage("Payment method is not valid for this amount")
            .When(x => !string.IsNullOrEmpty(x.PaymentMethod));
    }
}

public class OrderRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public interface IInventoryService
{
    Task<bool> IsStockAvailableAsync(string productId, int quantity);
}

public interface IPaymentService
{
    Task<bool> IsPaymentMethodValidAsync(string paymentMethod, decimal amount);
}

// ==================== Order validator tests ====================

public class OrderValidatorTests
{
    private readonly IInventoryService _inventory = Substitute.For<IInventoryService>();
    private readonly IPaymentService _payments = Substitute.For<IPaymentService>();
    private readonly OrderValidator _sut;

    public OrderValidatorTests() => _sut = new OrderValidator(_inventory, _payments);

    [Fact]
    public async Task ValidateAsync_StockAvailableAndPaymentValid_ShouldPass()
    {
        var order = new OrderRequest
        {
            ProductId = "PROD001",
            Quantity = 5,
            PaymentMethod = "CreditCard",
            Amount = 1000m
        };

        _inventory.IsStockAvailableAsync("PROD001", 5).Returns(Task.FromResult(true));
        _payments.IsPaymentMethodValidAsync("CreditCard", 1000m).Returns(Task.FromResult(true));

        var result = await _sut.TestValidateAsync(order);

        result.ShouldNotHaveAnyValidationErrors();
        await _inventory.Received(1).IsStockAvailableAsync("PROD001", 5);
        await _payments.Received(1).IsPaymentMethodValidAsync("CreditCard", 1000m);
    }

    [Fact]
    public async Task ValidateAsync_StockUnavailable_ShouldFail()
    {
        var order = new OrderRequest
        {
            ProductId = "PROD001",
            Quantity = 100,
            PaymentMethod = "CreditCard",
            Amount = 10_000m
        };

        _inventory.IsStockAvailableAsync("PROD001", 100).Returns(Task.FromResult(false));
        _payments.IsPaymentMethodValidAsync("CreditCard", 10_000m).Returns(Task.FromResult(true));

        var result = await _sut.TestValidateAsync(order);

        result.ShouldHaveValidationErrorFor(x => x)
              .WithErrorMessage("Insufficient stock");
    }

    [Fact]
    public async Task ValidateAsync_EmptyProductId_ShouldSkipInventoryCheck()
    {
        var order = new OrderRequest
        {
            ProductId = "",
            Quantity = 5,
            PaymentMethod = "CreditCard",
            Amount = 1000m
        };

        _inventory.IsStockAvailableAsync(Arg.Any<string>(), Arg.Any<int>())
                  .Returns(Task.FromResult(true));
        _payments.IsPaymentMethodValidAsync("CreditCard", 1000m).Returns(Task.FromResult(true));

        var result = await _sut.TestValidateAsync(order);

        result.ShouldHaveValidationErrorFor(x => x.ProductId);

        // Gated by When — never fires.
        await _inventory.DidNotReceive().IsStockAvailableAsync(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ValidateAsync_PaymentMethodInvalid_ShouldFail()
    {
        var order = new OrderRequest
        {
            ProductId = "PROD001",
            Quantity = 1,
            PaymentMethod = "Cash",
            Amount = 100_000m
        };

        _inventory.IsStockAvailableAsync("PROD001", 1).Returns(Task.FromResult(true));
        _payments.IsPaymentMethodValidAsync("Cash", 100_000m).Returns(Task.FromResult(false));

        var result = await _sut.TestValidateAsync(order);

        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod)
              .WithErrorMessage("Payment method is not valid for this amount");
    }

    [Fact]
    public async Task ValidateAsync_ParallelAsyncRules_ShouldRunInParallel()
    {
        var order = new OrderRequest
        {
            ProductId = "PROD001",
            Quantity = 5,
            PaymentMethod = "CreditCard",
            Amount = 1000m
        };

        _inventory.IsStockAvailableAsync("PROD001", 5)
                  .Returns(async _ =>
                  {
                      await Task.Delay(100);
                      return true;
                  });
        _payments.IsPaymentMethodValidAsync("CreditCard", 1000m)
                 .Returns(async _ =>
                 {
                     await Task.Delay(100);
                     return true;
                 });

        var startTime = DateTime.UtcNow;
        var result = await _sut.TestValidateAsync(order);
        var elapsed = DateTime.UtcNow - startTime;

        result.ShouldNotHaveAnyValidationErrors();

        // If the async rules ran sequentially, elapsed would be ~200ms.
        Assert.True(elapsed.TotalMilliseconds < 300,
            $"Validation took {elapsed.TotalMilliseconds}ms — async rules may not be parallel.");
    }
}
