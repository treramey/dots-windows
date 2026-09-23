// =============================================================================
// CollectionSizeAttribute — control AutoData collection sizes
// =============================================================================

using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using AwesomeAssertions;
using Xunit;

namespace AutoDataXunitIntegration.Templates;

public class CollectionProduct
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}

public class CollectionCustomer
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
}

public class CollectionOrder
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

// -----------------------------------------------------------------------------
// CollectionSizeAttribute — overrides the default RepeatCount per parameter
// -----------------------------------------------------------------------------

/// <summary>
/// Controls the size of collections produced by AutoData.
/// Default size is 3 — this attribute overrides per parameter.
/// </summary>
public class CollectionSizeAttribute : CustomizeAttribute
{
    private readonly int _size;

    public CollectionSizeAttribute(int size) => _size = size;

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var objectType = parameter.ParameterType.GetGenericArguments()[0];

        var isTypeCompatible = parameter.ParameterType.IsGenericType &&
            parameter.ParameterType.GetGenericTypeDefinition()
                .MakeGenericType(objectType)
                .IsAssignableFrom(typeof(List<>).MakeGenericType(objectType));

        if (!isTypeCompatible)
        {
            throw new InvalidOperationException(
                $"{nameof(CollectionSizeAttribute)} target type is not List-compatible: " +
                $"{parameter.ParameterType} {parameter.Name}");
        }

        var customizationType = typeof(CollectionSizeCustomization<>).MakeGenericType(objectType);
        return (ICustomization)Activator.CreateInstance(customizationType, parameter, _size)!;
    }

    private sealed class CollectionSizeCustomization<T> : ICustomization
    {
        private readonly ParameterInfo _parameter;
        private readonly int _repeatCount;

        public CollectionSizeCustomization(ParameterInfo parameter, int repeatCount)
        {
            _parameter = parameter;
            _repeatCount = repeatCount;
        }

        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(
                new FilteringSpecimenBuilder(
                    new FixedBuilder(fixture.CreateMany<T>(_repeatCount).ToList()),
                    new EqualRequestSpecification(_parameter)));
        }
    }
}

// -----------------------------------------------------------------------------
// Tests
// -----------------------------------------------------------------------------

public class CollectionSizeTests
{
    [Theory]
    [AutoData]
    public void CollectionSize_ControlsSize(
        [CollectionSize(5)] List<CollectionProduct> products,
        [CollectionSize(3)] List<CollectionOrder> orders,
        CollectionCustomer customer)
    {
        products.Should().HaveCount(5);
        orders.Should().HaveCount(3);
        customer.Should().NotBeNull();

        products.Should().AllSatisfy(product =>
        {
            product.Name.Should().NotBeNullOrEmpty();
            product.Price.Should().BeGreaterOrEqualTo(0);
        });

        orders.Should().AllSatisfy(order =>
            order.OrderNumber.Should().NotBeNullOrEmpty());
    }

    [Theory]
    [AutoData]
    public void CollectionSize_MultipleDifferentSizes(
        [CollectionSize(1)] List<CollectionCustomer> singleCustomer,
        [CollectionSize(10)] List<CollectionProduct> manyProducts,
        [CollectionSize(2)] List<CollectionOrder> twoOrders)
    {
        singleCustomer.Should().HaveCount(1);
        manyProducts.Should().HaveCount(10);
        twoOrders.Should().HaveCount(2);
    }

    [Theory]
    [AutoData]
    public void CollectionSize_LargeData(
        [CollectionSize(100)] List<CollectionProduct> products)
    {
        products.Should().HaveCount(100);

        var distinctNames = products.Select(p => p.Name).Distinct().Count();
        distinctNames.Should().BeGreaterThan(1);
    }
}
