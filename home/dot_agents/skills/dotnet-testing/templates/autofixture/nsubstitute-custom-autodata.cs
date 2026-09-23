// =============================================================================
// Custom AutoData attributes — AutoFixture + NSubstitute integration
// =============================================================================

using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;
using Mapster;
using MapsterMapper;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace MyProject.Tests.AutoFixtureConfigurations;

// =============================================================================
// Basic AutoData attribute (AutoNSubstitute only)
// =============================================================================

/// <summary>
/// Basic auto-mocking AutoData attribute.
/// Auto-substitutes every interface / abstract class with NSubstitute.
/// </summary>
public class AutoNSubstituteDataAttribute : AutoDataAttribute
{
    public AutoNSubstituteDataAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture() =>
        new Fixture().Customize(new AutoNSubstituteCustomization());
}

/// <summary>Inline variant of AutoNSubstituteData.</summary>
public class InlineAutoNSubstituteDataAttribute : InlineAutoDataAttribute
{
    public InlineAutoNSubstituteDataAttribute(params object[] values)
        : base(new AutoNSubstituteDataAttribute(), values)
    {
    }
}

// =============================================================================
// Project-wide AutoData attribute (multiple customizations)
// =============================================================================

/// <summary>
/// Project-wide AutoData attribute.
/// Composes AutoNSubstitute + Mapster + domain customizations.
/// </summary>
public class AutoDataWithCustomizationAttribute : AutoDataAttribute
{
    public AutoDataWithCustomizationAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture() => new Fixture()
        .Customize(new AutoNSubstituteCustomization())
        .Customize(new MapsterMapperCustomization())
        .Customize(new DomainModelCustomization());
}

/// <summary>
/// Inline variant.
/// Important: InlineAutoDataAttribute requires an AutoDataAttribute *instance*,
/// not a Func&lt;IFixture&gt;.
/// </summary>
public class InlineAutoDataWithCustomizationAttribute : InlineAutoDataAttribute
{
    public InlineAutoDataWithCustomizationAttribute(params object[] values)
        : base(new AutoDataWithCustomizationAttribute(), values)
    {
    }
}

// =============================================================================
// Mapster customization
// =============================================================================

/// <summary>
/// Mapster mapper customization.
/// Returns a real IMapper, never a substitute.
/// </summary>
public class MapsterMapperCustomization : ICustomization
{
    private IMapper? _mapper;

    public void Customize(IFixture fixture) => fixture.Register(() => Mapper);

    private IMapper Mapper
    {
        get
        {
            if (_mapper is not null) return _mapper;

            var typeAdapterConfig = new TypeAdapterConfig();

            // Replace with your project's map register.
            typeAdapterConfig.Scan(typeof(ServiceMapRegister).Assembly);

            _mapper = new Mapper(typeAdapterConfig);
            return _mapper;
        }
    }
}

public class ServiceMapRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ShipperModel, ShipperDto>();
        config.NewConfig<ShipperDto, ShipperModel>();
    }
}

// =============================================================================
// AutoMapper customization (alternative)
// =============================================================================

public class AutoMapperCustomization : ICustomization
{
    private IMapper? _mapper;

    public void Customize(IFixture fixture) => fixture.Register<IMapper>(() => Mapper);

    private IMapper Mapper
    {
        get
        {
            if (_mapper is not null) return _mapper;

            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.AddMaps(typeof(MappingProfile).Assembly);
            });

            _mapper = configuration.CreateMapper();
            return _mapper;
        }
    }
}

public class MappingProfile : Profile
{
    public MappingProfile() => CreateMap<ShipperModel, ShipperDto>().ReverseMap();
}

// =============================================================================
// Domain model customization
// =============================================================================

public class DomainModelCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<ShipperModel>(composer => composer
            .With(x => x.ShipperId, () => fixture.Create<int>() % 1000 + 1)
            .With(x => x.CompanyName, () => $"Company_{fixture.Create<string>()[..8]}")
            .With(x => x.Phone, () => $"02-{fixture.Create<int>() % 90000000 + 10000000}"));

        fixture.Customize<OrderModel>(composer => composer
            .With(x => x.OrderId, () => fixture.Create<int>() % 10000 + 1)
            .With(x => x.OrderDate, () => DateTime.Today.AddDays(-fixture.Create<int>() % 365))
            .With(x => x.TotalAmount, () => Math.Round(fixture.Create<decimal>() % 10000, 2)));
    }
}

// =============================================================================
// Recursion-controlled AutoData attribute
// =============================================================================

/// <summary>
/// AutoData attribute that omits recursion (depth 1).
/// </summary>
public class AutoDataWithRecursionDepthAttribute : AutoDataAttribute
{
    public AutoDataWithRecursionDepthAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture()
    {
        var fixture = new Fixture()
            .Customize(new AutoNSubstituteCustomization());

        fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));

        fixture.Behaviors.Add(new OmitOnRecursionBehavior(recursionDepth: 1));

        return fixture;
    }
}

// =============================================================================
// CompositeCustomization
// =============================================================================

public class ProjectCustomization : CompositeCustomization
{
    public ProjectCustomization()
        : base(
            new AutoNSubstituteCustomization(),
            new MapsterMapperCustomization(),
            new DomainModelCustomization())
    {
    }
}

public class ProjectAutoDataAttribute : AutoDataAttribute
{
    public ProjectAutoDataAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture() =>
        new Fixture().Customize(new ProjectCustomization());
}

public class ProjectInlineAutoDataAttribute : InlineAutoDataAttribute
{
    public ProjectInlineAutoDataAttribute(params object[] values)
        : base(new ProjectAutoDataAttribute(), values)
    {
    }
}

// =============================================================================
// Sample domain models
// =============================================================================

public class ShipperModel
{
    public int ShipperId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class ShipperDto
{
    public int ShipperId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class OrderModel
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}
