// NSubstitute Mock/Stub/Spy pattern examples.
// Illustrates the five Test Double roles and common substitution patterns.

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NSubstituteMockingExamples;

// ==================== Test data models ====================

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public OrderStatus Status { get; set; }
}

public enum CustomerType { Regular, Premium, VIP }
public enum OrderStatus { Pending, Processing, Completed, Cancelled }
public enum PaymentResult { Success, Failed, Pending }

// ==================== Dependency interfaces ====================

public interface IUserRepository
{
    User? GetById(int id);
    Task<User?> GetByIdAsync(int id);
    void Save(User user);
    Task SaveAsync(User user);
    void Delete(int id);
    IEnumerable<User> GetAll();
}

public interface IEmailService
{
    void SendEmail(string to, string subject, string body, ILogger logger);
    void SendWelcomeEmail(string email, string name);
    void SendConfirmation(string email);
    bool SendNotification(string email, string message);
}

public interface ICustomerService
{
    CustomerType GetCustomerType(int customerId);
}

public interface IPaymentGateway
{
    PaymentResult ProcessPayment(decimal amount);
}

public interface IOrderRepository
{
    Order? GetById(int id);
    void Save(Order order);
}

// ==================== Production classes ====================

public class UserService
{
    private readonly IUserRepository _repository;
    private readonly IEmailService? _emailService;
    private readonly ILogger<UserService>? _logger;

    public UserService(IUserRepository repository) => _repository = repository;

    public UserService(IUserRepository repository, IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }

    public UserService(IUserRepository repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public User? GetUser(int id) => _repository.GetById(id);
    public async Task<User?> GetUserAsync(int id) => await _repository.GetByIdAsync(id);

    public void CreateUser(User user)
    {
        _repository.Save(user);
        _logger?.LogInformation("User created: {Name}", user.Name);
    }

    public void RegisterUser(string email, string name) =>
        _emailService?.SendWelcomeEmail(email, name);

    public async Task SaveUserAsync(User user) => await _repository.SaveAsync(user);
}

public class OrderService
{
    private readonly IOrderRepository? _repository;
    private readonly IEmailService? _emailService;
    private readonly ILogger<OrderService>? _logger;

    public OrderService() { }

    public OrderService(IOrderRepository repository, IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }

    public OrderService(IOrderRepository repository, IEmailService emailService, ILogger<OrderService> logger)
    {
        _repository = repository;
        _emailService = emailService;
        _logger = logger;
    }

    public OrderResult ProcessOrder(Order order, ILogger dummyLogger) =>
        new() { Success = true };

    public OrderResult ProcessOrder(int orderId)
    {
        var order = _repository?.GetById(orderId);
        if (order == null) return new OrderResult { Success = false };

        order.Status = OrderStatus.Completed;
        return new OrderResult { Success = true };
    }
}

public class OrderResult { public bool Success { get; set; } }

public class PricingService
{
    private readonly ICustomerService _customerService;

    public PricingService(ICustomerService customerService) =>
        _customerService = customerService;

    public decimal CalculateDiscount(int customerId, decimal amount) =>
        _customerService.GetCustomerType(customerId) switch
        {
            CustomerType.Premium => amount * 0.2m,
            CustomerType.VIP     => amount * 0.3m,
            _                    => 0
        };
}

public class PaymentService
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IPaymentGateway paymentGateway, ILogger<PaymentService> logger)
    {
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    public void ProcessPayment(decimal amount)
    {
        var result = _paymentGateway.ProcessPayment(amount);
        _logger.LogInformation("Payment processed: {Amount} - Result: {Result}", amount, result);
    }
}

// ==================== Pattern tests ====================

public class NSubstituteMockPatternsTests
{
    // ==================== Pattern 1: Dummy — parameter filler ====================

    [Fact]
    public void Dummy_ProcessOrder_WhenLoggerNotUsed_ShouldSucceed()
    {
        var dummyLogger = Substitute.For<ILogger>();
        var sut = new OrderService();
        var order = new Order { Id = 1, ProductName = "Test" };

        var result = sut.ProcessOrder(order, dummyLogger);

        Assert.True(result.Success);
    }

    // ==================== Stub — canned return values ====================

    [Fact]
    public void Stub_GetUser_WithValidId_ShouldReturnUser()
    {
        var stubRepository = Substitute.For<IUserRepository>();
        stubRepository.GetById(123).Returns(new User
        {
            Id = 123,
            Name = "John Doe",
            Email = "john@example.com"
        });

        var sut = new UserService(stubRepository);

        var actual = sut.GetUser(123);

        Assert.NotNull(actual);
        Assert.Equal("John Doe", actual.Name);
        Assert.Equal("john@example.com", actual.Email);
    }

    [Fact]
    public void Stub_GetUser_WithAnyId_ShouldReturnDefault()
    {
        var stubRepository = Substitute.For<IUserRepository>();
        stubRepository.GetById(Arg.Any<int>()).Returns(new User
        {
            Id = 999,
            Name = "Default User"
        });

        var sut = new UserService(stubRepository);

        var result1 = sut.GetUser(1);
        var result2 = sut.GetUser(100);
        var result3 = sut.GetUser(999);

        Assert.Equal("Default User", result1?.Name);
        Assert.Equal("Default User", result2?.Name);
        Assert.Equal("Default User", result3?.Name);
    }

    [Fact]
    public void Stub_CalculateDiscount_PremiumCustomer_ShouldReturn20Percent()
    {
        var stubCustomerService = Substitute.For<ICustomerService>();
        stubCustomerService.GetCustomerType(123).Returns(CustomerType.Premium);

        var sut = new PricingService(stubCustomerService);

        var discount = sut.CalculateDiscount(123, 1000);

        Assert.Equal(200, discount);
    }

    [Fact]
    public void Stub_CalculateDiscount_VipCustomer_ShouldReturn30Percent()
    {
        var stubCustomerService = Substitute.For<ICustomerService>();
        stubCustomerService.GetCustomerType(456).Returns(CustomerType.VIP);

        var sut = new PricingService(stubCustomerService);

        var discount = sut.CalculateDiscount(456, 1000);

        Assert.Equal(300, discount);
    }

    // ==================== Fake — simplified real implementation ====================

    [Fact]
    public void Fake_CreateAndGetUser_ShouldRoundTrip()
    {
        var fakeRepository = new FakeUserRepository();
        var sut = new UserService(fakeRepository);

        var newUser = new User { Id = 1, Name = "John Doe", Email = "john@example.com" };

        fakeRepository.Save(newUser);
        var actual = sut.GetUser(1);

        Assert.NotNull(actual);
        Assert.Equal("John Doe", actual.Name);
        Assert.Equal("john@example.com", actual.Email);
    }

    [Fact]
    public void Fake_DeleteUser_ShouldRemove()
    {
        var fakeRepository = new FakeUserRepository();
        var sut = new UserService(fakeRepository);

        fakeRepository.Save(new User { Id = 1, Name = "John" });

        fakeRepository.Delete(1);
        var actual = sut.GetUser(1);

        Assert.Null(actual);
    }

    // ==================== Spy — record calls for later inspection ====================

    [Fact]
    public void Spy_CreateUser_ShouldLogCreationInfo()
    {
        var spyLogger = Substitute.For<ILogger<UserService>>();
        var repository = Substitute.For<IUserRepository>();
        var sut = new UserService(repository, spyLogger);

        sut.CreateUser(new User { Id = 1, Name = "John Doe" });

        spyLogger.Received(1).LogInformation("User created: {Name}", "John Doe");
    }

    // ==================== Mock — strict interaction verification ====================

    [Fact]
    public void Mock_RegisterUser_ShouldSendWelcomeEmail()
    {
        var mockEmailService = Substitute.For<IEmailService>();
        var repository = Substitute.For<IUserRepository>();
        var sut = new UserService(repository, mockEmailService);

        sut.RegisterUser("john@example.com", "John Doe");

        mockEmailService.Received(1).SendWelcomeEmail("john@example.com", "John Doe");
    }

    [Fact]
    public void Mock_RegisterUser_ShouldSendOnlyOnce()
    {
        var mockEmailService = Substitute.For<IEmailService>();
        var repository = Substitute.For<IUserRepository>();
        var sut = new UserService(repository, mockEmailService);

        sut.RegisterUser("john@example.com", "John");

        mockEmailService.Received(1).SendWelcomeEmail(Arg.Any<string>(), Arg.Any<string>());
    }

    // ==================== Async substitution ====================

    [Fact]
    public async Task Async_GetUserAsync_WhenUserExists_ShouldReturnUser()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.GetByIdAsync(123).Returns(Task.FromResult<User?>(
            new User { Id = 123, Name = "John Doe" }));

        var sut = new UserService(repository);

        var result = await sut.GetUserAsync(123);

        Assert.NotNull(result);
        Assert.Equal("John Doe", result.Name);
        await repository.Received(1).GetByIdAsync(123);
    }

    [Fact]
    public async Task Async_SaveUserAsync_WhenDatabaseFails_ShouldThrow()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.SaveAsync(Arg.Any<User>())
                  .Throws(new InvalidOperationException("Database connection failed"));

        var sut = new UserService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sut.SaveUserAsync(new User { Name = "John" }));
    }

    // ==================== Sequenced return values ====================

    [Fact]
    public void Returns_Sequence_GetAll_ShouldReturnDifferentBatches()
    {
        var repository = Substitute.For<IUserRepository>();

        repository.GetAll().Returns(
            new[] { new User { Id = 1, Name = "User1" } },
            new[] { new User { Id = 1, Name = "User1" }, new User { Id = 2, Name = "User2" } },
            new[] { new User { Id = 1, Name = "User1" }, new User { Id = 2, Name = "User2" }, new User { Id = 3, Name = "User3" } }
        );

        var result1 = repository.GetAll();
        var result2 = repository.GetAll();
        var result3 = repository.GetAll();

        Assert.Single(result1);
        Assert.Equal(2, result2.Count());
        Assert.Equal(3, result3.Count());
    }

    // ==================== Exception handling ====================

    [Fact]
    public void Throws_GetUser_WhenDatabaseConnectionFails_ShouldThrow()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.GetById(Arg.Any<int>())
                  .Throws(new InvalidOperationException("Database connection failed"));

        var sut = new UserService(repository);

        Assert.Throws<InvalidOperationException>(() => sut.GetUser(123));
    }

    // ==================== Conditional / computed return ====================

    [Fact]
    public void Returns_Conditional_GetById_ShouldDeriveValueFromArgs()
    {
        var repository = Substitute.For<IUserRepository>();

        // Even ID returns a Premium user; odd ID returns a Regular user.
        repository.GetById(Arg.Any<int>()).Returns(x =>
        {
            var id = (int)x[0];
            return new User
            {
                Id = id,
                Name = $"User{id}",
                CustomerType = id % 2 == 0 ? CustomerType.Premium : CustomerType.Regular
            };
        });

        var sut = new UserService(repository);

        var user1 = sut.GetUser(1);
        var user2 = sut.GetUser(2);
        var user3 = sut.GetUser(3);

        Assert.Equal(CustomerType.Regular, user1?.CustomerType);
        Assert.Equal(CustomerType.Premium, user2?.CustomerType);
        Assert.Equal(CustomerType.Regular, user3?.CustomerType);
    }

    // ==================== DidNotReceive verification ====================

    [Fact]
    public void DidNotReceive_ProcessOrder_WhenOrderMissing_ShouldNotSendEmail()
    {
        var mockEmailService = Substitute.For<IEmailService>();
        var repository = Substitute.For<IOrderRepository>();
        repository.GetById(Arg.Any<int>()).Returns((Order?)null);

        var sut = new OrderService(repository, mockEmailService);

        var result = sut.ProcessOrder(999);

        Assert.False(result.Success);
        mockEmailService.DidNotReceive().SendConfirmation(Arg.Any<string>());
    }
}

// ==================== Hand-rolled Fake implementation ====================

public class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<int, User> _users = new();

    public User? GetById(int id)
    {
        _users.TryGetValue(id, out var user);
        return user;
    }

    public Task<User?> GetByIdAsync(int id) => Task.FromResult(GetById(id));

    public void Save(User user) => _users[user.Id] = user;

    public Task SaveAsync(User user)
    {
        Save(user);
        return Task.CompletedTask;
    }

    public void Delete(int id) => _users.Remove(id);

    public IEnumerable<User> GetAll() => _users.Values;
}
