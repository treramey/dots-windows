// =============================================================================
// AutoFixture complex objects and recursion handling
// Demonstrates nested object construction, collections, and recursion fixes.
// =============================================================================

using AutoFixture;
using AwesomeAssertions;

namespace TestProject.AutoFixtureBasics;

/// <summary>
/// Demonstrates AutoFixture handling complex object structures.
/// </summary>
public class ComplexObjectScenariosTests
{
    #region Nested object auto-construction

    [Fact]
    public void NestedObjects_FullyPopulatedAcrossAllLevels()
    {
        var fixture = new Fixture();

        var customer = fixture.Create<Customer>();

        customer.Should().NotBeNull();
        customer.Id.Should().BePositive();
        customer.Name.Should().NotBeNullOrEmpty();

        // Nested object
        customer.Address.Should().NotBeNull();
        customer.Address.Street.Should().NotBeNullOrEmpty();
        customer.Address.City.Should().NotBeNullOrEmpty();
        customer.Address.Location.Should().NotBeNull();
        customer.Address.Location.Latitude.Should().NotBe(0);

        // Another nested object
        customer.ContactInfo.Should().NotBeNull();
        customer.ContactInfo.Phone.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DeepNesting_AllLevelsConstructed()
    {
        var fixture = new Fixture();
        var order = fixture.Create<Order>();

        order.Customer.Address.Location.Should().NotBeNull();
        order.Customer.ContactInfo.Should().NotBeNull();
    }

    #endregion

    #region Collections and arrays

    [Fact]
    public void List_Populated()
    {
        var fixture = new Fixture();
        var order = fixture.Create<Order>();

        order.Items.Should().NotBeNull();
        order.Items.Should().NotBeEmpty();
        order.Items.Should().AllSatisfy(item =>
        {
            item.ProductId.Should().BePositive();
            item.ProductName.Should().NotBeNullOrEmpty();
            item.Quantity.Should().BePositive();
            item.UnitPrice.Should().BePositive();
        });
    }

    [Fact]
    public void Arrays_Populated()
    {
        var order = new Fixture().Create<Order>();
        order.Tags.Should().NotBeNull();
        order.Tags.Should().NotBeEmpty();
    }

    [Fact]
    public void Dictionary_Populated()
    {
        var order = new Fixture().Create<Order>();
        order.Metadata.Should().NotBeNull();
        order.Metadata.Should().NotBeEmpty();
    }

    [Fact]
    public void HashSet_Populated()
    {
        var order = new Fixture().Create<Order>();
        order.CategoryIds.Should().NotBeNull();
        order.CategoryIds.Should().NotBeEmpty();
    }

    #endregion

    #region Recursion handling

    [Fact]
    public void Recursion_DefaultBehaviour_Throws()
    {
        var fixture = new Fixture();

        Action act = () => fixture.Create<Category>();

        act.Should().Throw<ObjectCreationException>();
    }

    [Fact]
    public void Recursion_OmitOnRecursion_Succeeds()
    {
        var fixture = CreateRecursionSafeFixture();

        var category = fixture.Create<Category>();

        category.Should().NotBeNull();
        category.Id.Should().BePositive();
        category.Name.Should().NotBeNullOrEmpty();
        // Recursive property becomes null.
    }

    [Fact]
    public void BidirectionalAssociation_OmitOnRecursion_Succeeds()
    {
        var fixture = CreateRecursionSafeFixture();

        var customer = fixture.Create<CustomerWithOrders>();

        customer.Should().NotBeNull();
        customer.Orders.Should().NotBeNull();
    }

    #endregion

    #region Shared base pattern

    /// <summary>
    /// Builds a fixture that omits on recursion.
    /// </summary>
    private static Fixture CreateRecursionSafeFixture()
    {
        var fixture = new Fixture();

        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));

        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        return fixture;
    }

    #endregion

    #region Collection sizing

    [Fact]
    public void RepeatCount_ControlsCollectionSize()
    {
        var fixture = CreateRecursionSafeFixture();
        fixture.RepeatCount = 5;

        var order = fixture.Create<Order>();

        order.Items.Should().HaveCount(5);
    }

    [Fact]
    public void Build_With_ControlsSpecificCollection()
    {
        var fixture = CreateRecursionSafeFixture();

        var order = fixture.Build<Order>()
            .With(x => x.Items, fixture.CreateMany<OrderItem>(10).ToList())
            .Create();

        order.Items.Should().HaveCount(10);
    }

    #endregion

    #region Enums

    [Fact]
    public void Enum_ProducesValidValue()
    {
        var customer = new Fixture().Create<Customer>();

        customer.Type.Should().BeOneOf(
            CustomerType.Regular,
            CustomerType.Premium,
            CustomerType.VIP);
    }

    [Fact]
    public void Enum_CreateMany_ProducesVariedValues()
    {
        var fixture = new Fixture();

        var types = fixture.CreateMany<CustomerType>(10).ToList();

        types.Distinct().Should().HaveCountGreaterThan(1);
    }

    #endregion

    #region Sample model classes

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Address Address { get; set; } = null!;
        public ContactInfo ContactInfo { get; set; } = null!;
        public CustomerType Type { get; set; }
    }

    public class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public GeoLocation Location { get; set; } = null!;
    }

    public class GeoLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class ContactInfo
    {
        public string Phone { get; set; } = string.Empty;
        public string MobilePhone { get; set; } = string.Empty;
        public string Fax { get; set; } = string.Empty;
    }

    public enum CustomerType
    {
        Regular,
        Premium,
        VIP
    }

    public class Order
    {
        public int Id { get; set; }
        public Customer Customer { get; set; } = null!;
        public List<OrderItem> Items { get; set; } = new();
        public string[] Tags { get; set; } = Array.Empty<string>();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public HashSet<int> CategoryIds { get; set; } = new();
        public DateTime OrderDate { get; set; }
    }

    public class OrderItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    // Self-referential example
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Category? Parent { get; set; }
        public List<Category> Children { get; set; } = new();
    }

    // Bidirectional association example
    public class CustomerWithOrders
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderWithCustomer> Orders { get; set; } = new();
    }

    public class OrderWithCustomer
    {
        public int Id { get; set; }
        public CustomerWithOrders Customer { get; set; } = null!;
    }

    #endregion
}

/// <summary>
/// Suggested base class for unified recursion handling.
/// </summary>
public abstract class AutoFixtureTestBase
{
    protected Fixture CreateFixture()
    {
        var fixture = new Fixture();

        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));

        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        return fixture;
    }
}
