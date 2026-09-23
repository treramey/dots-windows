// =============================================================================
// AutoData attribute family — usage examples
// AutoData, InlineAutoData, MemberAutoData, CompositeAutoData.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using AutoFixture;
using AutoFixture.Xunit2;
using AwesomeAssertions;
using Xunit;

namespace AutoDataXunitIntegration.Templates;

// -----------------------------------------------------------------------------
// 1. Test models
// -----------------------------------------------------------------------------

public class Person
{
    public Guid Id { get; set; }

    [StringLength(10)]
    public string Name { get; set; } = string.Empty;

    [Range(18, 80)]
    public int Age { get; set; }

    public string Email { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}

public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class Customer
{
    public Person Person { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }

    public bool CanPlaceOrder(decimal orderAmount) => orderAmount <= CreditLimit;
}

public class Order
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public OrderStatus Status { get; set; }

    public void TransitionTo(OrderStatus newStatus) => Status = newStatus;
}

public enum OrderStatus
{
    Created,
    Confirmed,
    Shipped,
    Delivered,
    Completed,
    Cancelled
}

// -----------------------------------------------------------------------------
// 2. AutoData — auto-generates every parameter
// -----------------------------------------------------------------------------

public class AutoDataBasicTests
{
    [Theory]
    [AutoData]
    public void AutoData_GeneratesAllParameters(Person person, string message, int count)
    {
        person.Should().NotBeNull();
        person.Id.Should().NotBe(Guid.Empty);
        person.Name.Should().HaveLength(10);
        person.Age.Should().BeInRange(18, 80);
        message.Should().NotBeNullOrEmpty();
        count.Should().NotBe(0);
    }

    [Theory]
    [AutoData]
    public void AutoData_HonoursDataAnnotationsOnParameters(
        [StringLength(5, MinimumLength = 3)] string shortName,
        [Range(1, 100)] int percentage,
        Person person)
    {
        shortName.Length.Should().BeInRange(3, 5);
        percentage.Should().BeInRange(1, 100);
        person.Should().NotBeNull();
    }
}

// -----------------------------------------------------------------------------
// 3. InlineAutoData — fixed + auto-generated
// -----------------------------------------------------------------------------

public class InlineAutoDataTests
{
    /// <summary>
    /// Fixed values must appear in the same order as the parameters.
    /// </summary>
    [Theory]
    [InlineAutoData("VIP customer", 100000)]
    [InlineAutoData("Standard customer", 50000)]
    [InlineAutoData("New customer", 10000)]
    public void InlineAutoData_MixesFixedAndAuto(
        string customerType,
        decimal creditLimit,
        Person person)
    {
        var customer = new Customer
        {
            Person = person,
            Type = customerType,
            CreditLimit = creditLimit
        };

        customer.Type.Should().BeOneOf("VIP customer", "Standard customer", "New customer");
        customer.CreditLimit.Should().BeOneOf(100000, 50000, 10000);
        customer.Person.Should().NotBeNull();
        customer.Person.Age.Should().BeInRange(18, 80);
    }

    [Theory]
    [InlineAutoData("Product A", 100)]
    [InlineAutoData("Product B", 200)]
    [InlineAutoData("Product C", 300)]
    public void InlineAutoData_OrderingMatches(
        string name,
        decimal price,
        string description,
        Product product)
    {
        name.Should().BeOneOf("Product A", "Product B", "Product C");
        price.Should().BeOneOf(100, 200, 300);
        description.Should().NotBeNullOrEmpty();
        product.Should().NotBeNull();
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(1)]
    [InlineAutoData(100)]
    [InlineAutoData(int.MaxValue)]
    public void InlineAutoData_BoundaryValues(int boundary, string message)
    {
        boundary.Should().BeOneOf(0, 1, 100, int.MaxValue);
        message.Should().NotBeNullOrEmpty();
    }
}

// -----------------------------------------------------------------------------
// 4. MemberAutoData — externally sourced data
// -----------------------------------------------------------------------------

public class MemberAutoDataTests
{
    public static IEnumerable<object[]> GetProductCategories()
    {
        yield return new object[] { "Electronics", "TECH" };
        yield return new object[] { "Apparel", "FASHION" };
        yield return new object[] { "Home", "HOME" };
        yield return new object[] { "Sports", "SPORTS" };
    }

    [Theory]
    [MemberAutoData(nameof(GetProductCategories))]
    public void MemberAutoData_StaticMethod_Sources(
        string categoryName,
        string categoryCode,
        Product product)
    {
        var categorized = new CategorizedProduct
        {
            Product = product,
            CategoryName = categoryName,
            CategoryCode = categoryCode
        };

        categorized.CategoryName.Should().BeOneOf("Electronics", "Apparel", "Home", "Sports");
        categorized.CategoryCode.Should().BeOneOf("TECH", "FASHION", "HOME", "SPORTS");
        categorized.Product.Should().NotBeNull();
    }

    public static IEnumerable<object[]> StatusTransitions => new[]
    {
        new object[] { OrderStatus.Created, OrderStatus.Confirmed },
        new object[] { OrderStatus.Confirmed, OrderStatus.Shipped },
        new object[] { OrderStatus.Shipped, OrderStatus.Delivered },
        new object[] { OrderStatus.Delivered, OrderStatus.Completed }
    };

    [Theory]
    [MemberAutoData(nameof(StatusTransitions))]
    public void MemberAutoData_StaticProperty_OrderStatusTransitions(
        OrderStatus fromStatus,
        OrderStatus toStatus,
        Order order)
    {
        order.Status = fromStatus;
        order.TransitionTo(toStatus);
        order.Status.Should().Be(toStatus);
    }

    /// <summary>
    /// MemberAutoData accepts dynamically computed values (unlike InlineAutoData).
    /// </summary>
    public static IEnumerable<object[]> GetDynamicPriceData()
    {
        var basePrice = 1000m;
        yield return new object[] { "Basic", basePrice };
        yield return new object[] { "Pro", basePrice * 2 };
        yield return new object[] { "Enterprise", CalculateEnterprisePrice() };
    }

    private static decimal CalculateEnterprisePrice() => 5000m * 1.2m;

    [Theory]
    [MemberAutoData(nameof(GetDynamicPriceData))]
    public void MemberAutoData_DynamicValues(
        string planName,
        decimal price,
        Customer customer)
    {
        planName.Should().NotBeNullOrEmpty();
        price.Should().BeOneOf(1000m, 2000m, 6000m);
        customer.Should().NotBeNull();
    }
}

public class CategorizedProduct
{
    public Product Product { get; set; } = new();
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
}

// -----------------------------------------------------------------------------
// 5. Custom AutoData attributes
// -----------------------------------------------------------------------------

public class DomainAutoDataAttribute : AutoDataAttribute
{
    public DomainAutoDataAttribute() : base(() => CreateFixture()) { }

    private static IFixture CreateFixture()
    {
        var fixture = new Fixture();

        fixture.Customize<Person>(composer => composer
            .With(p => p.Age, () => Random.Shared.Next(18, 65))
            .With(p => p.Email, () => $"user{Random.Shared.Next(1000)}@example.com")
            .With(p => p.Name, () => $"User{Random.Shared.Next(100)}"));

        fixture.Customize<Product>(composer => composer
            .With(p => p.Price, () => Random.Shared.Next(100, 10000))
            .With(p => p.IsAvailable, true)
            .With(p => p.Name, () => $"Product{Random.Shared.Next(1000)}"));

        return fixture;
    }
}

public class BusinessAutoDataAttribute : AutoDataAttribute
{
    public BusinessAutoDataAttribute() : base(() => CreateFixture()) { }

    private static IFixture CreateFixture()
    {
        var fixture = new Fixture();

        fixture.Customize<Order>(composer => composer
            .With(o => o.Status, OrderStatus.Created)
            .With(o => o.Amount, () => Random.Shared.Next(1000, 50000))
            .With(o => o.OrderNumber, () => $"ORD{DateTime.Now:yyyyMMdd}{Random.Shared.Next(1000):D4}"));

        return fixture;
    }
}

public class CustomAutoDataTests
{
    [Theory]
    [DomainAutoData]
    public void UsingDomainAutoData(Person person, Product product)
    {
        person.Age.Should().BeInRange(18, 64);
        person.Email.Should().EndWith("@example.com");
        person.Name.Should().StartWith("User");

        product.IsAvailable.Should().BeTrue();
        product.Price.Should().BeInRange(100, 9999);
        product.Name.Should().StartWith("Product");
    }

    [Theory]
    [BusinessAutoData]
    public void UsingBusinessAutoData(Order order)
    {
        order.Status.Should().Be(OrderStatus.Created);
        order.Amount.Should().BeInRange(1000, 49999);
        order.OrderNumber.Should().StartWith("ORD");
    }
}

// -----------------------------------------------------------------------------
// 6. CompositeAutoData — stack multiple AutoData sources
// -----------------------------------------------------------------------------

public class CompositeAutoDataAttribute : AutoDataAttribute
{
    public CompositeAutoDataAttribute(params Type[] autoDataAttributeTypes)
        : base(() => CreateFixture(autoDataAttributeTypes))
    {
    }

    private static IFixture CreateFixture(Type[] autoDataAttributeTypes)
    {
        var fixture = new Fixture();

        foreach (var attributeType in autoDataAttributeTypes)
        {
            var createFixtureMethod = attributeType.GetMethod(
                "CreateFixture",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (createFixtureMethod != null)
            {
                var sourceFixture = (IFixture)createFixtureMethod.Invoke(null, null)!;

                foreach (var customization in sourceFixture.Customizations)
                {
                    fixture.Customizations.Add(customization);
                }
            }
        }

        return fixture;
    }
}

public class CompositeAutoDataTests
{
    [Theory]
    [CompositeAutoData(typeof(DomainAutoDataAttribute), typeof(BusinessAutoDataAttribute))]
    public void CompositeAutoData_StacksCustomizations(
        Person person,
        Product product,
        Order order)
    {
        // DomainAutoData rules.
        person.Age.Should().BeInRange(18, 64);
        person.Email.Should().EndWith("@example.com");
        product.IsAvailable.Should().BeTrue();

        // BusinessAutoData rules.
        order.Status.Should().Be(OrderStatus.Created);
        order.OrderNumber.Should().StartWith("ORD");
    }
}
