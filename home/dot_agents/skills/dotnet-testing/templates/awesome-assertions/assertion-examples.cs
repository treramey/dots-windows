using AwesomeAssertions;
using Xunit;

namespace YourProject.Tests.Examples;

/// <summary>
/// Common AwesomeAssertions examples across objects, strings, numerics,
/// collections, exceptions, async, complex equivalence, and AssertionScope.
/// </summary>
public class AssertionExamples
{
    #region Object assertions

    [Fact]
    public void ObjectAssertions_BasicChecks()
    {
        var user = new User
        {
            Id = 1,
            Name = "John Doe",
            Email = "john@example.com"
        };

        // Null check
        user.Should().NotBeNull();

        // Type checks
        user.Should().BeOfType<User>();
        user.Should().BeAssignableTo<IUser>();

        // Structural equivalence
        var anotherUser = new User
        {
            Id = 1,
            Name = "John Doe",
            Email = "john@example.com"
        };
        user.Should().BeEquivalentTo(anotherUser);
    }

    [Fact]
    public void ObjectAssertions_PropertyChecks()
    {
        var product = new Product
        {
            Id = 1,
            Name = "Laptop",
            Price = 999.99m,
            Stock = 10
        };

        product.Id.Should().BeGreaterThan(0);
        product.Name.Should().NotBeNullOrEmpty();
        product.Price.Should().BePositive();

        // Anonymous-object equivalence — only the listed members are checked.
        product.Should().BeEquivalentTo(new
        {
            Id = 1,
            Name = "Laptop",
            Price = 999.99m
        });
    }

    #endregion

    #region String assertions

    [Fact]
    public void StringAssertions_ContentChecks()
    {
        var message = "Hello World";

        message.Should().NotBeNullOrEmpty();
        message.Should().NotBeNullOrWhiteSpace();

        message.Should().Contain("Hello");
        message.Should().StartWith("Hello");
        message.Should().EndWith("World");
        message.Should().ContainEquivalentOf("WORLD"); // case-insensitive

        message.Should().HaveLength(11);
        message.Should().HaveLengthGreaterThan(5);
    }

    [Fact]
    public void StringAssertions_PatternMatching()
    {
        var email = "user@example.com";

        email.Should().MatchRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");

        email.Should().Contain("@").And.Contain(".");

        email.Should().NotBeNullOrEmpty()
             .And.Contain("@")
             .And.MatchRegex(@"@[\w-]+\.");
    }

    #endregion

    #region Numeric assertions

    [Fact]
    public void NumericAssertions_RangeChecks()
    {
        var age = 25;

        age.Should().BeGreaterThan(18);
        age.Should().BeLessThan(65);
        age.Should().BeInRange(18, 65);

        age.Should().BeOneOf(25, 30, 35);
        age.Should().BePositive();
    }

    [Fact]
    public void NumericAssertions_FloatingPoint()
    {
        var pi = 3.14159;

        // Always use approximate comparison for floating point.
        pi.Should().BeApproximately(3.14, 0.01);

        double.NaN.Should().Be(double.NaN);
        double.PositiveInfinity.Should().BePositiveInfinity();
        double.NegativeInfinity.Should().BeNegativeInfinity();
    }

    #endregion

    #region Collection assertions

    [Fact]
    public void CollectionAssertions_BasicChecks()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };

        numbers.Should().NotBeEmpty();
        numbers.Should().HaveCount(5);
        numbers.Should().HaveCountGreaterThan(3);

        numbers.Should().Contain(3);
        numbers.Should().ContainSingle(x => x == 3);
        numbers.Should().NotContain(0);

        numbers.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void CollectionAssertions_ComplexObjects()
    {
        var users = new[]
        {
            new User { Id = 1, Name = "John", Age = 30 },
            new User { Id = 2, Name = "Jane", Age = 25 },
            new User { Id = 3, Name = "Bob",  Age = 35 }
        };

        users.Should().Contain(u => u.Name == "John");
        users.Should().OnlyContain(u => u.Age >= 18);

        users.Should().AllSatisfy(u =>
        {
            u.Id.Should().BeGreaterThan(0);
            u.Name.Should().NotBeNullOrEmpty();
            u.Age.Should().BePositive();
        });

        var ages = users.Select(u => u.Age).ToArray();
        ages.Should().BeInAscendingOrder();
    }

    #endregion

    #region Exception assertions

    [Fact]
    public void ExceptionAssertions_BasicThrow()
    {
        var calculator = new Calculator();

        Action act = () => calculator.Divide(10, 0);

        act.Should().Throw<DivideByZeroException>()
           .WithMessage("*cannot divide by zero*");
    }

    [Fact]
    public void ExceptionAssertions_ArgumentParameter()
    {
        var userService = new UserService();

        Action act = () => userService.GetUser(-1);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*User ID must be positive*")
           .And.ParamName.Should().Be("userId");
    }

    [Fact]
    public void ExceptionAssertions_NotThrow()
    {
        var calculator = new Calculator();

        Action act = () => calculator.Add(1, 2);
        act.Should().NotThrow();
    }

    #endregion

    #region Async assertions

    [Fact]
    public async Task AsyncAssertions_TaskCompletion()
    {
        var service = new AsyncService();

        var result = await service.GetDataAsync();

        result.Should().NotBeNull();
        result.Should().BeOfType<DataResult>();
    }

    [Fact]
    public async Task AsyncAssertions_ExecutionTime()
    {
        var service = new AsyncService();

        Func<Task> act = () => service.GetDataAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AsyncAssertions_Throws()
    {
        var service = new AsyncService();

        Func<Task> act = async () => await service.GetInvalidDataAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*data not found*");
    }

    #endregion

    #region Complex equivalence

    [Fact]
    public void ComplexObjectComparison_DeepEquivalence()
    {
        var order = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            Items = new[]
            {
                new OrderItem { ProductId = 1, Quantity = 2, Price = 10.5m },
                new OrderItem { ProductId = 2, Quantity = 1, Price = 25.0m }
            },
            TotalAmount = 46.0m
        };

        var expected = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            Items = new[]
            {
                new OrderItem { ProductId = 1, Quantity = 2, Price = 10.5m },
                new OrderItem { ProductId = 2, Quantity = 1, Price = 25.0m }
            },
            TotalAmount = 46.0m
        };

        order.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ComplexObjectComparison_ExcludingTimestamps()
    {
        var user = new User
        {
            Id = 1,
            Name = "John",
            Email = "john@example.com",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        var expected = new User
        {
            Id = 1,
            Name = "John",
            Email = "john@example.com",
            CreatedAt = DateTime.Now.AddDays(-1),
            UpdatedAt = DateTime.Now.AddHours(-2)
        };

        user.Should().BeEquivalentTo(expected, options => options
            .Excluding(u => u.CreatedAt)
            .Excluding(u => u.UpdatedAt));
    }

    [Fact]
    public void ComplexObjectComparison_PartialProperties()
    {
        var product = new Product
        {
            Id = 1,
            Name = "Laptop",
            Price = 999.99m,
            Stock = 10,
            CreatedAt = DateTime.Now
        };

        product.Should().BeEquivalentTo(new
        {
            Name  = "Laptop",
            Price = 999.99m
        });
    }

    #endregion

    #region AssertionScope

    [Fact]
    public void AssertionScope_BatchAllFailures()
    {
        var user = new User
        {
            Id = 0,
            Name = "",
            Email = "invalid-email"
        };

        using (new AssertionScope())
        {
            user.Id.Should().BeGreaterThan(0, "User ID must be positive");
            user.Name.Should().NotBeNullOrEmpty("User name is required");
            user.Email.Should().MatchRegex(@"@.*\.", "Email format is invalid");
        }
        // Every failure is reported together, instead of stopping on the first.
    }

    #endregion
}

#region Test model classes

public interface IUser
{
    int Id { get; set; }
    string Name { get; set; }
}

public class User : IUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public OrderItem[] Items { get; set; } = Array.Empty<OrderItem>();
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class DataResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}

#endregion

#region Service test classes

public class Calculator
{
    public int Add(int a, int b) => a + b;

    public int Divide(int a, int b)
    {
        if (b == 0)
            throw new DivideByZeroException("Cannot divide by zero");
        return a / b;
    }
}

public class UserService
{
    public User GetUser(int userId)
    {
        if (userId <= 0)
            throw new ArgumentException("User ID must be positive", nameof(userId));

        return new User { Id = userId, Name = "Test User" };
    }
}

public class AsyncService
{
    public async Task<DataResult> GetDataAsync()
    {
        await Task.Delay(100);
        return new DataResult { IsSuccess = true, Message = "Success" };
    }

    public async Task<DataResult> GetInvalidDataAsync()
    {
        await Task.Delay(100);
        throw new InvalidOperationException("Data not found");
    }
}

#endregion
