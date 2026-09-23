// =============================================================================
// Bogus basic usage examples
// Demonstrates Faker<T> syntax, RuleFor rules, and data generation
// =============================================================================

using Bogus;
using AwesomeAssertions;
using Xunit;

namespace BogusBasics.Templates;

#region Test model classes

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsAvailable { get; set; }
}

public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public bool IsPremium { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => Quantity * UnitPrice;
}

#endregion

#region Basic Faker<T> usage

public class BasicFakerUsageExamples
{
    /// <summary>
    /// The simplest possible Faker — single rule per property, single Generate() call.
    /// </summary>
    [Fact]
    public void BasicFaker_GeneratesSingleObject()
    {
        // Arrange — Faker<T> with RuleFor entries
        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Id, f => f.IndexFaker)              // monotonic index
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Category, f => f.Commerce.Department())
            .RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000))
            .RuleFor(p => p.Description, f => f.Lorem.Sentence())
            .RuleFor(p => p.CreatedDate, f => f.Date.Past())
            .RuleFor(p => p.IsAvailable, f => f.Random.Bool());

        // Act
        var product = productFaker.Generate();

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().NotBeNullOrEmpty();
        product.Price.Should().BeInRange(10, 1000);
        product.CreatedDate.Should().BeBefore(DateTime.Now);
    }

    /// <summary>
    /// Generate(n) eagerly produces a List of n items.
    /// </summary>
    [Fact]
    public void BasicFaker_GeneratesMultipleObjects()
    {
        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Id, f => f.IndexFaker)
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000));

        var products = productFaker.Generate(10);

        products.Should().HaveCount(10);
        products.Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// GenerateLazy returns IEnumerable — only materialises what you pull.
    /// </summary>
    [Fact]
    public void GenerateLazy_DefersMaterialisation()
    {
        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName());

        IEnumerable<Product> lazyProducts = productFaker.GenerateLazy(100);

        // Only 5 of the 100 are actually generated
        var firstFive = lazyProducts.Take(5).ToList();

        firstFive.Should().HaveCount(5);
    }
}

#endregion

#region RuleFor variations

public class RuleForExamples
{
    /// <summary>
    /// RuleFor against the major data-type categories.
    /// </summary>
    [Fact]
    public void RuleFor_VariousDataTypes()
    {
        var customerFaker = new Faker<Customer>()
            .RuleFor(c => c.Id, f => f.Random.Guid())
            .RuleFor(c => c.Name, f => f.Person.FullName)
            .RuleFor(c => c.Email, f => f.Internet.Email())
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber())
            .RuleFor(c => c.Address, f => f.Address.FullAddress())
            .RuleFor(c => c.BirthDate, f => f.Date.Past(50, DateTime.Now.AddYears(-18)))
            .RuleFor(c => c.IsPremium, f => f.Random.Bool());

        var customer = customerFaker.Generate();

        customer.Id.Should().NotBe(Guid.Empty);
        customer.Email.Should().Contain("@");
    }

    /// <summary>
    /// Cross-property rules — (f, c) hands you the partly-built object.
    /// </summary>
    [Fact]
    public void RuleFor_DerivesEmailFromName()
    {
        var customerFaker = new Faker<Customer>()
            .RuleFor(c => c.Id, f => f.Random.Guid())
            .RuleFor(c => c.Name, f => f.Person.FullName)
            .RuleFor(c => c.Email, (f, c) =>
            {
                var nameParts = c.Name.Split(' ');
                var firstName = nameParts.FirstOrDefault() ?? "user";
                var lastName = nameParts.LastOrDefault() ?? "name";
                return f.Internet.Email(firstName, lastName, "company.com");
            });

        var customer = customerFaker.Generate();

        customer.Email.Should().EndWith("@company.com");
    }

    /// <summary>
    /// IndexFaker yields a monotonic counter — useful for fake primary keys.
    /// </summary>
    [Fact]
    public void IndexFaker_GeneratesMonotonicIds()
    {
        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Id, f => f.IndexFaker)
            .RuleFor(p => p.Name, f => f.Commerce.ProductName());

        var products = productFaker.Generate(5);

        products[0].Id.Should().Be(0);
        products[1].Id.Should().Be(1);
        products[4].Id.Should().Be(4);
    }

    /// <summary>
    /// Nested object generation: build the inner Faker inline and consume it via RuleFor.
    /// A later rule derives TotalAmount from the items just generated.
    /// </summary>
    [Fact]
    public void RuleFor_NestedItemsAndDerivedTotal()
    {
        var orderFaker = new Faker<Order>()
            .RuleFor(o => o.Id, f => f.IndexFaker)
            .RuleFor(o => o.CustomerId, f => f.Random.Guid())
            .RuleFor(o => o.CustomerName, f => f.Person.FullName)
            .RuleFor(o => o.OrderDate, f => f.Date.Past())
            .RuleFor(o => o.Status, f => f.PickRandom("Pending", "Processing", "Shipped", "Delivered"))
            .RuleFor(o => o.Items, f =>
            {
                var itemFaker = new Faker<OrderItem>()
                    .RuleFor(i => i.Id, f => f.IndexFaker)
                    .RuleFor(i => i.ProductName, f => f.Commerce.ProductName())
                    .RuleFor(i => i.Quantity, f => f.Random.Int(1, 10))
                    .RuleFor(i => i.UnitPrice, f => f.Random.Decimal(10, 500));

                return itemFaker.Generate(f.Random.Int(1, 5));
            })
            .RuleFor(o => o.TotalAmount, (f, o) => o.Items.Sum(i => i.Subtotal));

        var order = orderFaker.Generate();

        order.Items.Should().HaveCountGreaterOrEqualTo(1);
        order.TotalAmount.Should().Be(order.Items.Sum(i => i.Subtotal));
    }
}

#endregion

#region Seeded randomness

public class SeedExamples
{
    /// <summary>
    /// Set Randomizer.Seed at the entry point to make a generation reproducible.
    /// </summary>
    [Fact]
    public void Seed_GuaranteesReproducibleSequence()
    {
        Randomizer.Seed = new Random(12345);

        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => f.Random.Decimal(10, 100));

        var products1 = productFaker.Generate(3);

        Randomizer.Seed = new Random(12345);
        var products2 = productFaker.Generate(3);

        for (int i = 0; i < 3; i++)
        {
            products1[i].Name.Should().Be(products2[i].Name);
            products1[i].Price.Should().Be(products2[i].Price);
        }

        // Clean-up — back to system entropy
        Randomizer.Seed = new Random();
    }

    /// <summary>
    /// UseSeed seeds a single Faker instance independently of Randomizer.Seed.
    /// </summary>
    [Fact]
    public void UseSeed_SeedsOneFakerOnly()
    {
        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .UseSeed(42);

        var products1 = productFaker.Generate(3);

        var productFaker2 = new Faker<Product>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .UseSeed(42);

        var products2 = productFaker2.Generate(3);

        for (int i = 0; i < 3; i++)
        {
            products1[i].Name.Should().Be(products2[i].Name);
        }
    }
}

#endregion

#region Conditional / probabilistic generation

public class ConditionalGenerationExamples
{
    /// <summary>
    /// PickRandom uniformly picks from a fixed set.
    /// </summary>
    [Fact]
    public void PickRandom_PicksFromFixedSet()
    {
        var orderFaker = new Faker<Order>()
            .RuleFor(o => o.Status, f => f.PickRandom("Pending", "Processing", "Shipped", "Delivered"));

        var orders = orderFaker.Generate(100);

        orders.All(o => new[] { "Pending", "Processing", "Shipped", "Delivered" }.Contains(o.Status))
            .Should().BeTrue();
    }

    /// <summary>
    /// PickRandomWeighted controls the distribution — values + matching weights.
    /// </summary>
    [Fact]
    public void PickRandomWeighted_RespectsDistribution()
    {
        var customerFaker = new Faker<Customer>()
            .RuleFor(c => c.Name, f => f.Person.FullName)
            // 70% User, 25% Admin, 5% SuperAdmin
            .RuleFor(c => c.Address, f => f.PickRandomWeighted(
                new[] { "User", "Admin", "SuperAdmin" },
                new[] { 0.7f, 0.25f, 0.05f }));

        var customers = customerFaker.Generate(1000);

        var userCount = customers.Count(c => c.Address == "User");
        userCount.Should().BeGreaterThan(600); // ~70%
    }

    /// <summary>
    /// OrNull randomly nulls out the result at the supplied probability.
    /// </summary>
    [Fact]
    public void OrNull_NullsValueWithProbability()
    {
        var customerFaker = new Faker<Customer>()
            .RuleFor(c => c.Name, f => f.Person.FullName)
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber().OrNull(f, 0.5f));

        var customers = customerFaker.Generate(100);

        var nullCount = customers.Count(c => c.Phone == null);
        nullCount.Should().BeGreaterThan(30).And.BeLessThan(70); // ~50%
    }

    /// <summary>
    /// Bool(p) returns true with probability p.
    /// </summary>
    [Fact]
    public void Bool_ProbabilityControlled()
    {
        var customerFaker = new Faker<Customer>()
            .RuleFor(c => c.Name, f => f.Person.FullName)
            .RuleFor(c => c.IsPremium, f => f.Random.Bool(0.2f));

        var customers = customerFaker.Generate(1000);

        var premiumCount = customers.Count(c => c.IsPremium);
        premiumCount.Should().BeGreaterThan(150).And.BeLessThan(250); // ~20%
    }
}

#endregion

#region Localisation

public class LocalizationExamples
{
    /// <summary>
    /// Pass a locale code to the Faker<T> constructor.
    /// </summary>
    [Fact]
    public void Localization_TraditionalChinese()
    {
        var customerFaker = new Faker<Customer>("zh_TW")
            .RuleFor(c => c.Id, f => f.Random.Guid())
            .RuleFor(c => c.Name, f => f.Person.FullName)
            .RuleFor(c => c.Address, f => f.Address.FullAddress())
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber());

        var customer = customerFaker.Generate();

        customer.Name.Should().NotBeNullOrEmpty();
        customer.Address.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Japanese locale.
    /// </summary>
    [Fact]
    public void Localization_Japanese()
    {
        var customerFaker = new Faker<Customer>("ja")
            .RuleFor(c => c.Name, f => f.Person.FullName)
            .RuleFor(c => c.Address, f => f.Address.FullAddress());

        var customer = customerFaker.Generate();

        customer.Name.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Pick a locale at row level — multi-region table where each row uses its own locale.
    /// </summary>
    [Fact]
    public void Localization_PerRowLocaleChoice()
    {
        var locales = new[] { "en_US", "zh_TW", "ja", "ko", "fr" };

        var customerFaker = new Faker<Customer>()
            .RuleFor(c => c.Id, f => f.Random.Guid())
            .RuleFor(c => c.Address, f => f.PickRandom(locales)) // store locale here
            .RuleFor(c => c.Name, (f, c) =>
            {
                var localFaker = new Faker(c.Address);
                return localFaker.Person.FullName;
            });

        var customers = customerFaker.Generate(5);

        customers.Should().AllSatisfy(c => c.Name.Should().NotBeNullOrEmpty());
    }
}

#endregion
