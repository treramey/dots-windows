# TUnit

A source-generated, parallel-by-default .NET test framework. Alternative to xUnit when the suite is dominated by parameterized tests, when source-generation startup wins matter, or when you want first-class DI and lifecycle hooks. The shared rules from `SKILL.md` still apply — FIRST, 3A, three-part naming — but the attribute set and lifecycle hooks differ from xUnit.

## When to use

The repo default is xUnit ([reference/xunit-setup.md](xunit-setup.md)). TUnit is a deliberate deviation, not a casual swap. Reasons to evaluate it:

- The suite is dominated by `[Theory]` cases (thousands), and source-generated discovery would meaningfully cut startup time.
- You want true per-test parallelism by default (xUnit parallelism is per collection; TUnit is per test) and the tests genuinely are independent.
- You want DI-resolved test constructors without `IClassFixture<T>` ceremony — TUnit ships a Microsoft.Extensions.DependencyInjection bridge.
- You want first-class lifecycle hooks at Assembly / Class / Test scope without a fixture-marker dance.
- You are starting a fresh test project, are on .NET 8+, and have explicit license to deviate from the repo-default stack.
- AOT compilation of the test binary matters (CI startup, container deploys). TUnit is AOT-compatible; xUnit historically is not.

Do **not** migrate an existing xUnit suite to TUnit without a written reason. The package set diverges (the NSubstitute and AutoFixture integration patterns differ), and there is no neutral switch cost. See "Migration from xUnit" below for the mapping when you do have a reason.

### Reported performance (upstream)

| Scenario       | xUnit    | TUnit (source-gen) | TUnit (AOT) |
| -------------- | -------- | ------------------ | ----------- |
| Simple tests   | ~1400 ms | ~1000 ms           | ~60 ms      |
| Async tests    | ~1400 ms | ~930 ms            | ~26 ms      |
| Parallel suite | ~1425 ms | ~999 ms            | ~54 ms      |

Numbers are illustrative — measure on your own suite before adopting.

## Package set

Pinned in `Directory.Packages.props`:

```xml
<PackageVersion Include="TUnit" Version="..." />
<PackageVersion Include="Microsoft.Testing.Extensions.CodeCoverage" Version="..." />
<PackageVersion Include="Microsoft.Testing.Extensions.TrxReport" Version="..." />
```

In the test `.csproj`:

```xml
<PackageReference Include="TUnit" />
<PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
<PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
```

**Do not** install `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`. TUnit builds on `Microsoft.Testing.Platform`, not VSTest, and the legacy SDK package will conflict.

Full project file template: [templates/tunit/fundamentals-project.csproj](../templates/tunit/fundamentals-project.csproj). Global usings: [templates/tunit/fundamentals-globalusings.cs](../templates/tunit/fundamentals-globalusings.cs).

### Project bootstrap

```bash
# Manual: start from a console template (TUnit uses the dotnet run entry point).
dotnet new console -n MyApp.Tests -o tests/MyApp.Tests

# Or: TUnit ships a project template.
dotnet new install TUnit.Templates
dotnet new tunit -n MyApp.Tests -o tests/MyApp.Tests
```

## Basic syntax

Every TUnit test method **must be async** (`async Task`). Synchronous test methods will not compile. Assertions are awaited.

```csharp
[Test]
public async Task Add_When1And2_ShouldReturn3()
{
    var sut = new Calculator();
    var result = sut.Add(1, 2);
    await Assert.That(result).IsEqualTo(3);
}
```

The fluent assertion API (`Assert.That(actual).IsXxx(expected)`) is the idiomatic TUnit style. It is **not** AwesomeAssertions — if you want `.Should()` semantics you can install AwesomeAssertions and mix the two, but the per-test cost is one extra package and a style split.

Common assertion families: equality (`IsEqualTo` / `IsNotEqualTo`), null (`IsNull` / `IsNotNull`), booleans (`IsTrue` / `IsFalse`), numerics (`IsGreaterThan`, `IsBetween`, `Within(tolerance)` for floats), strings (`Contains`, `StartsWith`, `EndsWith`, `IsEmpty`), collections (`HasCount`, `Contains`, `IsEmpty`), exceptions (`Throws<T>`, `WithMessage`, `DoesNotThrow`), composition (`And.`, `Or.`).

See [templates/tunit/fundamentals-assertions.cs](../templates/tunit/fundamentals-assertions.cs) for one-of-each examples, and [templates/tunit/fundamentals-basic-tests.cs](../templates/tunit/fundamentals-basic-tests.cs) for end-to-end test classes.

## Parameterized tests

`[Arguments(...)]` replaces xUnit's `[InlineData(...)]`. One `[Arguments]` row = one independent test case.

```csharp
[Test]
[Arguments(1, 2, 3)]
[Arguments(-1, 1, 0)]
[Arguments(0, 0, 0)]
public async Task Add_WithMultipleInputs_ShouldReturnExpectedSum(int a, int b, int expected)
{
    var sut = new Calculator();
    await Assert.That(sut.Add(a, b)).IsEqualTo(expected);
}
```

For more complex inputs (objects, computed values, file-driven scenarios) use `[MethodDataSource(nameof(GetData))]` where `GetData` returns `IEnumerable<(...)>` of strongly typed tuples. For combinatorial coverage, `[MatrixDataSource]` + `[Matrix(...)]` parameters auto-generate the cross product — be ruthless about case-count growth (a 5×4×3×6 matrix is 360 tests, which is almost always wrong).

> **TUnit 1.x change:** `ClassDataSource<T>` injects a single `T` instance now; it no longer enumerates `IEnumerable<T>`. To produce N test cases from a class, use `[MethodDataSource(typeof(DataClass), nameof(DataClass.GetData))]`. ClassDataSource is reserved for injecting a shared service / fixture instance.

Templates: [templates/tunit/advanced-data-sources.cs](../templates/tunit/advanced-data-sources.cs), [templates/tunit/advanced-matrix.cs](../templates/tunit/advanced-matrix.cs).

## Lifecycle hooks

Three scopes — Test, Class, Assembly — each with `[Before(...)]` and `[After(...)]` attributes:

| Hook                                         | Method shape   | Runs                                         |
| -------------------------------------------- | -------------- | -------------------------------------------- |
| `[Before(Test)]`                             | instance       | before every test                            |
| `[After(Test)]`                              | instance       | after every test                             |
| `[Before(Class)]`                            | static         | once before the first test in the class      |
| `[After(Class)]`                             | static         | once after the last test in the class        |
| `[Before(Assembly)]`                         | static         | once before any test in the assembly         |
| `[After(Assembly)]`                          | static         | once after every test in the assembly        |
| `[BeforeEvery(Test)]` / `[AfterEvery(Test)]` | static, global | once around every test in the assembly       |
| `IDisposable` / `IAsyncDisposable`           | instance       | between `[After(Test)]` and `[After(Class)]` |

Execution order for a typical class: `Before(Class)` → constructor → `Before(Test)` → test → `After(Test)` → `Dispose` → `After(Class)`.

```csharp
public class DatabaseLifecycleTests
{
    private static TestDatabase? _database;

    [Before(Class)]
    public static async Task ClassSetup() { _database = new TestDatabase(); await _database.InitializeAsync(); }

    [Before(Test)]
    public async Task TestSetup() => await _database!.ClearDataAsync();

    [Test]
    public async Task UserCreation_ShouldPersist() { /* ... */ }

    [After(Class)]
    public static async Task ClassTearDown() { if (_database != null) await _database.DisposeAsync(); }
}
```

Templates: [templates/tunit/fundamentals-lifecycle.cs](../templates/tunit/fundamentals-lifecycle.cs), and [templates/tunit/advanced-lifecycle-di.cs](../templates/tunit/advanced-lifecycle-di.cs) for combined lifecycle + DI cases.

## Dependency injection

TUnit ships a `DependencyInjectionDataSourceAttribute<TScope>` base. Implement it once against your DI container — typically `Microsoft.Extensions.DependencyInjection.IServiceScope` — and TUnit will resolve constructor parameters on test classes that opt in:

```csharp
public class MicrosoftDependencyInjectionDataSourceAttribute :
    DependencyInjectionDataSourceAttribute<IServiceScope>
{
    private static readonly IServiceProvider ServiceProvider = CreateSharedServiceProvider();

    public override IServiceScope CreateScope(DataGeneratorMetadata _) => ServiceProvider.CreateScope();
    public override object? Create(IServiceScope scope, Type type) => scope.ServiceProvider.GetService(type);

    private static IServiceProvider CreateSharedServiceProvider() =>
        new ServiceCollection()
            .AddSingleton<IOrderRepository, MockOrderRepository>()
            .AddTransient<OrderService>()
            .BuildServiceProvider();
}

[MicrosoftDependencyInjectionDataSource]
public class DependencyInjectionTests(OrderService orderService) { /* ... */ }
```

Compare with xUnit's `IClassFixture<T>` pattern: there is no marker interface, no separate fixture class, and constructor injection is type-checked against the registered services.

Full implementation: [templates/tunit/advanced-lifecycle-di.cs](../templates/tunit/advanced-lifecycle-di.cs).

## Parallel execution and ordering

TUnit runs every test in parallel by default, even within a single class. This is the headline feature, and it is also the easiest way to introduce flakiness when tests are not actually independent.

To force serialization, group tests with `[NotInParallel("GroupName")]`. Tests sharing the same group run one at a time; different groups still run in parallel relative to each other.

```csharp
[Test]
[NotInParallel("DatabaseTests")]
public async Task SerializedDatabaseTest1() { /* ... */ }

[Test]
[NotInParallel("DatabaseTests")]
public async Task SerializedDatabaseTest2() { /* ... */ }
```

Test dependencies (this test must run after that one) are not a first-class concept in TUnit and not a pattern this skill endorses — fix the test isolation instead. The shared `SKILL.md` rule "tests must be independently runnable" still applies.

## Execution control

- **`[Retry(n)]`** — retry on failure up to `n` more times. Use only for genuinely flaky external dependencies (network, file locks, container warm-up). Do not use it to paper over logic bugs.
- **`[Timeout(ms)]`** — fail the test if it runs longer than `ms` milliseconds. Pair with `Stopwatch` assertions to validate SLAs.
- **`[DisplayName("...")]`** — override the test name in reports. Supports `{0}`, `{1}`, … placeholders bound to `[Arguments]` parameters; produces business-language test reports without hurting symbol names.
- **`[Property("Key", "Value")]`** — tag the test for filtering.

Full template: [templates/tunit/advanced-execution-control.cs](../templates/tunit/advanced-execution-control.cs).

### Filtering

TUnit uses `dotnet run` (because it builds on `Microsoft.Testing.Platform`, not VSTest):

```bash
dotnet run --treenode-filter "/*/*/*/*[Category=Unit]"
dotnet run --treenode-filter "/*/*/*/*[Priority=High]"
dotnet run --treenode-filter "/*/*/*/*[(Category=Unit)&(Priority=High)]"
dotnet run --treenode-filter "/*/*/*/*[((Category=Unit)&(Priority=High))|(Suite=Smoke)]"
```

Path is `Assembly/Namespace/Class/Method`; property names and values are case-sensitive; composite conditions need explicit parentheses.

## ASP.NET Core integration

`WebApplicationFactory<Program>` works with TUnit identically to xUnit, except the test class implements `IDisposable` (or `IAsyncDisposable`) directly — no `IClassFixture` needed:

```csharp
public class WebApiIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public WebApiIntegrationTests() => _client = _factory.CreateClient();

    [Test]
    public async Task WeatherForecast_Get_ShouldReturnContent()
    {
        var response = await _client.GetAsync("/weatherforecast");
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }

    public void Dispose() { _client?.Dispose(); _factory?.Dispose(); }
}
```

The API project still needs `public partial class Program { }` at the bottom of `Program.cs`. Full template: [templates/tunit/advanced-aspnet-integration.cs](../templates/tunit/advanced-aspnet-integration.cs).

For containers under TUnit, `[Before(Assembly)]` / `[After(Assembly)]` is the idiomatic place to orchestrate Testcontainers — one network and several engines started once for the whole assembly. See [templates/tunit/advanced-testcontainers.cs](../templates/tunit/advanced-testcontainers.cs) and [reference/testcontainers.md](testcontainers.md).

## Migration from xUnit

When there is a written reason to migrate:

| xUnit                            | TUnit                                                                   |
| -------------------------------- | ----------------------------------------------------------------------- |
| `[Fact]`                         | `[Test]`                                                                |
| `[Theory]` + `[InlineData(...)]` | `[Test]` + `[Arguments(...)]`                                           |
| `[MemberData(nameof(Data))]`     | `[MethodDataSource(nameof(Data))]`                                      |
| Constructor setup                | `[Before(Test)]` (or keep constructor)                                  |
| `IDisposable.Dispose`            | `[After(Test)]` (or keep `Dispose`)                                     |
| `IClassFixture<T>`               | `[Before(Class)]` + static state, or `ClassDataSource<T>` for injection |
| `ICollectionFixture<T>`          | `[Before(Assembly)]` + static state                                     |
| `Assert.Equal(expected, actual)` | `await Assert.That(actual).IsEqualTo(expected)`                         |
| `Assert.True(value)`             | `await Assert.That(value).IsTrue()`                                     |
| `Assert.Null(value)`             | `await Assert.That(value).IsNull()`                                     |
| `Assert.Throws<T>(() => f())`    | `await Assert.That(() => f()).Throws<T>()`                              |
| `[Trait("Category", "Unit")]`    | `[Property("Category", "Unit")]`                                        |
| Skip via `[Fact(Skip = "...")]`  | `[Skip("...")]`                                                         |
| Async signature `async Task`     | unchanged — required                                                    |
| `dotnet test`                    | `dotnet run`                                                            |

Mechanical changes that almost always need to happen during migration:

1. Remove `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` from the test `.csproj`.
2. Replace `[Fact]` and `[Theory] + [InlineData]` with `[Test]` and `[Test] + [Arguments]`.
3. Make every test method `async Task` and `await` every assertion.
4. Replace `Assert.X(expected, actual)` with `await Assert.That(actual).IsX(expected)`. Mixed style with AwesomeAssertions is acceptable if the rewrite cost is too high.
5. Move `IClassFixture<T>` setup into `[Before(Class)]` static methods over static fields.
6. Adjust CI scripts: `dotnet run --treenode-filter ...` in place of `dotnet test --filter ...`.

For the reverse direction (TUnit → xUnit), the noun-mapping table reverses. See [reference/xunit-upgrade.md](xunit-upgrade.md) for general framework-version patterns.

## Pitfalls

- **Forgetting `await` on an assertion.** The assertion returns a `Task`; if you do not await it, the test passes silently regardless of the outcome. The TUnit analyzers catch most cases — keep them enabled.
- **Synchronous test method.** TUnit requires `async Task`. A `void` or non-async test will not be discovered.
- **Installing `Microsoft.NET.Test.Sdk`.** Conflicts with `Microsoft.Testing.Platform`; the test discoverer will misbehave or the build will fail.
- **Forgetting `IsTestProject`.** Without `<IsTestProject>true</IsTestProject>` in the `.csproj`, tooling does not detect the project as a test project and reports "0 tests".
- **Parallel-by-default surprises.** Tests that share a static field, a singleton mock, or a file path will start failing intermittently when migrated to TUnit. Reach for `[NotInParallel("Group")]` only as a last resort; fix the shared state first.
- **`ClassDataSource<T>` for case enumeration.** Common TUnit 1.x mistake. Use `[MethodDataSource(typeof(...))]` to produce N test cases from a class; reserve `ClassDataSource` for injecting a shared instance.
- **Enums in `[Matrix(...)]`.** Attribute literal rules disallow enum constants. Pass the integer values; TUnit converts back on the parameter.

## Cross-references

- [reference/xunit-setup.md](xunit-setup.md) — the default. Start here unless you have a written reason to deviate.
- [reference/xunit-upgrade.md](xunit-upgrade.md) — framework-version migration patterns; the noun mapping in the table above mirrors the same idea in reverse.
- [reference/awesome-assertions.md](awesome-assertions.md) — works inside TUnit tests if you prefer the `.Should()` API over `Assert.That`.
- [reference/nsubstitute.md](nsubstitute.md) — substitution still works under TUnit; AutoFixture's `AutoDataAttribute` does not apply.
- [reference/aspnet-integration.md](aspnet-integration.md) — `WebApplicationFactory` patterns translate directly.
- [reference/testcontainers.md](testcontainers.md) — `[Before(Assembly)]` is the natural place to orchestrate multi-engine containers.
