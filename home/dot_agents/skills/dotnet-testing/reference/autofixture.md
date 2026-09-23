# AutoFixture

AutoFixture removes the dense Arrange block. Instead of hand-rolling test data — instantiating an object, setting twelve properties, none of which the test cares about — you ask the library for "an instance of `T`" and it fills every property with plausible filler. The test becomes a statement about the *behavior* under test, not a recitation of irrelevant data.

Reach for AutoFixture when (a) the test cares about behavior, not specific input values; (b) the production object graph is deep and constructing it manually would dominate the test; or (c) you want xUnit `[Theory]` parameters injected automatically. Skip it when the test's whole point *is* a specific input value — those tests should pin that value explicitly. A pinned value beats a generated one whenever the value is part of the test's intent. See [reference/builder-pattern.md](builder-pattern.md) for the hand-rolled alternative.

Package set, all pinned in `Directory.Packages.props`:

```xml
<PackageReference Include="AutoFixture" />
<PackageReference Include="AutoFixture.AutoNSubstitute" />
<PackageReference Include="AutoFixture.Xunit2" />
```

FIRST, 3A, and three-part naming all apply — see SKILL.md.

## Basics

`Fixture` is the entry point. It produces values for every common BCL type out of the box (primitives, strings, `Guid`, `DateTime`, `Uri`, `MailAddress`, `Version`, `TimeSpan`, collections) and walks complex object graphs property-by-property.

```csharp
var fixture = new Fixture();

var id     = fixture.Create<int>();         // positive integer
var name   = fixture.Create<string>();      // GUID-shaped string
var price  = fixture.Create<decimal>();     // positive decimal
var when   = fixture.Create<DateTime>();    // after MinValue
var guid   = fixture.Create<Guid>();        // not Guid.Empty
var email  = fixture.Create<MailAddress>(); // .Address contains "@"
var uri    = fixture.Create<Uri>();         // IsAbsoluteUri == true

var orders = fixture.CreateMany<Order>();   // 3 by default
var batch  = fixture.CreateMany<Order>(50); // explicit count
fixture.RepeatCount = 5;                    // change global default
```

Complex graphs are walked end-to-end — nested objects, collection properties, every layer non-default. `[StringLength]` / `[Range]` from `System.ComponentModel.DataAnnotations` are honored automatically; generated values land inside the declared bounds. See [templates/autofixture/basics-usage.cs](../templates/autofixture/basics-usage.cs) for the BCL/special-type matrix and [templates/autofixture/basics-complex-objects.cs](../templates/autofixture/basics-complex-objects.cs) for nested objects, collections, enums, and recursion handling.

### Fine-grained control: `Build<T>`

When the test needs to pin a few values but doesn't care about the rest:

| Method                  | Purpose                                |
| ----------------------- | -------------------------------------- |
| `.With(prop, value)`    | Set a fixed value                      |
| `.With(prop, factory)`  | Set via a per-call factory             |
| `.Without(prop)`        | Skip the property (leave default)      |
| `.OmitAutoProperties()` | Only the properties you set get values |

```csharp
var vip = fixture.Build<Customer>()
    .With(c => c.Tier, CustomerTier.Vip)
    .With(c => c.CreditLimit, 100_000m)
    .Without(c => c.InternalId)
    .Create();
```

The anonymous-testing rule: pin the values the assertion depends on; let everything else stay random. If a test asserts `result.Status == Processed` and the SUT only branches on `order.Total > 0`, pin `Total`, not `Status`, not anything else.

### Recursion

Circular references (`Category.Parent`, `Employee.Manager`) throw `ObjectCreationException` by default. Swap the recursion behavior once, ideally on a shared base fixture:

```csharp
public abstract class AutoFixtureTestBase
{
    protected IFixture Fixture { get; }

    protected AutoFixtureTestBase()
    {
        Fixture = new Fixture();
        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));
        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }
}
```

After this, recursive properties become `null` or empty collections instead of crashing the run.

## Customization (`ISpecimenBuilder`)

`Customize<T>` registers a per-type rule that every subsequent `Create<T>` will follow. Put these in the constructor of a test class (or in a shared `ICustomization`) so each test inherits the rule without restating it.

```csharp
Fixture.Customize<Order>(c => c
    .With(o => o.Status, OrderStatus.Created)
    .With(o => o.OrderNumber, () => $"ORD-{Random.Shared.Next(1000, 9999)}"));
```

A common trap: `.With(x => x.Age, Random.Shared.Next(30, 50))` evaluates `Next` *once* — every generated object then shares that single value. Use a lambda to get a fresh value per object: `.With(x => x.Age, () => Random.Shared.Next(30, 50))`. Prefer `Random.Shared` over `new Random()`: thread-safe, no allocation, no duplicate-seed gotchas. See [templates/autofixture/customization-dataannotations.cs](../templates/autofixture/customization-dataannotations.cs) for the fixed-vs-factory contrast and thread-safety check.

`Customize<T>` is per-type. When the rule applies to *any* type with a particular property (every `Email` property anywhere in the object graph, every `DateTime` named `UpdateTime`, every `int` matching a predicate) — implement `ISpecimenBuilder`. The contract: return a value when you can handle the request, return `new NoSpecimen()` to defer to the rest of the pipeline.

```csharp
public sealed class EmailSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo pi &&
            pi.PropertyType == typeof(string) &&
            pi.Name.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            return $"user{Guid.NewGuid():N}@example.com";
        }
        return new NoSpecimen();
    }
}
```

[templates/autofixture/customization-isb.cs](../templates/autofixture/customization-isb.cs) shows the `RandomRangedDateTimeBuilder` (target by property name), the predicate-driven int builder, and the `NoSpecimen` chain-of-responsibility contract. [templates/autofixture/customization-numeric-range.cs](../templates/autofixture/customization-numeric-range.cs) extends that to a generic `NumericRangeBuilder<TValue>` (int/long/decimal/double/float/short/byte), and a `DateTimeRangeBuilder`, wired through fluent `fixture.AddRandomRange<T, TValue>(...)` / `fixture.AddDateTimeRange<T>(...)` extensions.

### Priority: `Insert(0)` vs `Add()`

AutoFixture matches `Customizations` in order. Built-in builders (`NumericSequenceGenerator`, `RangeAttributeRelay`) sit at the front of the chain and will intercept numeric requests before your builder ever runs. For numeric customizations, insert at the head:

```csharp
fixture.Customizations.Add(new MyNumericBuilder(...));       // may be intercepted for int/long
fixture.Customizations.Insert(0, new MyNumericBuilder(...)); // guarantees first shot
```

| Type                       | Built-in interceptors                             | Effect of `Add()`                       |
| -------------------------- | ------------------------------------------------- | --------------------------------------- |
| `int`                      | `RangeAttributeRelay`, `NumericSequenceGenerator` | Builder may never run — use `Insert(0)` |
| `decimal`/`double`/`float` | `NumericSequenceGenerator`                        | Same — use `Insert(0)`                  |
| `DateTime`                 | none specific                                     | `Add()` works                           |
| properties                 | resolved late                                     | `Add()` usually works                   |

Group related rules into a class so the same fixture configuration can be reused across multiple test projects:

```csharp
public sealed class DomainCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Order>(c => c.With(o => o.Status, OrderStatus.Created));
        fixture.Customizations.Insert(0, new EmailSpecimenBuilder());
    }
}

var fixture = new Fixture().Customize(new DomainCustomization());
```

## NSubstitute integration

`AutoFixture.AutoNSubstitute` teaches the fixture how to create substitutes for interfaces and abstract classes on demand. When `Create<MyService>()` walks the constructor and hits `IRepository`, it calls `Substitute.For<IRepository>()` automatically and injects it.

```csharp
var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
var sut = fixture.Create<OrderService>();   // every interface dependency is a substitute
```

The same instance is reused for downstream requests of the same interface, so the SUT and the test both see the same substitute — provided you mark it `[Frozen]`.

### `[Frozen]` — reuse one substitute across parameters

By default a fresh substitute is created per request. To pin one and reuse it (so you can configure it *and* see the SUT use it), mark the parameter `[Frozen]`:

```csharp
[Theory, AutoDataWithCustomization]
public async Task GetOrderAsync_WhenOrderExists_ShouldReturnOrder(
    [Frozen] IOrderRepository repository,
    OrderService sut,                       // sut.repository == repository
    Order existing)
{
    repository.GetAsync(existing.Id).Returns(existing);

    var result = await sut.GetOrderAsync(existing.Id);

    result.Should().BeSameAs(existing);
}
```

Parameter order matters: `[Frozen]` must appear **before** the SUT in the parameter list. If the SUT is constructed first, it captures a different instance and your `.Returns(...)` calls never affect the code under test.

```csharp
[Theory, AutoDataWithCustomization]
public void Wrong(OrderService sut, [Frozen] IOrderRepository repo)   // BAD: frozen too late
public void Right([Frozen] IOrderRepository repo, OrderService sut)   // GOOD
```

The full pattern catalogue — multiple frozen dependencies, frozen + auto-generated data, frozen + `IFixture`, verification patterns (`Received`, `DidNotReceive`, `Arg.Is<>`, captured args), sequential returns, conditional returns, exception simulation — lives in [templates/autofixture/nsubstitute-frozen.cs](../templates/autofixture/nsubstitute-frozen.cs).

### Real dependencies (mappers, validators)

Don't substitute `IMapper`. A substituted mapper returns `null` for every call and you end up either configuring every projection by hand or asserting against `null`. Register a real one via an `ICustomization`:

```csharp
public sealed class MapsterMapperCustomization : ICustomization
{
    private IMapper? _mapper;
    public void Customize(IFixture fixture) => fixture.Register(() => Mapper);

    private IMapper Mapper => _mapper ??= BuildMapper();
    private static IMapper BuildMapper()
    {
        var config = new TypeAdapterConfig();
        config.Scan(typeof(ServiceMapRegister).Assembly);
        return new Mapper(config);
    }
}
```

The same shape works for AutoMapper (`new MapperConfiguration(cfg => cfg.AddMaps(...)).CreateMapper()`) and for FluentValidation validators (`fixture.Inject<IValidator<T>>(new TValidator())`). Rule of thumb: if the dependency is a *tool* whose own behavior the test depends on (mapping, validation), use the real one. If it is a *collaborator* the test wants to observe (repository, gateway), substitute it. The full Mapster + AutoMapper + domain customization stack — including a `CompositeCustomization` — is in [templates/autofixture/nsubstitute-custom-autodata.cs](../templates/autofixture/nsubstitute-custom-autodata.cs).

## Bogus integration

AutoFixture gives you *scaffolded* data (`Email1a2b3c4d`); Bogus gives you *realistic* data (`john.doe@example.com`). The integration uses `ISpecimenBuilder` to delegate specific properties or types to a Bogus `Faker`. See [reference/bogus.md](bogus.md) for the Bogus side of the equation.

```csharp
public sealed class EmailSpecimenBuilder : ISpecimenBuilder
{
    private static readonly Faker _faker = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo pi &&
            pi.PropertyType == typeof(string) &&
            pi.Name.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return _faker.Internet.Email();
        }
        return new NoSpecimen();
    }
}
```

| Builder                      | Matches                                      | Bogus call              |
| ---------------------------- | -------------------------------------------- | ----------------------- |
| `EmailSpecimenBuilder`       | property name contains `Email`               | `Internet.Email()`      |
| `PhoneSpecimenBuilder`       | property name contains `Phone`               | `Phone.PhoneNumber()`   |
| `NameSpecimenBuilder`        | `FirstName` / `LastName` / `FullName`        | `Person.FirstName` etc. |
| `AddressSpecimenBuilder`     | `Street` / `City` / `PostalCode` / `Country` | `Address.*`             |
| `WebsiteSpecimenBuilder`     | property name contains `Website`             | `Internet.Url()`        |
| `CompanyNameSpecimenBuilder` | `Name` on a `Company` type                   | `Company.CompanyName()` |

Register them once via an extension so tests opt in with one line:

```csharp
public static class FixtureExtensions
{
    public static IFixture WithBogus(this IFixture fixture)
    {
        fixture.WithOmitOnRecursion();
        fixture.Customizations.Insert(0, new EmailSpecimenBuilder());
        fixture.Customizations.Insert(0, new PhoneSpecimenBuilder());
        fixture.Customizations.Insert(0, new NameSpecimenBuilder());
        fixture.Customizations.Insert(0, new AddressSpecimenBuilder());
        return fixture;
    }
}

var fixture = new Fixture().WithBogus();
var user = fixture.Create<User>(); // user.Email is "jane.smith@…", user.Phone is real-looking
```

The full property-level and type-level builder set, plus the localized `Faker("zh_TW")` variant, is in [templates/autofixture/bogus-specimen-builders.cs](../templates/autofixture/bogus-specimen-builders.cs). A `HybridTestDataGenerator` (uniform `ITestDataGenerator` surface), the `Fixture.WithBogus()/WithBogusFor<T>()/WithSeed()` extensions, and `BogusAutoDataAttribute` / `SeededBogusAutoDataAttribute` live in [templates/autofixture/bogus-hybrid-generator.cs](../templates/autofixture/bogus-hybrid-generator.cs).

### Seeds and reproducibility

AutoFixture has its own pseudo-random source; Bogus has its own. Even with both seeded (`Bogus.Randomizer.Seed = new Random(n)`), you cannot guarantee identical *values* across runs because the two libraries interleave differently. You can guarantee consistent *behavior* (the test passes deterministically). For tests that need byte-identical data, stay inside one library.

## AutoData / Theory

`[AutoData]` (from `AutoFixture.Xunit2`) injects every `[Theory]` parameter from a fixture. `[InlineAutoData]` combines fixed values with auto-generated ones. `[MemberAutoData]` pulls fixed values from a static member; everything else is auto-generated.

```csharp
[Theory, AutoData]
public void Create_WhenInputIsValid_ShouldGenerateAllParameters(Order order, Customer customer)
{
    order.Should().NotBeNull();
    customer.Should().NotBeNull();
}

[Theory]
[InlineAutoData("VIP", 100_000)]
[InlineAutoData("Standard", 10_000)]
public void CalculateLimit_ByTier_ShouldMatchExpected(
    string tier,            // fixed
    decimal expectedLimit,  // fixed
    Customer customer)      // generated
{
    customer.Tier = tier;
    CalculateLimit(customer).Should().Be(expectedLimit);
}
```

Fixed parameters must come first and only accept compile-time constants. `[InlineAutoData("VIP", SomeConst)]` works; `[InlineAutoData("VIP", 100 * 1000)]` does not. For dynamic values, use `[MemberAutoData]` against a static member. For null parameters in `[InlineAutoData]`, use `null!` and a nullable signature so the compiler accepts both the attribute argument and the parameter:

```csharp
[Theory]
[InlineAutoDataWithCustomization(null!, null!)]
[InlineAutoDataWithCustomization("", "")]
public async Task SearchAsync_WhenAllEmpty_ShouldThrow(
    string? companyName, string? phone, ShipperService sut)
{
    await Assert.ThrowsAsync<ArgumentException>(() => sut.SearchAsync(companyName!, phone!));
}
```

For real projects, subclass `AutoDataAttribute` once and use it everywhere — this is where you wire NSubstitute, mappers, and domain customizations together. Note that `InlineAutoDataAttribute` expects an `AutoDataAttribute` *instance*, not a fixture factory — pass `new AutoDataWithCustomizationAttribute()` so the inline form picks up exactly the same customizations as the plain form. See the full attribute family — `AutoData`, `InlineAutoData`, `MemberAutoData`, custom `DomainAutoData`/`BusinessAutoData`, `CompositeAutoData` stacking — in [templates/autofixture/autodata-attributes.cs](../templates/autofixture/autodata-attributes.cs).

### `[CollectionSize]`

`CreateMany<T>()` returns three items by default. A `CollectionSizeAttribute` subclass — derived from `CustomizeAttribute` — overrides the count per parameter; alternatively set `fixture.RepeatCount = n` on the fixture itself.

```csharp
[Theory, AutoData]
public void Sized([CollectionSize(5)] List<Product> products) =>
    products.Should().HaveCount(5);
```

The attribute implementation (with `FilteringSpecimenBuilder` + `FixedBuilder` + `EqualRequestSpecification`) is in [templates/autofixture/autodata-collection-size.cs](../templates/autofixture/autodata-collection-size.cs).

### `CompositeAutoData` — stack multiple customization sources

When two `AutoDataAttribute` subclasses each carry useful rules, a `CompositeAutoData` reflectively merges their customizations so a single `[Theory]` picks up both:

```csharp
[Theory]
[CompositeAutoData(typeof(DomainAutoDataAttribute), typeof(BusinessAutoDataAttribute))]
public void StackedCustomizations(Person p, Product pr, Order o) { /* ... */ }
```

Keep the stack shallow — two, maybe three sources at most. Past that, readers can't tell where a value came from.

## Pitfalls

- **AutoFixture for tests that need a specific value.** If the test asserts on a specific business outcome, pin the inputs that drive that outcome. AutoFixture is for filler, not for the inputs that matter.
- **Forgetting `AutoNSubstituteCustomization`.** Without it, asking the fixture for an interface throws `ObjectCreationException`. Add the customization or build the substitute manually.
- **Wrong parameter order with `[Frozen]`.** The SUT must come *after* every frozen dependency it consumes.
- **Recursive object graphs.** Apply `OmitOnRecursionBehavior` on a shared base, not once per test.
- **`Random.Shared.Next(...)` evaluated eagerly.** Wrap in a lambda for per-call generation.
- **`Add()` instead of `Insert(0)` for numeric builders.** Built-in `NumericSequenceGenerator` intercepts numeric requests first; the custom builder never runs.
- **Substituting `IMapper` / `IValidator<T>`.** Both are tools; register the real instance instead.
- **Hand-crafted builders that AutoFixture would handle.** If a Test Data Builder is mostly setting properties to default-shaped values, replace it with `Build<T>().With(...)`. See [reference/builder-pattern.md](builder-pattern.md) for the inverse case (when a builder beats AutoFixture: heavy domain semantics, validation invariants, multi-step construction).
- **Over-customizing the fixture.** If half the codebase has to read `DomainCustomization` to understand a test, the test is no longer self-explanatory. Pull pinned values back into the test itself.
- **Expecting cross-library reproducibility.** AutoFixture+Bogus tests can be deterministic in *behavior* without being deterministic in *values*. If exact bytes matter, stay inside one library.
- **Returning `null` from an `ISpecimenBuilder`.** Always return `new NoSpecimen()` to defer to the chain — returning `null` cascades nulls through the rest of the object.

## Templates

- [templates/autofixture/basics-usage.cs](../templates/autofixture/basics-usage.cs) — `Create<T>`, `CreateMany<T>`, `Build<T>`, `OmitAutoProperties()`.
- [templates/autofixture/basics-complex-objects.cs](../templates/autofixture/basics-complex-objects.cs) — nested graphs, collections, enums, recursion handling, `AutoFixtureTestBase`.
- [templates/autofixture/basics-xunit-integration.cs](../templates/autofixture/basics-xunit-integration.cs) — shared fixture, `[Theory]` with `Build<T>`, `MemberData`, DTO validation, batch.
- [templates/autofixture/customization-dataannotations.cs](../templates/autofixture/customization-dataannotations.cs) — `[StringLength]`/`[Range]` auto-recognition, fixed vs factory, `Random.Shared`.
- [templates/autofixture/customization-isb.cs](../templates/autofixture/customization-isb.cs) — `RandomRangedDateTimeBuilder`, predicate-driven int builder, `NoSpecimen` chain.
- [templates/autofixture/customization-numeric-range.cs](../templates/autofixture/customization-numeric-range.cs) — generic `NumericRangeBuilder<TValue>` + `DateTimeRangeBuilder`, fluent `AddRandomRange` extensions.
- [templates/autofixture/nsubstitute-frozen.cs](../templates/autofixture/nsubstitute-frozen.cs) — `[Frozen]` patterns: basic, multiple, with `IFixture`, verification, sequential/conditional returns.
- [templates/autofixture/nsubstitute-custom-autodata.cs](../templates/autofixture/nsubstitute-custom-autodata.cs) — `AutoDataWithCustomization`, Mapster + AutoMapper customizations, `CompositeCustomization`.
- [templates/autofixture/bogus-specimen-builders.cs](../templates/autofixture/bogus-specimen-builders.cs) — property-level + type-level Bogus builders, localized variant.
- [templates/autofixture/bogus-hybrid-generator.cs](../templates/autofixture/bogus-hybrid-generator.cs) — `HybridTestDataGenerator`, `FixtureExtensions.WithBogus()`, `BogusAutoDataAttribute`.
- [templates/autofixture/autodata-attributes.cs](../templates/autofixture/autodata-attributes.cs) — `AutoData`, `InlineAutoData`, `MemberAutoData`, custom + `CompositeAutoData`.
- [templates/autofixture/autodata-collection-size.cs](../templates/autofixture/autodata-collection-size.cs) — `CollectionSizeAttribute` implementation.

## Cross-references

- [reference/bogus.md](bogus.md) — realistic data generation that pairs with AutoFixture via SpecimenBuilders.
- [reference/nsubstitute.md](nsubstitute.md) — base substitute API; `[Frozen]` builds on it.
- [reference/builder-pattern.md](builder-pattern.md) — the hand-rolled alternative; use when domain semantics dominate.
- [reference/awesome-assertions.md](awesome-assertions.md) — fluent assertions that pair naturally with auto-generated data.
- [reference/xunit-setup.md](xunit-setup.md) — `Theory`/`InlineData` plumbing that `AutoData` extends.
