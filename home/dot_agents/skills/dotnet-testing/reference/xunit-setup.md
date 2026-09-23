# xUnit project setup

How a fresh test project is laid out, what goes in its `.csproj`, and how it relates to the production project. For a one-shot scaffold, run `scripts/new-test-project.ps1` — it consults the consumer repo's `Directory.Packages.props` and produces the layout below. The rest of this document explains what that script generates and why.

Three-part naming and FIRST apply — see SKILL.md.

## Layout

```text
MyProject/
├── src/
│   └── MyProject.Core/
│       ├── MyProject.Core.csproj
│       ├── Calculator.cs
│       ├── Models/
│       ├── Services/
│       │   ├── IOrderService.cs
│       │   └── OrderService.cs
│       └── Repositories/
├── tests/
│   └── MyProject.Core.Tests/
│       ├── MyProject.Core.Tests.csproj
│       ├── CalculatorTests.cs
│       ├── Models/
│       ├── Services/
│       │   └── OrderServiceTests.cs
│       └── Fixtures/
│           ├── DatabaseFixture.cs
│           └── TestDataFixture.cs
└── MyProject.sln
```

Rules:

1. **Separate `src/` and `tests/` trees** at the repo root.
2. **Test project name mirrors the production project**: `MyProject.Core` -> `MyProject.Core.Tests`.
3. **Folder structure inside the test project mirrors the production project** — `src/.../Services/Foo.cs` is tested by `tests/.../Services/FooTests.cs`.
4. **One test project per production project**. Don't merge multiple production projects into a single test project.
5. **One test class per production class**: `Calculator.cs` -> `CalculatorTests.cs`.
6. **Avoid deep nesting.** Three or four levels under the project root is usually enough.

**Full example:** [templates/xunit-setup/project-structure.md](../templates/xunit-setup/project-structure.md)

### Multi-project solutions

Larger solutions follow the same pattern — one production project to one test project, plus a dedicated `*.Integration.Tests` when integration suites get large enough to want their own dependencies (Testcontainers, `Aspire.Hosting.Testing`). See [reference/testcontainers.md](testcontainers.md) and [reference/aspire.md](aspire.md).

### Naming for non-trivial repos

For projects that grow beyond a single test type, make the test category explicit in the name:

```text
tests/
├── MyProject.Core.Test.Unit/                # Unit tests, milliseconds
├── MyProject.WebApi.Test.Unit/
└── MyProject.WebApi.Test.Integration/       # WebApplicationFactory / Testcontainers
```

| Suffix                     | Purpose                                           | Typical cost       |
| -------------------------- | ------------------------------------------------- | ------------------ |
| `*.Tests` or `*.Test.Unit` | Unit tests — no external resources                | Milliseconds       |
| `*.Test.Integration`       | Multi-component, may touch DB/HTTP/Testcontainers | Seconds            |
| `*.Acceptance.Tests`       | End-to-end behaviour                              | Seconds to minutes |
| `*.Performance.Tests`      | Throughput/latency benchmarks                     | Variable           |

CI can split fast vs slow phases off these suffixes:

```powershell
dotnet test --filter "FullyQualifiedName~.Test.Unit"          # fast gate
dotnet test --filter "FullyQualifiedName~.Test.Integration"   # full verification
```

## Standard `.csproj`

A minimal-but-conventional test project — no version pins, CPM owns those:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>

    <PackageReference Include="AwesomeAssertions" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="AutoFixture" />
    <PackageReference Include="AutoFixture.AutoNSubstitute" />
    <PackageReference Include="AutoFixture.Xunit2" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\MyProject.Core\MyProject.Core.csproj" />
  </ItemGroup>
</Project>
```

**Full example:** [templates/xunit-setup/xunit-test-project.csproj](../templates/xunit-setup/xunit-test-project.csproj)

Notes:

- No `Version="…"` attributes — Central Package Management owns versions in `Directory.Packages.props`. Hard-coding here causes drift.
- `xunit.runner.visualstudio` and `coverlet.collector` use `PrivateAssets=all` so they don't leak into downstream references.
- `<Using Include="Xunit" />` removes `using Xunit;` from every test file (the repo's `ImplicitUsings` is `enable`).

| Property                | Effect                                                           |
| ----------------------- | ---------------------------------------------------------------- |
| `IsPackable=false`      | Test projects must never produce a NuGet package.                |
| `IsTestProject=true`    | Marks the project so test runners and `dotnet test` discover it. |
| `Nullable=enable`       | NRT enabled — tests catch null-handling regressions.             |
| `ImplicitUsings=enable` | Allows the `<Using Include="Xunit" />` shortcut.                 |

### Why each core package is here

| Package                                       | What it does                                                                      |
| --------------------------------------------- | --------------------------------------------------------------------------------- |
| `xunit`                                       | The framework — `[Fact]`, `[Theory]`, the `Assert` class.                         |
| `xunit.runner.visualstudio`                   | Test Explorer integration for VS Code, Visual Studio, and Rider.                  |
| `Microsoft.NET.Test.Sdk`                      | The .NET test platform — what makes `dotnet test` discover and run tests.         |
| `coverlet.collector`                          | Code coverage — see [reference/coverage.md](coverage.md).                         |
| `AwesomeAssertions`                           | Fluent assertions — see [reference/awesome-assertions.md](awesome-assertions.md). |
| `NSubstitute`                                 | Test doubles — see [reference/nsubstitute.md](nsubstitute.md).                    |
| `AutoFixture` (+ `Xunit2`, `AutoNSubstitute`) | Anonymous data + `[AutoData]` — see [reference/autofixture.md](autofixture.md).   |

### Optional `xunit.runner.json`

Drop next to the `.csproj`, set `<None Update="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />`, and configure runtime knobs:

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": true,
  "maxParallelThreads": -1,
  "diagnosticMessages": false
}
```

Defaults are usually fine; reach for this when you need to serialise a particular collection or change the parallelism story. For an upgrade-ready variant see [reference/xunit-upgrade.md](xunit-upgrade.md).

## Creating a project from scratch

```powershell
# 1. Solution
dotnet new sln -n MyProject

# 2. Production class library
dotnet new classlib -n MyProject.Core -o src/MyProject.Core

# 3. xUnit test project
dotnet new xunit -n MyProject.Core.Tests -o tests/MyProject.Core.Tests

# 4. Wire both into the solution
dotnet sln add src/MyProject.Core/MyProject.Core.csproj
dotnet sln add tests/MyProject.Core.Tests/MyProject.Core.Tests.csproj

# 5. Test project references the production project (only this direction)
dotnet add tests/MyProject.Core.Tests/MyProject.Core.Tests.csproj \
  reference src/MyProject.Core/MyProject.Core.csproj

# 6. Add the rest of the convention packages
cd tests/MyProject.Core.Tests
dotnet add package AwesomeAssertions
dotnet add package NSubstitute
dotnet add package AutoFixture
dotnet add package AutoFixture.AutoNSubstitute
dotnet add package AutoFixture.Xunit2
dotnet add package coverlet.collector
```

If the repo uses Central Package Management, omit explicit versions on `dotnet add package` and pin everything in `Directory.Packages.props` instead.

Visual Studio / Rider follow the same shape: File -> New -> "xUnit Test Project (.NET)", drop into `tests/{Production}.Tests`, then add a project reference to the production project. Add the AwesomeAssertions / NSubstitute / AutoFixture trio via the NuGet UI.

## xUnit's test lifecycle

For each test method:

1. xUnit creates a **new instance** of the test class.
2. The **constructor** runs (use it for per-test arrange).
3. The **test method** runs.
4. If the class implements `IDisposable` / `IAsyncDisposable`, **`Dispose` runs**.

```csharp
public sealed class CalculatorTests : IDisposable
{
    private readonly Calculator _calculator;

    public CalculatorTests()
    {
        _calculator = new Calculator();   // fresh per test
    }

    [Fact]
    public void Add_WhenGivenTwoPositiveIntegers_ShouldReturnSum()
    {
        // Arrange
        var a = 5;
        var b = 3;

        // Act
        var result = _calculator.Add(a, b);

        // Assert
        result.Should().Be(8);
    }

    public void Dispose() { /* tidy per-test */ }
}
```

This is what gives you the *Independent* in FIRST without any effort. The moment you put state in a `static` field or share an `IClassFixture` with mutable state, you have thrown that gift away.

Cross-test shared setup belongs in `IClassFixture<T>` (single class) or `ICollectionFixture<T>` (cross-class) — and **only when the fixture is immutable after construction**. A mutable fixture is a flake factory.

## Project reference direction

```text
Test project  ->  Production project    OK
Production project  ->  Test project    NEVER
```

If the production project needs to expose `internal` types to the test project:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="MyProject.Core.Tests" />
</ItemGroup>
```

This is a controlled, file-level escape hatch — it does not authorise the test to reach into `private` members via reflection. See the absolute bans in the umbrella SKILL.md.

## Test class naming and folder layout

| Production class | Test class            | File                     |
| ---------------- | --------------------- | ------------------------ |
| `Calculator`     | `CalculatorTests`     | `CalculatorTests.cs`     |
| `OrderService`   | `OrderServiceTests`   | `OrderServiceTests.cs`   |
| `UserRepository` | `UserRepositoryTests` | `UserRepositoryTests.cs` |

Folder layout in the test project mirrors the production project's folder layout so a contributor finding `src/MyProject.Core/Services/OrderService.cs` knows the tests live at `tests/MyProject.Core.Tests/Services/OrderServiceTests.cs`.

A `Fixtures/` folder under the test project holds shared fixture classes (`IClassFixture<T>` / `ICollectionFixture<T>` types, builders for fakers, etc.) that aren't tied to one test class. Builder classes themselves live in `Builders/` — see [reference/builder-pattern.md](builder-pattern.md).

## Running tests

```powershell
dotnet test                                                    # whole solution
dotnet test tests/MyProject.Core.Tests/                        # one project
dotnet test --filter "FullyQualifiedName~CalculatorTests"      # one class
dotnet test --filter "FullyQualifiedName~Add_WhenGiven1And2"   # one test
dotnet test --collect:"XPlat Code Coverage"                    # with coverage
dotnet test --verbosity detailed                               # noisy output
```

IDE integration (VS Code C# Dev Kit, Visual Studio Test Explorer, Rider's Unit Tests window) discovers tests automatically once `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` are referenced and the project builds.

## Troubleshooting

| Symptom                                   | Likely cause                                                                                                                                           |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Test Explorer is empty                    | Missing `xunit.runner.visualstudio` or `Microsoft.NET.Test.Sdk`; `bin/`/`obj/` is stale — clean and rebuild.                                           |
| `dotnet test` finds them, IDE does not    | IDE extension missing (C# Dev Kit for VS Code), or `IsTestProject` not set.                                                                            |
| Tests pass alone, fail in parallel        | Shared mutable state — usually a `static` field or `IClassFixture` carrying mutable data.                                                              |
| "No tests are available in this assembly" | Project compiled but `[Fact]`/`[Theory]` not present, or `xunit` reference broken. Confirm the `<Using Include="Xunit" />` or explicit `using Xunit;`. |
| Internal types not visible to tests       | Missing `<InternalsVisibleTo Include="MyProject.Core.Tests" />` in the production csproj.                                                              |

## Checklist

- [ ] Project under `tests/`, named `{Production}.Tests` (or `{Production}.Test.Unit`).
- [ ] `IsPackable=false`, `IsTestProject=true`, `Nullable=enable`.
- [ ] `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` referenced.
- [ ] AwesomeAssertions, NSubstitute, AutoFixture (`+ Xunit2`, `+ AutoNSubstitute`) referenced.
- [ ] No `Version="…"` attributes — CPM owns versions.
- [ ] Reference to the production project from test project, not the reverse.
- [ ] Test folder layout mirrors production folder layout.
- [ ] `dotnet test` succeeds.
- [ ] IDE discovers tests.

## Sibling references

[reference/fundamentals.md](fundamentals.md) · [reference/naming.md](naming.md) · [reference/builder-pattern.md](builder-pattern.md) · [reference/awesome-assertions.md](awesome-assertions.md) · [reference/nsubstitute.md](nsubstitute.md) · [reference/autofixture.md](autofixture.md) · [reference/coverage.md](coverage.md) · [reference/xunit-upgrade.md](xunit-upgrade.md)
