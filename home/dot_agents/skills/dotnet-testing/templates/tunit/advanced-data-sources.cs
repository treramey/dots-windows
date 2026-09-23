// TUnit advanced data sources — MethodDataSource, ClassDataSource.
// TUnit 1.x note: ClassDataSource<T> injects a single T instance, it no
// longer enumerates IEnumerable<T>. To produce N test cases from a
// class, use MethodDataSource(typeof(DataClass), nameof(Method)).

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Text.Json;

namespace TUnit.Advanced.DataSource.Examples;

// ===== Domain models =====

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
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TotalAmount => SubTotal - DiscountAmount + ShippingFee;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class OrderValidationScenario
{
    public string Name { get; set; } = "";
    public Order Order { get; set; } = new();
    public bool ExpectedValid { get; set; }
    public string? ExpectedErrorKeyword { get; set; }
}

// ===== MethodDataSource — most flexible =====

/// <summary>
/// MethodDataSource binds the test to a method returning IEnumerable
/// of strongly-typed tuples. One tuple = one independent test case.
/// </summary>
public class MethodDataSourceBasicTests
{
    [Test]
    [MethodDataSource(nameof(GetOrderTestData))]
    public async Task CreateOrder_WithVariousInputs_ShouldComputeExpectedTotal(
        string customerId,
        CustomerLevel level,
        List<OrderItem> items,
        decimal expectedTotal)
    {
        // Arrange
        var order = new Order
        {
            CustomerId = customerId,
            CustomerLevel = level,
            Items = items
        };

        // Assert
        await Assert.That(order).IsNotNull();
        await Assert.That(order.CustomerId).IsEqualTo(customerId);
        await Assert.That(order.CustomerLevel).IsEqualTo(level);
        await Assert.That(order.SubTotal).IsEqualTo(expectedTotal);
    }

    /// <summary>Strongly-typed tuples; preferred over object[].</summary>
    public static IEnumerable<(string, CustomerLevel, List<OrderItem>, decimal)> GetOrderTestData()
    {
        yield return (
            "CUST001",
            CustomerLevel.Regular,
            new List<OrderItem>
            {
                new() { ProductId = "PROD001", ProductName = "Widget A", UnitPrice = 100m, Quantity = 2 }
            },
            200m
        );

        yield return (
            "CUST002",
            CustomerLevel.Vip,
            new List<OrderItem>
            {
                new() { ProductId = "PROD002", ProductName = "Widget B", UnitPrice = 500m, Quantity = 1 }
            },
            500m
        );

        yield return (
            "CUST003",
            CustomerLevel.Platinum,
            new List<OrderItem>
            {
                new() { ProductId = "PROD001", ProductName = "Widget A", UnitPrice = 100m, Quantity = 1 },
                new() { ProductId = "PROD002", ProductName = "Widget B", UnitPrice = 200m, Quantity = 2 }
            },
            500m
        );
    }
}

/// <summary>Loading data from an external JSON file.</summary>
public class MethodDataSourceFromFileTests
{
    [Test]
    [MethodDataSource(nameof(GetDiscountTestDataFromFile))]
    public async Task CalculateDiscount_FromFileScenarios_ShouldMatchExpected(
        string scenario,
        decimal originalAmount,
        CustomerLevel level,
        string discountCode,
        decimal expectedDiscount)
    {
        decimal discount = CalculateMockDiscount(originalAmount, level, discountCode);
        await Assert.That(discount).IsEqualTo(expectedDiscount);
    }

    public static IEnumerable<(string, decimal, CustomerLevel, string, decimal)> GetDiscountTestDataFromFile()
    {
        // In production this reads JSON via File.ReadAllText / JsonSerializer.
        var scenarios = new List<DiscountScenario>
        {
            new() { Scenario = "Regular member, no code", Amount = 1000, Level = 0, Code = "", Expected = 0 },
            new() { Scenario = "VIP with VIP code", Amount = 1000, Level = 1, Code = "VIP50", Expected = 50 },
            new() { Scenario = "Platinum with SAVE20", Amount = 1000, Level = 2, Code = "SAVE20", Expected = 250 }
        };

        foreach (var s in scenarios)
        {
            yield return (s.Scenario, s.Amount, (CustomerLevel)s.Level, s.Code, s.Expected);
        }
    }

    private static decimal CalculateMockDiscount(decimal amount, CustomerLevel level, string code) =>
        level switch
        {
            CustomerLevel.Regular => 0,
            CustomerLevel.Vip when code == "VIP50" => 50,
            CustomerLevel.Platinum when code == "SAVE20" => 250,
            _ => 0
        };

    private class DiscountScenario
    {
        public string Scenario { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Level { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal Expected { get; set; }
    }
}

// ===== ClassDataSource — TUnit 1.x semantics =====

/// <summary>
/// In TUnit 1.x, ClassDataSource&lt;T&gt; injects a single T instance.
/// To produce N test cases from a class, use MethodDataSource(typeof(...))
/// instead.
/// </summary>
public class ClassDataSourceTests
{
    /// <summary>Recommended: MethodDataSource pulling scenarios from a class.</summary>
    [Test]
    [MethodDataSource(typeof(OrderValidationTestData), nameof(OrderValidationTestData.GetScenarios))]
    public async Task ValidateOrder_AcrossScenarios_ShouldMatchExpectedFlag(OrderValidationScenario scenario)
    {
        var isValid = ValidateOrder(scenario.Order);
        await Assert.That(isValid).IsEqualTo(scenario.ExpectedValid);
    }

    /// <summary>
    /// ClassDataSource&lt;T&gt; correct 1.x use: inject a shared fixture
    /// or expensive-to-build service instance.
    /// </summary>
    [Test]
    [ClassDataSource<OrderValidationTestData>(Shared = SharedType.PerClass)]
    public async Task ValidateOrder_WithSharedDataObject_ShouldExposeScenarios(OrderValidationTestData testData)
    {
        var scenarios = OrderValidationTestData.GetScenarios().ToList();
        await Assert.That(scenarios).IsNotEmpty();
    }

    private static bool ValidateOrder(Order order)
    {
        if (string.IsNullOrEmpty(order.CustomerId)) return false;
        if (order.Items.Count == 0) return false;
        return true;
    }
}

public class OrderValidationTestData
{
    /// <summary>Static data method for MethodDataSource.</summary>
    public static IEnumerable<OrderValidationScenario> GetScenarios()
    {
        yield return new OrderValidationScenario
        {
            Name = "Valid regular order",
            Order = CreateValidOrder(),
            ExpectedValid = true,
            ExpectedErrorKeyword = null
        };

        yield return new OrderValidationScenario
        {
            Name = "Empty customer id",
            Order = CreateOrderWithEmptyCustomerId(),
            ExpectedValid = false,
            ExpectedErrorKeyword = "Customer"
        };

        yield return new OrderValidationScenario
        {
            Name = "No items",
            Order = CreateOrderWithNoItems(),
            ExpectedValid = false,
            ExpectedErrorKeyword = "Items"
        };
    }

    private static Order CreateValidOrder() => new()
    {
        CustomerId = "CUST001",
        CustomerLevel = CustomerLevel.Regular,
        Items = new List<OrderItem>
        {
            new() { ProductId = "PROD001", ProductName = "Test product", UnitPrice = 100m, Quantity = 1 }
        }
    };

    private static Order CreateOrderWithEmptyCustomerId() => new()
    {
        CustomerId = "",
        CustomerLevel = CustomerLevel.Regular,
        Items = new List<OrderItem>
        {
            new() { ProductId = "PROD001", ProductName = "Test product", UnitPrice = 100m, Quantity = 1 }
        }
    };

    private static Order CreateOrderWithNoItems() => new()
    {
        CustomerId = "CUST001",
        CustomerLevel = CustomerLevel.Regular,
        Items = new List<OrderItem>()
    };
}
