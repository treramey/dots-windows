// TUnit advanced lifecycle + dependency injection.
// Implements MicrosoftDependencyInjectionDataSourceAttribute on top of
// TUnit's DependencyInjectionDataSourceAttribute<IServiceScope>, so a
// real Microsoft DI container resolves test class constructors.

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace TUnit.Advanced.Lifecycle.Examples;

// ===== Domain types =====

public enum CustomerLevel
{
    Regular = 0,
    Vip = 1,
    Platinum = 2,
    Diamond = 3
}

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public CustomerLevel CustomerLevel { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public decimal SubTotal => Items.Sum(i => i.UnitPrice * i.Quantity);
    public decimal TotalAmount => SubTotal;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public interface IOrderRepository { Task<bool> SaveOrderAsync(Order order); }
public interface IDiscountCalculator { Task<decimal> CalculateDiscountAsync(Order order, string discountCode); }
public interface IShippingCalculator { decimal CalculateShippingFee(Order order); }
public interface ILogger<T> { void LogInformation(string message); }

// ===== Mocks =====

public class MockOrderRepository : IOrderRepository
{
    public Task<bool> SaveOrderAsync(Order order)
    {
        order.OrderId = Guid.NewGuid().ToString();
        return Task.FromResult(true);
    }
}

public class MockDiscountCalculator : IDiscountCalculator
{
    public Task<decimal> CalculateDiscountAsync(Order order, string discountCode)
    {
        var baseDiscount = order.CustomerLevel == CustomerLevel.Vip
            ? order.TotalAmount * 0.1m
            : 0m;
        return Task.FromResult(baseDiscount);
    }
}

public class MockShippingCalculator : IShippingCalculator
{
    public decimal CalculateShippingFee(Order order)
    {
        if (order.CustomerLevel == CustomerLevel.Diamond) return 0m;
        if (order.SubTotal >= 1000m) return 0m;
        return 80m;
    }
}

public class MockLogger<T> : ILogger<T>
{
    public void LogInformation(string message) { /* swallowed in tests */ }
}

// ===== System under test =====

public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IDiscountCalculator _discountCalculator;
    private readonly IShippingCalculator _shippingCalculator;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository repository,
        IDiscountCalculator discountCalculator,
        IShippingCalculator shippingCalculator,
        ILogger<OrderService> logger)
    {
        _repository = repository;
        _discountCalculator = discountCalculator;
        _shippingCalculator = shippingCalculator;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(string customerId, CustomerLevel level, List<OrderItem> items)
    {
        var order = new Order
        {
            CustomerId = customerId,
            CustomerLevel = level,
            Items = items
        };

        await _repository.SaveOrderAsync(order);
        _logger.LogInformation($"Order created: {order.OrderId}");
        return order;
    }
}

// ===== Lifecycle: full execution-order example =====

/// <summary>
/// Demonstrates the lifecycle: Before(Class) -> ctor -> Before(Test) ->
/// test -> After(Test) -> [Dispose] -> After(Class). Build a string log
/// inside the test to verify the order in tests.
/// </summary>
public class LifecycleCompleteExample
{
    private readonly StringBuilder _logBuilder;
    private static readonly List<string> ClassLog = [];

    public LifecycleCompleteExample()
    {
        _logBuilder = new StringBuilder();
        _logBuilder.AppendLine("constructor");
    }

    [Before(Class)]
    public static async Task BeforeClass()
    {
        ClassLog.Add("BeforeClass");
        await Task.Delay(10);
    }

    [Before(Test)]
    public async Task BeforeTest()
    {
        _logBuilder.AppendLine("BeforeTest");
        await Task.Delay(5);
    }

    [Test]
    public async Task FirstTest_ShouldSeeBeforeClassAndBeforeTest()
    {
        _logBuilder.AppendLine("FirstTest");

        var log = _logBuilder.ToString();
        await Assert.That(log).Contains("constructor");
        await Assert.That(log).Contains("BeforeTest");
        await Assert.That(ClassLog).Contains("BeforeClass");
    }

    [Test]
    public async Task SecondTest_ShouldHaveIndependentInstance()
    {
        _logBuilder.AppendLine("SecondTest");

        var log = _logBuilder.ToString();
        await Assert.That(log).Contains("constructor");
        await Assert.That(log).Contains("BeforeTest");
    }

    [After(Test)]
    public async Task AfterTest()
    {
        _logBuilder.AppendLine("AfterTest");
        await Task.Delay(5);
    }

    [After(Class)]
    public static async Task AfterClass()
    {
        ClassLog.Add("AfterClass");
        await Task.Delay(10);
    }
}

/// <summary>
/// IAsyncDisposable in TUnit — release resources cleanly when async
/// cleanup is required.
/// </summary>
public class DisposableLifecycleExample : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly List<string> _tempFiles = [];

    public DisposableLifecycleExample()
    {
        _httpClient = new HttpClient();
    }

    [Test]
    public async Task TestWithResources_ShouldClaimAndReleaseCleanly()
    {
        var tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);

        await Assert.That(_httpClient).IsNotNull();
        await Assert.That(File.Exists(tempFile)).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        await Task.CompletedTask;
    }
}

// ===== Dependency injection — Microsoft DI integration =====

/// <summary>
/// Bridges TUnit's data-source pipeline to Microsoft.Extensions.DependencyInjection.
/// Apply this attribute on a test class with a primary constructor; TUnit
/// resolves each constructor parameter from the scoped service provider.
/// </summary>
public class MicrosoftDependencyInjectionDataSourceAttribute : DependencyInjectionDataSourceAttribute<IServiceScope>
{
    private static readonly IServiceProvider ServiceProvider = CreateSharedServiceProvider();

    public override IServiceScope CreateScope(DataGeneratorMetadata dataGeneratorMetadata) =>
        ServiceProvider.CreateScope();

    public override object? Create(IServiceScope scope, Type type) =>
        scope.ServiceProvider.GetService(type);

    private static IServiceProvider CreateSharedServiceProvider() =>
        new ServiceCollection()
            .AddSingleton<IOrderRepository, MockOrderRepository>()
            .AddSingleton<IDiscountCalculator, MockDiscountCalculator>()
            .AddSingleton<IShippingCalculator, MockShippingCalculator>()
            .AddSingleton<ILogger<OrderService>, MockLogger<OrderService>>()
            .AddTransient<OrderService>()
            .BuildServiceProvider();
}

/// <summary>Test class consuming the DI-resolved OrderService.</summary>
[MicrosoftDependencyInjectionDataSource]
public class DependencyInjectionTests(OrderService orderService)
{
    [Test]
    public async Task CreateOrder_WithDiResolvedService_ShouldSucceed()
    {
        var items = new List<OrderItem>
        {
            new() { ProductId = "PROD001", ProductName = "Test", UnitPrice = 100m, Quantity = 2 }
        };

        var order = await orderService.CreateOrderAsync("CUST001", CustomerLevel.Vip, items);

        await Assert.That(order).IsNotNull();
        await Assert.That(order.CustomerId).IsEqualTo("CUST001");
        await Assert.That(order.CustomerLevel).IsEqualTo(CustomerLevel.Vip);
        await Assert.That(order.Items).HasCount().EqualTo(1);
    }
}

// ===== Properties for filtering =====

public class PropertiesExamples
{
    public static class TestProperties
    {
        public const string CATEGORY_UNIT = "Unit";
        public const string CATEGORY_INTEGRATION = "Integration";
        public const string CATEGORY_E2E = "E2E";

        public const string PRIORITY_CRITICAL = "Critical";
        public const string PRIORITY_HIGH = "High";
        public const string PRIORITY_MEDIUM = "Medium";
        public const string PRIORITY_LOW = "Low";
    }

    [Test]
    [Property("Category", "Database")]
    [Property("Priority", "High")]
    public async Task DatabaseTest_HighPriority_FilterableByProperty()
    {
        await Assert.That(true).IsTrue();
    }

    [Test]
    [Property("Category", TestProperties.CATEGORY_UNIT)]
    [Property("Priority", TestProperties.PRIORITY_HIGH)]
    public async Task UnitTest_UsingConstants_KeepsValuesConsistent()
    {
        await Assert.That(1 + 1).IsEqualTo(2);
    }
}

/*
 * Tree-node filter (TUnit uses `dotnet run`, not `dotnet test`):
 *
 *   dotnet run --treenode-filter "/*/*/*/*[Category=Unit]"
 *   dotnet run --treenode-filter "/*/*/*/*[Priority=High]"
 *   dotnet run --treenode-filter "/*/*/*/*[(Category=Unit)&(Priority=High)]"
 *
 * Path: Assembly / Namespace / Class / Method. Property names and values
 * are case-sensitive; parenthesize composite conditions.
 */
