// =============================================================================
// AutoFixture custom ISpecimenBuilder samples
// Demonstrates building precise per-property generators.
// =============================================================================

using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using AwesomeAssertions;
using Xunit;

namespace AutoFixtureCustomization.Templates;

// -----------------------------------------------------------------------------
// 1. Test models
// -----------------------------------------------------------------------------

public class Member
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class Order
{
    public Guid Id { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime ShipDate { get; set; }
    public int Quantity { get; set; }
    public int Priority { get; set; }
}

// -----------------------------------------------------------------------------
// 2. RandomRangedDateTimeBuilder — precise DateTime control by property name
// -----------------------------------------------------------------------------

/// <summary>
/// Custom DateTime range builder.
/// Only acts on named DateTime properties; others are unaffected.
/// </summary>
public class RandomRangedDateTimeBuilder : ISpecimenBuilder
{
    private readonly DateTime _minDate;
    private readonly DateTime _maxDate;
    private readonly HashSet<string> _targetProperties;

    public RandomRangedDateTimeBuilder(
        DateTime minDate,
        DateTime maxDate,
        params string[] targetProperties)
    {
        _minDate = minDate;
        _maxDate = maxDate;
        _targetProperties = new HashSet<string>(targetProperties);
    }

    public object Create(object request, ISpecimenContext context)
    {
        // 1. Only act on PropertyInfo requests.
        if (request is not PropertyInfo propertyInfo)
            return new NoSpecimen();

        // 2. Only DateTime properties.
        if (propertyInfo.PropertyType != typeof(DateTime))
            return new NoSpecimen();

        // 3. Only properties in the target list.
        if (!_targetProperties.Contains(propertyInfo.Name))
            return new NoSpecimen();

        // 4. Generate a random DateTime in range.
        return GenerateRandomDateTime();
    }

    private DateTime GenerateRandomDateTime()
    {
        var range = _maxDate - _minDate;
        var randomTicks = (long)(Random.Shared.NextDouble() * range.Ticks);
        return _minDate.AddTicks(randomTicks);
    }
}

// -----------------------------------------------------------------------------
// 3. RandomRangedNumericSequenceBuilder — int range by property name
// -----------------------------------------------------------------------------

/// <summary>
/// Simple int range builder; matches by property name.
/// </summary>
public class RandomRangedNumericSequenceBuilder : ISpecimenBuilder
{
    private readonly int _min;
    private readonly int _max;
    private readonly HashSet<string> _targetProperties;

    public RandomRangedNumericSequenceBuilder(
        int min,
        int max,
        params string[] targetProperties)
    {
        _min = min;
        _max = max;
        _targetProperties = new HashSet<string>(targetProperties);
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is not PropertyInfo propertyInfo)
            return new NoSpecimen();

        if (propertyInfo.PropertyType != typeof(int))
            return new NoSpecimen();

        if (!_targetProperties.Contains(propertyInfo.Name))
            return new NoSpecimen();

        // Random.Next(min, max) — max is exclusive.
        return Random.Shared.Next(_min, _max);
    }
}

// -----------------------------------------------------------------------------
// 4. ImprovedRandomRangedNumericSequenceBuilder — predicate-based version
// -----------------------------------------------------------------------------

/// <summary>
/// Predicate-driven int range builder.
/// Allows precise control over which properties are handled.
/// </summary>
public class ImprovedRandomRangedNumericSequenceBuilder : ISpecimenBuilder
{
    private readonly int _min;
    private readonly int _max;
    private readonly Func<PropertyInfo, bool> _predicate;

    public ImprovedRandomRangedNumericSequenceBuilder(
        int min,
        int max,
        Func<PropertyInfo, bool> predicate)
    {
        _min = min;
        _max = max;
        _predicate = predicate;
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is not PropertyInfo propertyInfo)
            return new NoSpecimen();

        if (propertyInfo.PropertyType != typeof(int))
            return new NoSpecimen();

        // Predicate decides whether to handle.
        if (!_predicate(propertyInfo))
            return new NoSpecimen();

        return Random.Shared.Next(_min, _max);
    }
}

// -----------------------------------------------------------------------------
// 5. Tests
// -----------------------------------------------------------------------------

public class CustomSpecimenBuilderTests
{
    [Fact]
    public void RandomRangedDateTimeBuilder_OnlyTargetsNamedProperty()
    {
        var fixture = new Fixture();
        var minDate = new DateTime(2025, 1, 1);
        var maxDate = new DateTime(2025, 12, 31);

        // Only restrict UpdateTime.
        fixture.Customizations.Add(
            new RandomRangedDateTimeBuilder(minDate, maxDate, "UpdateTime"));

        var member = fixture.Create<Member>();

        // UpdateTime stays in range.
        member.UpdateTime.Should().BeOnOrAfter(minDate);
        member.UpdateTime.Should().BeOnOrBefore(maxDate);

        // CreateTime is unaffected (default AutoFixture).
        member.CreateTime.Should().NotBe(default);
    }

    [Fact]
    public void RandomRangedDateTimeBuilder_ControlsMultipleProperties()
    {
        var fixture = new Fixture();
        var minDate = new DateTime(2025, 1, 1);
        var maxDate = new DateTime(2025, 6, 30);

        fixture.Customizations.Add(
            new RandomRangedDateTimeBuilder(minDate, maxDate, "OrderDate", "ShipDate"));

        var orders = fixture.CreateMany<Order>(10).ToList();

        orders.Should().AllSatisfy(order =>
        {
            order.OrderDate.Should().BeOnOrAfter(minDate);
            order.OrderDate.Should().BeOnOrBefore(maxDate);
            order.ShipDate.Should().BeOnOrAfter(minDate);
            order.ShipDate.Should().BeOnOrBefore(maxDate);
        });
    }

    [Fact]
    public void Add_MayBeOverriddenByBuiltInBuilders()
    {
        var fixture = new Fixture();

        // Add() registers but does not guarantee priority over built-ins.
        fixture.Customizations.Add(
            new RandomRangedNumericSequenceBuilder(30, 50, "Age"));

        var member = fixture.Create<Member>();

        // The built-in NumericSequenceGenerator may intercept ints;
        // switch to Insert(0) when that happens.
        member.Age.Should().BePositive();
    }

    [Fact]
    public void Insert0_GuaranteesHighestPriority()
    {
        var fixture = new Fixture();

        fixture.Customizations.Insert(0,
            new ImprovedRandomRangedNumericSequenceBuilder(
                30, 50,
                prop => prop.Name == "Age" && prop.DeclaringType == typeof(Member)));

        var members = fixture.CreateMany<Member>(20).ToList();

        members.Should().AllSatisfy(m => m.Age.Should().BeInRange(30, 49));
    }

    [Fact]
    public void Predicate_ControlsMultiplePropertiesPrecisely()
    {
        var fixture = new Fixture();

        // Order.Quantity in 1..100.
        fixture.Customizations.Insert(0,
            new ImprovedRandomRangedNumericSequenceBuilder(
                1, 100,
                prop => prop.Name == "Quantity" && prop.DeclaringType == typeof(Order)));

        // Order.Priority in 1..5.
        fixture.Customizations.Insert(0,
            new ImprovedRandomRangedNumericSequenceBuilder(
                1, 5,
                prop => prop.Name == "Priority" && prop.DeclaringType == typeof(Order)));

        var orders = fixture.CreateMany<Order>(20).ToList();

        orders.Should().AllSatisfy(order =>
        {
            order.Quantity.Should().BeInRange(1, 99);
            order.Priority.Should().BeInRange(1, 4);
        });
    }

    [Fact]
    public void CombinedDateTimeAndNumericBuilders()
    {
        var fixture = new Fixture();

        var minDate = new DateTime(2025, 1, 1);
        var maxDate = new DateTime(2025, 12, 31);

        // DateTime range — Add() is fine (no built-in conflict).
        fixture.Customizations.Add(
            new RandomRangedDateTimeBuilder(minDate, maxDate, "OrderDate"));

        // Numeric range — Insert(0) for priority over NumericSequenceGenerator.
        fixture.Customizations.Insert(0,
            new ImprovedRandomRangedNumericSequenceBuilder(
                1, 100,
                prop => prop.Name == "Quantity" && prop.DeclaringType == typeof(Order)));

        var order = fixture.Create<Order>();

        order.OrderDate.Should().BeOnOrAfter(minDate);
        order.OrderDate.Should().BeOnOrBefore(maxDate);
        order.Quantity.Should().BeInRange(1, 99);
    }
}

// -----------------------------------------------------------------------------
// 6. NoSpecimen — why it matters
// -----------------------------------------------------------------------------

/// <summary>
/// Shows why returning NoSpecimen() (not null) is critical in chain-of-responsibility builders.
/// </summary>
public class NoSpecimenExplanation
{
    /// <summary>Wrong: returning null breaks the chain.</summary>
    public class BadSpecimenBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is PropertyInfo propertyInfo &&
                propertyInfo.Name == "Age")
            {
                return 25;
            }

            // Wrong — returning null causes everything else to be null too.
            return null!;
        }
    }

    /// <summary>Right: NoSpecimen lets the next builder handle the request.</summary>
    public class GoodSpecimenBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is PropertyInfo propertyInfo &&
                propertyInfo.Name == "Age")
            {
                return 25;
            }

            // Right — defer to the chain.
            return new NoSpecimen();
        }
    }
}
