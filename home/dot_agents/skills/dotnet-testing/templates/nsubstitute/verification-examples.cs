// NSubstitute verification pattern examples.
// Covers call verification, argument matching, capture, and ordering.

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NSubstituteVerificationExamples;

// ==================== Test data models ====================

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public OrderStatus Status { get; set; }
}

public enum OrderStatus { Pending, Processing, Completed, Cancelled }

// ==================== Dependency interfaces ====================

public interface IEmailService
{
    void SendEmail(string to, string subject, string body);
    void SendWelcomeEmail(string email, string name);
    Task SendEmailAsync(string to, string subject, string body);
    bool SendNotification(string email, string message);
}

public interface IUserRepository
{
    User? GetById(int id);
    Task<User?> GetByIdAsync(int id);
    void Save(User user);
    Task SaveAsync(User user);
    void Update(User user);
    void Delete(int id);
}

public interface IOrderRepository
{
    Order? GetById(int id);
    void Save(Order order);
    void UpdateStatus(int orderId, OrderStatus status);
}

public interface IAuditLog
{
    void LogAction(string action, string details);
    void LogCreate(string entity, int id);
    void LogUpdate(string entity, int id);
    void LogDelete(string entity, int id);
}

// ==================== Production classes ====================

public class UserService
{
    private readonly IUserRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;
    private readonly IAuditLog? _auditLog;

    public UserService(IUserRepository repository, IEmailService emailService, ILogger<UserService> logger)
    {
        _repository = repository;
        _emailService = emailService;
        _logger = logger;
    }

    public UserService(IUserRepository repository, IEmailService emailService,
                       ILogger<UserService> logger, IAuditLog auditLog)
        : this(repository, emailService, logger)
    {
        _auditLog = auditLog;
    }

    public void RegisterUser(User user)
    {
        _repository.Save(user);
        _emailService.SendWelcomeEmail(user.Email, user.Name);
        _logger.LogInformation("User registered: {Email}", user.Email);
    }

    public void RegisterMultipleUsers(IEnumerable<User> users)
    {
        foreach (var user in users)
        {
            _repository.Save(user);
            _emailService.SendWelcomeEmail(user.Email, user.Name);
        }
    }

    public async Task RegisterUserAsync(User user)
    {
        await _repository.SaveAsync(user);
        await _emailService.SendEmailAsync(user.Email, "Welcome", $"Hello {user.Name}!");
        _logger.LogInformation("User registered asynchronously: {Email}", user.Email);
    }

    public void UpdateUser(User user)
    {
        _repository.Update(user);
        _auditLog?.LogUpdate("User", user.Id);
    }

    public void DeleteUser(int userId)
    {
        _repository.Delete(userId);
        _auditLog?.LogDelete("User", userId);
        _logger.LogWarning("User deleted: {UserId}", userId);
    }

    public void ActivateUser(int userId)
    {
        var user = _repository.GetById(userId);
        if (user == null)
        {
            _logger.LogError("User not found: {UserId}", userId);
            return;
        }

        user.IsActive = true;
        _repository.Update(user);
        _emailService.SendEmail(user.Email, "Account Activated", "Your account is now active");
        _logger.LogInformation("User activated: {UserId}", userId);
    }
}

public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IAuditLog _auditLog;
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository repository, IAuditLog auditLog,
                        IEmailService emailService, ILogger<OrderService> logger)
    {
        _repository = repository;
        _auditLog = auditLog;
        _emailService = emailService;
        _logger = logger;
    }

    public void ProcessOrder(int orderId)
    {
        _auditLog.LogAction("ProcessOrder", $"Starting order {orderId}");
        _repository.UpdateStatus(orderId, OrderStatus.Processing);
        _repository.UpdateStatus(orderId, OrderStatus.Completed);
        _auditLog.LogAction("ProcessOrder", $"Completed order {orderId}");
    }

    public void CancelOrder(int orderId)
    {
        _repository.UpdateStatus(orderId, OrderStatus.Cancelled);
        _logger.LogWarning("Order cancelled: {OrderId}", orderId);
    }
}

// ==================== Verification examples ====================

public class VerificationExamplesTests
{
    // ==================== Basic Received() verification ====================

    [Fact]
    public void Received_RegisterUser_ShouldCallSave()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        sut.RegisterUser(user);

        repository.Received().Save(user);
    }

    [Fact]
    public void Received_RegisterUser_ShouldSendWelcomeEmail()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        sut.RegisterUser(user);

        emailService.Received().SendWelcomeEmail("john@example.com", "John");
    }

    // ==================== Call-count verification ====================

    [Fact]
    public void ReceivedCount_RegisterMultipleUsers_ShouldCallSaveThreeTimes()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var users = new[]
        {
            new User { Id = 1, Name = "User1", Email = "user1@example.com" },
            new User { Id = 2, Name = "User2", Email = "user2@example.com" },
            new User { Id = 3, Name = "User3", Email = "user3@example.com" }
        };

        sut.RegisterMultipleUsers(users);

        repository.Received(3).Save(Arg.Any<User>());
        emailService.Received(3).SendWelcomeEmail(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void ReceivedCount_RegisterUser_ShouldCallSaveOnce()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        sut.RegisterUser(user);

        repository.Received(1).Save(user);
    }

    // ==================== DidNotReceive verification ====================

    [Fact]
    public void DidNotReceive_ActivateUser_WhenUserMissing_ShouldNotSendEmail()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.GetById(999).Returns((User?)null);

        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        sut.ActivateUser(999);

        emailService.DidNotReceive().SendEmail(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void DidNotReceive_ActivateUser_WhenUserMissing_ShouldNotCallUpdate()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.GetById(999).Returns((User?)null);

        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        sut.ActivateUser(999);

        repository.DidNotReceive().Update(Arg.Any<User>());
    }

    // ==================== Argument matchers — Arg.Any ====================

    [Fact]
    public void ArgAny_RegisterUser_AcceptsAnyUserInstance()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user1 = new User { Id = 1, Name = "John", Email = "john@example.com" };
        var user2 = new User { Id = 2, Name = "Jane", Email = "jane@example.com" };

        sut.RegisterUser(user1);
        sut.RegisterUser(user2);

        repository.Received(2).Save(Arg.Any<User>());
    }

    // ==================== Argument matchers — Arg.Is ====================

    [Fact]
    public void ArgIs_RegisterUser_ShouldSaveOnlyActiveUsers()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var activeUser = new User { Id = 1, Name = "John", Email = "john@example.com", IsActive = true };

        sut.RegisterUser(activeUser);

        repository.Received().Save(Arg.Is<User>(u => u.IsActive));
    }

    [Fact]
    public void ArgIs_RegisterUser_ShouldSaveUserWithValidEmail()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        sut.RegisterUser(user);

        repository.Received().Save(Arg.Is<User>(u => u.Email.Contains("@")));
    }

    [Fact]
    public void ArgIs_RegisterUser_ShouldSaveUserWithPositiveId()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        sut.RegisterUser(user);

        repository.Received().Save(Arg.Is<User>(u => u.Id > 0));
    }

    // ==================== Argument capture — Arg.Do ====================

    [Fact]
    public void ArgDo_RegisterUser_ShouldCaptureSavedUser()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        User? capturedUser = null;
        repository.Save(Arg.Do<User>(u => capturedUser = u));

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        sut.RegisterUser(user);

        Assert.NotNull(capturedUser);
        Assert.Equal("John", capturedUser.Name);
        Assert.Equal("john@example.com", capturedUser.Email);
    }

    [Fact]
    public void ArgDo_RegisterMultipleUsers_ShouldCaptureEveryCall()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var capturedUsers = new List<User>();
        repository.Save(Arg.Do<User>(u => capturedUsers.Add(u)));

        var users = new[]
        {
            new User { Id = 1, Name = "User1", Email = "user1@example.com" },
            new User { Id = 2, Name = "User2", Email = "user2@example.com" }
        };

        sut.RegisterMultipleUsers(users);

        Assert.Equal(2, capturedUsers.Count);
        Assert.Contains(capturedUsers, u => u.Name == "User1");
        Assert.Contains(capturedUsers, u => u.Name == "User2");
    }

    // ==================== Ordered verification ====================

    [Fact]
    public void ReceivedInOrder_ProcessOrder_ShouldUpdateStatusInSequence()
    {
        var repository = Substitute.For<IOrderRepository>();
        var auditLog = Substitute.For<IAuditLog>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<OrderService>>();
        var sut = new OrderService(repository, auditLog, emailService, logger);

        sut.ProcessOrder(123);

        Received.InOrder(() =>
        {
            auditLog.LogAction("ProcessOrder", "Starting order 123");
            repository.UpdateStatus(123, OrderStatus.Processing);
            repository.UpdateStatus(123, OrderStatus.Completed);
            auditLog.LogAction("ProcessOrder", "Completed order 123");
        });
    }

    [Fact]
    public void ReceivedInOrder_ActivateUser_ShouldQueryUpdateThenEmail()
    {
        var repository = Substitute.For<IUserRepository>();
        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };
        repository.GetById(1).Returns(user);

        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        sut.ActivateUser(1);

        Received.InOrder(() =>
        {
            repository.GetById(1);
            repository.Update(user);
            emailService.SendEmail(user.Email, "Account Activated", "Your account is now active");
        });
    }

    // ==================== Async verification ====================

    [Fact]
    public async Task Async_RegisterUserAsync_ShouldCallSaveAsync()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        await sut.RegisterUserAsync(user);

        await repository.Received(1).SaveAsync(user);
    }

    [Fact]
    public async Task Async_RegisterUserAsync_ShouldSendAsyncEmail()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        await sut.RegisterUserAsync(user);

        await emailService.Received(1).SendEmailAsync(
            "john@example.com",
            "Welcome",
            Arg.Is<string>(body => body.Contains("John")));
    }

    // ==================== ReceivedWithAnyArgs ====================

    [Fact]
    public void ReceivedWithAnyArgs_RegisterUser_OnlyCheckThatSendingHappened()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        sut.RegisterUser(user);

        emailService.ReceivedWithAnyArgs(1).SendWelcomeEmail(default!, default!);
    }

    // ==================== Multi-condition matcher ====================

    [Fact]
    public void ComplexObjectMatching_RegisterUser_ShouldSatisfyAllConditions()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User
        {
            Id = 1,
            Name = "John Doe",
            Email = "john@example.com",
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        sut.RegisterUser(user);

        repository.Received().Save(Arg.Is<User>(u =>
            u.Id > 0 &&
            u.Name.Length > 0 &&
            u.Email.Contains("@") &&
            u.IsActive));
    }

    // ==================== ILogger verification ====================

    [Fact]
    public void Logger_RegisterUser_ShouldLogInformation()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        sut.RegisterUser(user);

        logger.Received(1).LogInformation("User registered: {Email}", "john@example.com");
    }

    [Fact]
    public void Logger_DeleteUser_ShouldLogWarning()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var auditLog = Substitute.For<IAuditLog>();
        var sut = new UserService(repository, emailService, logger, auditLog);

        sut.DeleteUser(123);

        logger.Received(1).LogWarning("User deleted: {UserId}", 123);
    }

    [Fact]
    public void Logger_ActivateUser_WhenUserMissing_ShouldLogError()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.GetById(999).Returns((User?)null);

        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        sut.ActivateUser(999);

        logger.Received(1).LogError("User not found: {UserId}", 999);
    }

    // ==================== Argument capture with When...Do ====================

    [Fact]
    public void ArgCapture_UpdateUser_ShouldCaptureStateAtCallTime()
    {
        var repository = Substitute.For<IUserRepository>();
        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var auditLog = Substitute.For<IAuditLog>();
        var sut = new UserService(repository, emailService, logger, auditLog);

        User? capturedUser = null;
        repository.When(x => x.Update(Arg.Any<User>()))
                  .Do(x => capturedUser = x.Arg<User>());

        var user = new User
        {
            Id = 1,
            Name = "John",
            Email = "john@example.com",
            IsActive = false
        };
        user.IsActive = true;

        sut.UpdateUser(user);

        Assert.NotNull(capturedUser);
        Assert.True(capturedUser.IsActive);
        Assert.Equal("John", capturedUser.Name);
    }

    // ==================== Multi-argument verification ====================

    [Fact]
    public void MultipleArgs_ActivateUser_ShouldUseCorrectSubjectAndBody()
    {
        var repository = Substitute.For<IUserRepository>();
        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };
        repository.GetById(1).Returns(user);

        var emailService = Substitute.For<IEmailService>();
        var logger = Substitute.For<ILogger<UserService>>();
        var sut = new UserService(repository, emailService, logger);

        sut.ActivateUser(1);

        emailService.Received().SendEmail(
            Arg.Is<string>(email   => email == "john@example.com"),
            Arg.Is<string>(subject => subject == "Account Activated"),
            Arg.Is<string>(body    => body.Contains("active")));
    }
}
