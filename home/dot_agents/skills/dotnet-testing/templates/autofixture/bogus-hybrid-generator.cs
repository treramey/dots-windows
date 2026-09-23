// =============================================================================
// AutoFixture + Bogus — hybrid generator and Fixture extensions
// Unified test-data API and a BogusAutoData attribute.
// =============================================================================

using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using Bogus;
using AwesomeAssertions;
using Xunit;

namespace AutoFixtureBogusIntegration.Templates;

#region ITestDataGenerator interface

public interface ITestDataGenerator
{
    T Generate<T>();
    IEnumerable<T> Generate<T>(int count);
    T Generate<T>(Action<T> configure);
    IEnumerable<T> Generate<T>(int count, Action<T> configure);
}

#endregion

#region HybridTestDataGenerator

/// <summary>
/// Hybrid test-data generator.
/// AutoFixture builds the object; SpecimenBuilders inject Bogus data.
/// </summary>
public class HybridTestDataGenerator : ITestDataGenerator
{
    private readonly Fixture _fixture;
    private readonly Dictionary<Type, object> _registeredFakers;

    public HybridTestDataGenerator()
    {
        _fixture = new Fixture();
        _registeredFakers = new Dictionary<Type, object>();

        ConfigureDefaults();
    }

    private void ConfigureDefaults()
    {
        // Handle recursion.
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _fixture.Customizations.Add(new EmailSpecimenBuilder());
        _fixture.Customizations.Add(new PhoneSpecimenBuilder());
        _fixture.Customizations.Add(new NameSpecimenBuilder());
        _fixture.Customizations.Add(new AddressSpecimenBuilder());
        _fixture.Customizations.Add(new WebsiteSpecimenBuilder());
    }

    public HybridTestDataGenerator WithFaker<T>(Faker<T> faker) where T : class
    {
        _registeredFakers[typeof(T)] = faker;
        _fixture.Customizations.Add(new TypedBogusSpecimenBuilder<T>(faker));
        return this;
    }

    public HybridTestDataGenerator WithSeed(int seed)
    {
        Randomizer.Seed = new Random(seed);
        return this;
    }

    public HybridTestDataGenerator WithRepeatCount(int count)
    {
        _fixture.RepeatCount = count;
        return this;
    }

    public T Generate<T>() => _fixture.Create<T>();

    public IEnumerable<T> Generate<T>(int count) => _fixture.CreateMany<T>(count);

    public T Generate<T>(Action<T> configure)
    {
        var instance = _fixture.Create<T>();
        configure(instance);
        return instance;
    }

    public IEnumerable<T> Generate<T>(int count, Action<T> configure)
    {
        return _fixture.CreateMany<T>(count)
            .Select(item =>
            {
                configure(item);
                return item;
            });
    }
}

#endregion

#region Fixture extensions

public static class FixtureExtensions
{
    /// <summary>Adds all default Bogus SpecimenBuilders.</summary>
    public static Fixture WithBogus(this Fixture fixture)
    {
        fixture.WithOmitOnRecursion();

        fixture.Customizations.Add(new EmailSpecimenBuilder());
        fixture.Customizations.Add(new PhoneSpecimenBuilder());
        fixture.Customizations.Add(new NameSpecimenBuilder());
        fixture.Customizations.Add(new AddressSpecimenBuilder());
        fixture.Customizations.Add(new WebsiteSpecimenBuilder());
        fixture.Customizations.Add(new CompanyNameSpecimenBuilder());

        return fixture;
    }

    /// <summary>Replaces ThrowingRecursionBehavior with OmitOnRecursionBehavior.</summary>
    public static Fixture WithOmitOnRecursion(this Fixture fixture, int recursionDepth = 1)
    {
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));

        fixture.Behaviors.Add(new OmitOnRecursionBehavior(recursionDepth));
        return fixture;
    }

    /// <summary>
    /// Sets the Bogus random seed.
    /// AutoFixture and Bogus use separate randomizers, so the seed
    /// gives stable behavior but does not guarantee identical values.
    /// </summary>
    public static Fixture WithSeed(this Fixture fixture, int seed)
    {
        Randomizer.Seed = new Random(seed);
        return fixture;
    }

    /// <summary>Sets the default CreateMany count.</summary>
    public static Fixture WithRepeatCount(this Fixture fixture, int count)
    {
        fixture.RepeatCount = count;
        return fixture;
    }

    /// <summary>Registers a custom Faker for type T.</summary>
    public static Fixture WithBogusFor<T>(this Fixture fixture, Faker<T> faker) where T : class
    {
        fixture.Customizations.Add(new TypedBogusSpecimenBuilder<T>(faker));
        return fixture;
    }

    /// <summary>Registers a custom Faker via configuration action.</summary>
    public static Fixture WithBogusFor<T>(this Fixture fixture, Action<Faker<T>> configure) where T : class
    {
        var faker = new Faker<T>();
        configure(faker);
        fixture.Customizations.Add(new TypedBogusSpecimenBuilder<T>(faker));
        return fixture;
    }

    /// <summary>Adds a custom SpecimenBuilder.</summary>
    public static Fixture WithSpecimenBuilder(this Fixture fixture, ISpecimenBuilder builder)
    {
        fixture.Customizations.Add(builder);
        return fixture;
    }

    /// <summary>Adds a localized SpecimenBuilder.</summary>
    public static Fixture WithLocale(this Fixture fixture, string locale)
    {
        fixture.Customizations.Add(new LocalizedSpecimenBuilder(locale));
        return fixture;
    }
}

#endregion

#region BogusAutoDataAttribute

/// <summary>BogusAutoData — registers default Bogus SpecimenBuilders.</summary>
public class BogusAutoDataAttribute : AutoDataAttribute
{
    public BogusAutoDataAttribute() : base(() => CreateFixture())
    {
    }

    private static Fixture CreateFixture() => new Fixture().WithBogus();
}

/// <summary>Seeded variant — fixed seed for stable runs.</summary>
public class SeededBogusAutoDataAttribute : AutoDataAttribute
{
    public SeededBogusAutoDataAttribute(int seed)
        : base(() => CreateFixture(seed))
    {
    }

    private static Fixture CreateFixture(int seed) =>
        new Fixture().WithBogus().WithSeed(seed);
}

#endregion

#region Test base

/// <summary>Common Bogus + AutoFixture test base.</summary>
public abstract class BogusTestBase
{
    protected readonly Fixture Fixture;
    protected readonly ITestDataGenerator Generator;

    protected BogusTestBase()
    {
        Fixture = new Fixture().WithBogus();
        Generator = new HybridTestDataGenerator();
    }

    protected T Create<T>() => Fixture.Create<T>();

    protected IEnumerable<T> CreateMany<T>(int count = 3) => Fixture.CreateMany<T>(count);

    protected T Create<T>(Action<T> configure)
    {
        var instance = Fixture.Create<T>();
        configure(instance);
        return instance;
    }
}

#endregion

#region Tests

public class HybridTestDataGeneratorTests
{
    [Fact]
    public void Generate_SingleObject()
    {
        var generator = new HybridTestDataGenerator();

        var user = generator.Generate<User>();

        user.Should().NotBeNull();
        user.Email.Should().Contain("@");
        user.FirstName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Generate_MultipleObjects()
    {
        var generator = new HybridTestDataGenerator();

        var users = generator.Generate<User>(5).ToList();

        users.Should().HaveCount(5);
        users.Select(u => u.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_WithConfigure()
    {
        var generator = new HybridTestDataGenerator();

        var user = generator.Generate<User>(u =>
        {
            u.Age = 25;
            u.FirstName = "TestUser";
        });

        user.Age.Should().Be(25);
        user.FirstName.Should().Be("TestUser");
    }

    [Fact]
    public void WithFaker_RegistersCustomFaker()
    {
        var customFaker = new Faker<Product>()
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.Name, _ => "Custom Product")
            .RuleFor(p => p.Price, _ => 99.99m);

        var generator = new HybridTestDataGenerator()
            .WithFaker(customFaker);

        var product = generator.Generate<Product>();

        product.Name.Should().Be("Custom Product");
        product.Price.Should().Be(99.99m);
    }
}

public class BogusAutoDataAttributeTests
{
    [Theory]
    [BogusAutoData]
    public void BogusAutoData_AutoInjectsBogusData(User user)
    {
        user.Should().NotBeNull();
        user.Email.Should().Contain("@");
        user.FirstName.Should().NotBeNullOrEmpty();
        user.LastName.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [BogusAutoData]
    public void BogusAutoData_MultipleParameters(User user, Product product)
    {
        user.Should().NotBeNull();
        product.Should().NotBeNull();
        user.Email.Should().Contain("@");
        product.Name.Should().NotBeNullOrEmpty();
    }
}

public class BogusTestBaseTests : BogusTestBase
{
    [Fact]
    public void Create_UsesIntegratedFixture()
    {
        var user = Create<User>();

        user.Email.Should().Contain("@");
        user.FirstName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateMany_ProducesRequestedCount()
    {
        var users = CreateMany<User>(5).ToList();

        users.Should().HaveCount(5);
        users.Should().AllSatisfy(u => u.Email.Should().Contain("@"));
    }
}

#endregion
