# xUnit test project structure examples

## Standard project structure

A complete .NET solution with an xUnit test project laid out the recommended way:

```text
MyProject/
│
├── MyProject.sln                         # Solution file
│
├── src/                                  # Production code
│   │
│   └── MyProject.Core/                   # Core business logic project
│       ├── MyProject.Core.csproj
│       │
│       ├── Models/                       # Data models
│       │   ├── User.cs
│       │   ├── Order.cs
│       │   └── Product.cs
│       │
│       ├── Services/                     # Business logic services
│       │   ├── IOrderService.cs
│       │   ├── OrderService.cs
│       │   ├── IUserService.cs
│       │   └── UserService.cs
│       │
│       ├── Repositories/                 # Data access layer
│       │   ├── IOrderRepository.cs
│       │   ├── OrderRepository.cs
│       │   ├── IUserRepository.cs
│       │   └── UserRepository.cs
│       │
│       └── Utilities/                    # Helper classes
│           ├── Calculator.cs
│           ├── StringHelper.cs
│           └── DateTimeHelper.cs
│
├── tests/                                # Test code
│   │
│   └── MyProject.Core.Tests/             # Test project for the core library
│       ├── MyProject.Core.Tests.csproj
│       │
│       ├── Models/                       # Mirrors src/Models/
│       │   ├── UserTests.cs
│       │   ├── OrderTests.cs
│       │   └── ProductTests.cs
│       │
│       ├── Services/                     # Mirrors src/Services/
│       │   ├── OrderServiceTests.cs
│       │   └── UserServiceTests.cs
│       │
│       ├── Repositories/                 # Mirrors src/Repositories/
│       │   ├── OrderRepositoryTests.cs
│       │   └── UserRepositoryTests.cs
│       │
│       ├── Utilities/                    # Mirrors src/Utilities/
│       │   ├── CalculatorTests.cs
│       │   ├── StringHelperTests.cs
│       │   └── DateTimeHelperTests.cs
│       │
│       └── Fixtures/                     # Shared test resources (IClassFixture etc.)
│           ├── DatabaseFixture.cs
│           └── TestDataFixture.cs
│
├── .gitignore
└── README.md
```

## Multi-project solution structure

For solutions with multiple production projects:

```text
MyCompany.MyProduct/
│
├── MyCompany.MyProduct.sln
│
├── src/
│   ├── MyCompany.MyProduct.Core/             # Core business logic
│   ├── MyCompany.MyProduct.Web/              # Web API
│   ├── MyCompany.MyProduct.Infrastructure/   # Infrastructure layer
│   └── MyCompany.MyProduct.Shared/           # Shared code
│
├── tests/
│   ├── MyCompany.MyProduct.Core.Tests/             # Unit tests for Core
│   ├── MyCompany.MyProduct.Web.Tests/              # Unit tests for Web
│   ├── MyCompany.MyProduct.Infrastructure.Tests/   # Unit tests for Infrastructure
│   └── MyCompany.MyProduct.Integration.Tests/      # Cross-project integration tests
│
├── docs/
│   ├── architecture.md
│   └── api-documentation.md
│
├── .editorconfig
├── .gitignore
├── Directory.Build.props                 # Shared MSBuild properties
├── Directory.Build.targets               # Shared MSBuild targets
└── README.md
```

## Single test class example

```csharp
// CalculatorTests.cs
using MyProject.Core.Utilities;

namespace MyProject.Core.Tests.Utilities;

/// <summary>
/// Unit tests for the Calculator class.
/// </summary>
public sealed class CalculatorTests : IDisposable
{
    private readonly Calculator _calculator;

    // Constructor — runs before every test method (per-test arrange)
    public CalculatorTests()
    {
        _calculator = new Calculator();
    }

    // Fact — single test case
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

    // Theory — parameterised test
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    public void Add_WhenGivenMultipleInputs_ShouldReturnExpected(int a, int b, int expected)
    {
        // Act
        var result = _calculator.Add(a, b);

        // Assert
        result.Should().Be(expected);
    }

    // Dispose — runs after every test method (per-test cleanup)
    public void Dispose()
    {
        // tidy per-test resources if any
    }
}
```

## Naming conventions

### Test project names

| Production project | Test project                      | Notes                       |
| ------------------ | --------------------------------- | --------------------------- |
| `MyProject.Core`   | `MyProject.Core.Tests`            | Unit tests                  |
| `MyProject.Web`    | `MyProject.Web.Tests`             | Web layer unit tests        |
| `MyProject.Web`    | `MyProject.Web.Integration.Tests` | Web layer integration tests |
| `MyProject`        | `MyProject.Acceptance.Tests`      | Acceptance tests            |
| `MyProject`        | `MyProject.Performance.Tests`     | Performance/benchmark tests |

### Test class names

| Production class | Test class            | File                     |
| ---------------- | --------------------- | ------------------------ |
| `Calculator`     | `CalculatorTests`     | `CalculatorTests.cs`     |
| `OrderService`   | `OrderServiceTests`   | `OrderServiceTests.cs`   |
| `UserRepository` | `UserRepositoryTests` | `UserRepositoryTests.cs` |
| `StringHelper`   | `StringHelperTests`   | `StringHelperTests.cs`   |

## Directory mirroring rule

```text
Production: src/MyProject.Core/Services/OrderService.cs
Tests:      tests/MyProject.Core.Tests/Services/OrderServiceTests.cs
```

Anyone finding a production file can predict the test file's location without searching.

## CLI scaffold to produce this layout

```powershell
# 1. Create the solution
dotnet new sln -n MyProject

# 2. Create the production project
dotnet new classlib -n MyProject.Core -o src/MyProject.Core

# 3. Create the test project
dotnet new xunit -n MyProject.Core.Tests -o tests/MyProject.Core.Tests

# 4. Wire into the solution
dotnet sln add src/MyProject.Core/MyProject.Core.csproj
dotnet sln add tests/MyProject.Core.Tests/MyProject.Core.Tests.csproj

# 5. Project reference (tests -> production)
dotnet add tests/MyProject.Core.Tests/MyProject.Core.Tests.csproj reference src/MyProject.Core/MyProject.Core.csproj

# 6. Create sub-folders in the production project
mkdir src/MyProject.Core/Models
mkdir src/MyProject.Core/Services
mkdir src/MyProject.Core/Repositories
mkdir src/MyProject.Core/Utilities

# 7. Mirror the structure in the test project
mkdir tests/MyProject.Core.Tests/Models
mkdir tests/MyProject.Core.Tests/Services
mkdir tests/MyProject.Core.Tests/Repositories
mkdir tests/MyProject.Core.Tests/Utilities
mkdir tests/MyProject.Core.Tests/Fixtures
```

## Recommendations

### Do

1. **Separate test code from production code** with the `src/` and `tests/` convention.
2. **Name test projects consistently**: `{Production}.Tests`.
3. **Mirror folder structure** between production and test projects.
4. **One test file per production class**: `Calculator.cs` -> `CalculatorTests.cs`.
5. **Use a `Fixtures/` folder** for shared test resources.

### Don't

1. **Don't mix test and production code** in the same project.
2. **Don't nest too deeply** — three or four levels under the project root is usually enough.
3. **Don't put business logic in a test project** — it only contains tests.
4. **Don't reference test projects from production projects** — only the other direction.
5. **Don't pack test projects** — keep `IsPackable=false`.

## Minimal example

```text
Calculator/
├── Calculator.sln
│
├── src/
│   └── Calculator.Core/
│       ├── Calculator.Core.csproj
│       ├── Calculator.cs
│       └── MathHelper.cs
│
└── tests/
    └── Calculator.Core.Tests/
        ├── Calculator.Core.Tests.csproj
        ├── CalculatorTests.cs
        └── MathHelperTests.cs
```

```powershell
dotnet new sln -n Calculator
dotnet new classlib -n Calculator.Core -o src/Calculator.Core
dotnet new xunit -n Calculator.Core.Tests -o tests/Calculator.Core.Tests
dotnet sln add src/Calculator.Core/Calculator.Core.csproj
dotnet sln add tests/Calculator.Core.Tests/Calculator.Core.Tests.csproj
dotnet add tests/Calculator.Core.Tests reference src/Calculator.Core
```

Small, clear, suitable for small to mid-size projects.
