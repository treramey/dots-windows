// =============================================================================
// AutoFixture basic usage
// Demonstrates Fixture, Create<T>(), CreateMany<T>(), and Build<T>().
// =============================================================================

using AutoFixture;
using AwesomeAssertions;
using System.Net.Mail;

namespace TestProject.AutoFixtureBasics;

/// <summary>
/// Demonstrates AutoFixture basics.
/// </summary>
public class BasicAutoFixtureUsageTests
{
    #region Fixture and Create<T>()

    [Fact]
    public void Create_BasicTypes_ProducesValidValues()
    {
        // Arrange
        var fixture = new Fixture();

        // Act — produce a variety of basic types.
        var id = fixture.Create<int>();
        var name = fixture.Create<string>();
        var price = fixture.Create<decimal>();
        var isActive = fixture.Create<bool>();
        var date = fixture.Create<DateTime>();
        var guid = fixture.Create<Guid>();

        // Assert
        id.Should().BePositive();
        name.Should().NotBeNullOrEmpty();
        price.Should().BePositive();
        date.Should().BeAfter(DateTime.MinValue);
        guid.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_SpecialTypes_ProducesValidShapes()
    {
        var fixture = new Fixture();

        var email = fixture.Create<MailAddress>();
        var uri = fixture.Create<Uri>();
        var version = fixture.Create<Version>();
        var timeSpan = fixture.Create<TimeSpan>();

        email.Address.Should().Contain("@");
        uri.IsAbsoluteUri.Should().BeTrue();
        version.Major.Should().BePositive();
    }

    [Fact]
    public void Create_ProducesDifferentValuesEachCall()
    {
        var fixture = new Fixture();

        var name1 = fixture.Create<string>();
        var name2 = fixture.Create<string>();
        var id1 = fixture.Create<int>();
        var id2 = fixture.Create<int>();

        name1.Should().NotBe(name2);
        id1.Should().NotBe(id2);
    }

    #endregion

    #region CreateMany<T>() collections

    [Fact]
    public void CreateMany_DefaultCount_ProducesThreeItems()
    {
        var fixture = new Fixture();

        var items = fixture.CreateMany<string>().ToList();

        items.Should().HaveCount(3);
        items.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void CreateMany_ExplicitCount_ProducesRequestedNumber()
    {
        var fixture = new Fixture();

        var items = fixture.CreateMany<int>(10).ToList();

        items.Should().HaveCount(10);
        items.Should().AllSatisfy(x => x.Should().BePositive());
    }

    [Fact]
    public void CreateMany_ComplexType_PopulatesFully()
    {
        var fixture = new Fixture();

        var products = fixture.CreateMany<Product>(5).ToList();

        products.Should().HaveCount(5);
        products.Should().AllSatisfy(p =>
        {
            p.Id.Should().BePositive();
            p.Name.Should().NotBeNullOrEmpty();
            p.Price.Should().BePositive();
        });
    }

    #endregion

    #region Build<T>() fine-grained control

    [Fact]
    public void Build_With_SetsSpecificProperty()
    {
        var fixture = new Fixture();

        var customer = fixture.Build<Customer>()
            .With(x => x.Name, "Test customer")
            .With(x => x.Age, 25)
            .Create();

        customer.Name.Should().Be("Test customer");
        customer.Age.Should().Be(25);
        // Unset properties are still auto-populated.
        customer.Id.Should().BePositive();
        customer.Email.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Build_Without_LeavesPropertyAtDefault()
    {
        var fixture = new Fixture();

        var customer = fixture.Build<Customer>()
            .Without(x => x.InternalId)
            .Without(x => x.CreatedDate)
            .Create();

        customer.InternalId.Should().BeNullOrEmpty();
        customer.CreatedDate.Should().Be(default);
        customer.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Build_FactoryLambda_GeneratesPerCallValues()
    {
        var fixture = new Fixture();

        var products = fixture.Build<Product>()
            .With(x => x.Price, () => Math.Round((decimal)Random.Shared.NextDouble() * 1000, 2))
            .CreateMany(10)
            .ToList();

        products.Should().AllSatisfy(p => p.Price.Should().BeInRange(0, 1000));
    }

    #endregion

    #region OmitAutoProperties()

    [Fact]
    public void OmitAutoProperties_LeavesUnsetPropertiesAtDefault()
    {
        var fixture = new Fixture();

        var customer = fixture.Build<Customer>()
            .OmitAutoProperties()
            .With(x => x.Id, 123)
            .With(x => x.Name, "Test customer")
            .Create();

        customer.Id.Should().Be(123);
        customer.Name.Should().Be("Test customer");
        // Unset properties remain at their defaults.
        customer.Email.Should().BeNullOrEmpty();
        customer.Age.Should().Be(0);
        customer.Address.Should().BeNull();
    }

    [Fact]
    public void OmitAutoProperties_With_ReenablesAutoGeneration()
    {
        var fixture = new Fixture();

        var customer = fixture.Build<Customer>()
            .OmitAutoProperties()
            .With(x => x.Id)            // Enables auto generation for Id.
            .With(x => x.Name)          // Enables auto generation for Name.
            .With(x => x.Email, "test@example.com")
            .Create();

        customer.Id.Should().NotBe(0);
        customer.Name.Should().NotBeNullOrEmpty();
        customer.Email.Should().Be("test@example.com");
        customer.Age.Should().Be(0);
    }

    #endregion

    #region Sample model classes

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public Address? Address { get; set; }
        public string? InternalId { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    #endregion
}
