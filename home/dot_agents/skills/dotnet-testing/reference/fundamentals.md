# Fundamentals — going deeper

The umbrella's SKILL.md states FIRST and 3A in one breath each. This reference explains *why* each principle exists, the common ways tests violate them, and the decisions you make when sitting down to write a test from scratch (`[Fact]` vs `[Theory]`, what to cover, where the test sits in the pyramid).

## Why FIRST matters — and how tests typically violate it

### Fast — milliseconds, not seconds

Test suites get run hundreds of times a day on every developer's machine and on CI. A suite that takes more than a few seconds to give feedback stops being run. Friction wins.

The killer is hidden I/O — a "unit" test that opens a real database connection, reads a real file, sleeps a real `Task.Delay`, or hits a real HTTP endpoint. Each one looks individually cheap; in aggregate they push a 200-test suite from 0.5s to 90s.

```csharp
[Fact] // Fast: pure in-memory, no I/O
public void Add_WhenGiven1And2_ShouldReturn3()
{
    var calculator = new Calculator();
    var result = calculator.Add(1, 2);
    result.Should().Be(3);
}
```

If a test genuinely needs disk, network, or a database, it is not a unit test — it is an integration test. Put it in a separate project (`*.Test.Integration`) so the fast suite stays fast. See [reference/testcontainers.md](testcontainers.md) and [reference/aspnet-integration.md](aspnet-integration.md).

### Independent — fresh state per test

The xUnit runtime creates a new instance of the test class for every test method. That gift is wasted the moment mutable state lives in a `static` field, a shared `IClassFixture`, or a singleton.

Symptom of a violation: tests pass individually, fail when run in parallel. Or pass on the dev machine, fail on CI because CI runs them in a different order.

### Repeatable — same answer every run

Three smells produce flaky, non-repeatable tests:

1. **Reading the wall clock.** `DateTime.Now`, `DateTimeOffset.UtcNow`, `Stopwatch`. Inject a `TimeProvider`; see [reference/datetime.md](datetime.md).
2. **Reading randomness without a seed.** `Random`, `Guid.NewGuid()` when the GUID is the test target rather than incidental data.
3. **Reading the environment.** Machine name, current culture, environment variables, locale. Either fix them in the test or stop depending on them.

### Self-validating — pass/fail by assertion alone

A test that prints results and asks a human to read them is not a test. Use assertions. AwesomeAssertions makes the intent and the failure message clean — see [reference/awesome-assertions.md](awesome-assertions.md).

Log inspection is a *diagnostic* tool — useful when a test fails and you want to know why — but never the primary gate. For controlled diagnostic output see [reference/output-logging.md](output-logging.md).

### Timely — written alongside the code

Tests written weeks after the code tend to canonize whatever the code happens to do. Tests written alongside the code (or before it, in red/green/refactor) drive the design — they force the code into a testable shape, which is usually a *better* shape.

## The 3A pattern — responsibilities of each block

Every test method follows Arrange-Act-Assert. The structure makes intent obvious at a glance and makes it impossible to "hide" multiple behaviours inside one test.

| Block       | Responsibility                                        | Notes                                                                                                                                            |
| ----------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Arrange** | Build the SUT and the inputs; configure test doubles. | Use `const` for primitive expected/inputs to make them readable.                                                                                 |
| **Act**     | Invoke the method under test.                         | Usually a single line — calling the SUT method once.                                                                                             |
| **Assert**  | Verify the outcome.                                   | One behaviour per test. Multiple `.Should()` calls on related facets of one outcome are fine; verifying two independent behaviours is two tests. |

```csharp
[Fact]
public void Add_WhenGivenNegativeAndPositive_ShouldReturnCorrectResult()
{
    // Arrange
    var calculator = new Calculator();
    const int a = -5;
    const int b = 3;
    const int expected = -2;

    // Act
    var result = calculator.Add(a, b);

    // Assert
    result.Should().Be(expected);
}
```

## The testing pyramid

```
        ╱  E2E  ╲           few, slow, fragile, high-confidence
       ╱─────────╲
      ╱ Integration ╲       some, medium speed, real dependencies
     ╱───────────────╲
    ╱      Unit       ╲     many, milliseconds, pure logic
   ╱───────────────────╲
```

- **Unit (the base):** pure logic, no I/O, no real dependencies. The bulk of the suite. xUnit + NSubstitute + AutoFixture.
- **Integration:** multiple units working together, possibly with a containerized database or a `WebApplicationFactory`. Slower; smaller in number; verify the seams. See [reference/testcontainers.md](testcontainers.md) and [reference/aspnet-integration.md](aspnet-integration.md).
- **End-to-end:** the whole system through the user-facing surface. Few, expensive, often owned by a separate QA tier.

A common mistake is the *ice cream cone* — heavy at the top, light at the bottom. Symptom: a small change in a leaf class breaks dozens of E2E tests because the unit-level safety net is missing.

## `[Fact]` vs `[Theory]` — when to pick which

| Choose `[Fact]` when…                                 | Choose `[Theory]` when…                                 |
| ----------------------------------------------------- | ------------------------------------------------------- |
| Exactly one scenario                                  | Multiple input combinations exercise the same logic     |
| The arrangement varies meaningfully between scenarios | Inputs differ but the assertion shape is the same       |
| Cause and effect are tied to one specific value       | Boundaries, equivalence classes, invalid input families |

```csharp
[Theory]
[InlineData(1, 2, 3)]
[InlineData(-1, 1, 0)]
[InlineData(100, -50, 50)]
public void Add_WhenGivenVariousInputs_ShouldReturnSum(int a, int b, int expected)
{
    var calculator = new Calculator();
    var result = calculator.Add(a, b);
    result.Should().Be(expected);
}
```

If the rows of `[InlineData]` start having vastly different setup needs, split them into separate `[Fact]` tests. A `[Theory]` whose body is full of `if`s for "this row needs X, that row needs Y" is two tests pretending to be one.

For larger or computed datasets, prefer `[MemberData]` or `[ClassData]` — or AutoFixture's `[AutoData]` / `[InlineAutoData]`. See [reference/autofixture.md](autofixture.md).

**Full example:** [templates/fundamentals/parameterized-test.cs](../templates/fundamentals/parameterized-test.cs)

## Exception tests

Use `Assert.Throws<T>` or AwesomeAssertions' `.Should().Throw<T>()`.

```csharp
[Fact]
public void Divide_WhenDivisorIsZero_ShouldThrowDivideByZeroException()
{
    var calculator = new Calculator();

    var act = () => calculator.Divide(10m, 0m);

    act.Should().Throw<DivideByZeroException>()
        .WithMessage("Divisor cannot be zero");
}
```

Asserting on the exception *message* is fine when the message is part of the contract; asserting on the *type* alone is enough when the message is incidental. Don't pin tests to wording that will change with the next localization pass.

## Test class layout

Class per production class, horizontal separators between method groups, SUT instantiated either in the constructor (when cheap) or per test (when each method needs distinct setup).

```csharp
namespace MyProject.Tests;

public class CalculatorTests
{
    private readonly Calculator _calculator = new();

    //---------------------------------------------------------------------------------------------
    // Add

    [Fact]
    public void Add_WhenGivenTwoPositiveIntegers_ShouldReturnSum()
    {
        const int a = 1;
        const int b = 2;
        const int expected = 3;

        var result = _calculator.Add(a, b);

        result.Should().Be(expected);
    }

    //---------------------------------------------------------------------------------------------
    // Divide

    [Fact]
    public void Divide_WhenDivisorIsZero_ShouldThrowDivideByZeroException()
    {
        var act = () => _calculator.Divide(10m, 0m);

        act.Should().Throw<DivideByZeroException>()
            .WithMessage("Divisor cannot be zero");
    }
}
```

**Full example:** [templates/fundamentals/basic-test.cs](../templates/fundamentals/basic-test.cs)

## What to cover when writing tests for a method

Use this as a planning checklist, not a quota:

- **Happy path** — typical input produces the expected output.
- **Boundaries** — minimum, maximum, zero, empty string, empty collection, single-element collection.
- **Invalid input** — null, negative when negative is meaningless, malformed strings.
- **Exception paths** — every documented `throws` clause.
- **Branch coverage** — every `if`, `switch`, and ternary should have at least one test on each side.

If a branch has no observable effect at the public surface, that's a signal — the branch may be dead code, or the surface may be missing an observation point.

## Common assertions cheatsheet (AwesomeAssertions)

```csharp
result.Should().Be(expected);
result.Should().NotBe(expected);
result.Should().BeTrue();
result.Should().BeFalse();
result.Should().BeNull();
result.Should().NotBeNull();
collection.Should().BeEmpty();
collection.Should().Contain(item);
collection.Should().HaveCount(3);
act.Should().Throw<InvalidOperationException>();
```

See [reference/awesome-assertions.md](awesome-assertions.md) for the full surface and [reference/complex-equivalency.md](complex-equivalency.md) for `.Should().BeEquivalentTo(...)`.

## Test project shape

```text
Solution/
├── src/
│   └── MyProject/
│       ├── Calculator.cs
│       └── MyProject.csproj
└── tests/
    └── MyProject.Tests/
        ├── CalculatorTests.cs
        └── MyProject.Tests.csproj
```

The test project references the production project (`<ProjectReference>`), and pulls in `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, and `coverlet.collector`. Versions come from `Directory.Packages.props` — see [reference/xunit-setup.md](xunit-setup.md).

## Cross-links

- Names — [reference/naming.md](naming.md)
- xUnit project plumbing — [reference/xunit-setup.md](xunit-setup.md)
- Builders for tidy Arrange — [reference/builder-pattern.md](builder-pattern.md)
- Output capture during a test — [reference/output-logging.md](output-logging.md)
- Mocks/stubs — [reference/nsubstitute.md](nsubstitute.md)
- Anonymous data — [reference/autofixture.md](autofixture.md)
