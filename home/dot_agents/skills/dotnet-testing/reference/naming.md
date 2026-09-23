# Test naming — edge cases

The shared rule (`Method_Scenario_ExpectedBehavior`, PascalCase parts, underscore-separated) lives in SKILL.md. This file covers cases that rule doesn't fully pin down.

## The three parts in detail

| Part                  | Job                                                                | Examples                                                                |
| --------------------- | ------------------------------------------------------------------ | ----------------------------------------------------------------------- |
| **Method under test** | The exact public method being exercised.                           | `Add`, `ProcessOrder`, `IsValidEmail`                                   |
| **Scenario / input**  | The condition the test exercises — input shape, state, or trigger. | `WhenGiven1And2`, `WhenInputIsNull`, `WhenOrderIsValid`                 |
| **Expected behavior** | The observable outcome. Specific, verifiable.                      | `ShouldReturn3`, `ShouldThrowArgumentNullException`, `ShouldReturnTrue` |

## Scenario phrasing vocabulary

Pick the verb form that matches what the scenario actually *is* — input, event, or initial state — instead of forcing every test to start with `WhenGiven`.

| Phrase                              | When to use                                                                         |
| ----------------------------------- | ----------------------------------------------------------------------------------- |
| `WhenGiven…`                        | Generic input parameters: `Add_WhenGiven1And2_ShouldReturn3`                        |
| `When…`                             | Event or state-triggered behavior: `WhenOrderIsCancelled_ShouldRefundPayment`       |
| `WhenStarting…` / `WhenStartingAt…` | Initial state matters: `Increment_WhenStartingAtZero_ShouldReturn1`                 |
| `WhenInputIs…`                      | Single-argument shape: `IsValidEmail_WhenInputIsNull_ShouldReturnFalse`             |
| `WhenFormatIs…`                     | Specific format failures: `IsValidEmail_WhenFormatIsInvalid_ShouldReturnFalse`      |
| `WhenCalled…`                       | Method invocation under a specific condition: `Reset_WhenCalled_ShouldReturnToZero` |

For the expected-result half:

| Phrase           | When to use                                                     |
| ---------------- | --------------------------------------------------------------- |
| `ShouldReturn…`  | Method returns a value                                          |
| `ShouldThrow…`   | Expected exception type — include the exception in the name     |
| `ShouldBe…`      | Post-state assertion (`ShouldBeActive`, `ShouldBeEmpty`)        |
| `ShouldContain…` | Collection-membership assertion                                 |
| `ShouldHandle…`  | Boundary condition that should be tolerated, not throw          |
| `ShouldNot…`     | Verifying the absence of a side effect (`ShouldNotSendReceipt`) |

Examples:

```csharp
[Fact] public void ProcessOrder_WhenInputIsNull_ShouldThrowArgumentNullException() { }
[Fact] public void Calculate_WhenPriceIsNegative_ShouldThrowArgumentException() { }
[Fact] public void GetOrderNumber_WhenOrderIsValid_ShouldReturnFormattedOrderNumber() { }
[Fact] public void Reset_WhenCalled_ShouldReturnToZero() { }
[Fact] public void Cancel_WhenOrderIsAlreadyCompleted_ShouldThrowInvalidOperationException() { }
```

## `[Theory]` data naming

`[Theory]` tests cover several scenarios with a single method, so the scenario half should describe the *class* of inputs, not any one row:

```csharp
// Multiple unrelated input sets — "Various"
[Theory]
[InlineData(1, 2, 3)]
[InlineData(-1, 1, 0)]
public void Add_WhenGivenVariousInputs_ShouldReturnCorrectResult(int a, int b, int expected) { }

// All positive cases — "Valid"
[Theory]
[InlineData("test@example.com")]
public void IsValidEmail_WhenEmailFormatIsValid_ShouldReturnTrue(string validEmail) { }

// All negative cases — "Invalid"
[Theory]
[InlineData("invalid-email")]
public void IsValidEmail_WhenEmailFormatIsInvalid_ShouldReturnFalse(string invalidEmail) { }

// Input → output mapping — "Corresponding"
[Theory]
[InlineData("test@gmail.com", "gmail.com")]
public void GetDomain_WhenGivenValidEmails_ShouldReturnCorrespondingDomain(string email, string expected) { }
```

If a `[Theory]` row crosses multiple scenarios (one row throws, another returns a value), split it into two `[Theory]` methods — the name can only describe one outcome.

## Complex scenarios

When the scenario needs to mention multiple inputs or conditions, link them with `And`:

```csharp
[Fact] public void Calculate_WhenPriceIs100AndDiscountIs10Percent_ShouldReturn90() { }
[Fact] public void CalculateWithTax_WhenPriceIs100AndTaxRateIs5Percent_ShouldReturn105() { }
[Fact] public void Increment_WhenCalledTwiceFromZero_ShouldReturn2() { }
```

Keep it readable — three `And`s is the limit. Past that, the test is doing too much; split it or move setup behind a builder (see [builder-pattern.md](builder-pattern.md)).

## When to deviate

You may break the three-part pattern when:

- **The "method under test" isn't a method.** For a behavior that spans several methods (`ProcessOrder` triggers `ChargeCard` then `SendReceipt`), name the test after the behavior, not the entry point: `OrderProcessing_WhenCardIsDeclined_ShouldNotSendReceipt`.
- **A `[Theory]` describes a property, not a scenario.** Property-based or matrix tests can use `_ShouldSatisfy…` phrasing: `Add_ShouldSatisfyCommutativeProperty`.

You may **not** deviate by:

- Removing the underscores (`AddReturns3WhenGiven1And2`).
- Dropping the expected behavior (`Add_WhenGiven1And2`).
- Using lowercase or snake_case (`add_returns_3_when_given_1_and_2`).
- Numbering tests (`Add_Test1`, `Add_Test2`).
- Using meaningless suffixes (`TestAdd`, `EmailTest`, `OrderTest`).

## Class naming

`{ClassUnderTest}Tests`, in a namespace mirroring the production namespace with `.Tests` appended:

| Production type   | Test class             | Namespace                  |
| ----------------- | ---------------------- | -------------------------- |
| `Calculator`      | `CalculatorTests`      | `MyProject.Tests`          |
| `OrderService`    | `OrderServiceTests`    | `MyProject.Services.Tests` |
| `EmailHelper`     | `EmailHelperTests`     | `MyProject.Helpers.Tests`  |
| `PriceCalculator` | `PriceCalculatorTests` | `MyProject.Pricing.Tests`  |

One test class per production class. If a single class needs many tests (40+), split by method using nested classes, not by inventing alternate suffixes:

```csharp
public class OrderServiceTests
{
    public class ProcessOrder
    {
        [Fact] public void WhenInputIsNull_ShouldThrowArgumentNullException() { }
        [Fact] public void WhenOrderIsValid_ShouldReturnProcessedOrder() { }
    }

    public class CancelOrder
    {
        [Fact] public void WhenOrderIsAlreadyCancelled_ShouldThrowInvalidOperationException() { }
    }
}
```

Nested-class names still describe the method under test, and inner test names drop the method portion (since the enclosing class already names it).

## Class structure template

Use a horizontal separator comment between method groups so the file reads like a table of contents:

```csharp
namespace MyProject.Tests;

public class CalculatorTests
{
    private readonly Calculator _calculator = new();

    //---------------------------------------------------------------------------------------------
    // Add

    [Fact]
    public void Add_WhenGiven1And2_ShouldReturn3() { }

    //---------------------------------------------------------------------------------------------
    // Divide

    [Fact]
    public void Divide_WhenGiven10And2_ShouldReturn5() { }
}
```

**Full example:** [templates/naming/naming-convention-examples.cs](../templates/naming/naming-convention-examples.cs)

## Copy/paste name templates

```csharp
// Happy path
{Method}_WhenGiven{ValidInput}_ShouldReturn{ExpectedResult}

// Null input
{Method}_WhenInputIsNull_ShouldThrowArgumentNullException

// Empty / blank input
{Method}_WhenInputIsEmpty_ShouldReturn{ExpectedResult}

// Boundary
{Method}_WhenInputIs{BoundaryValue}_Should{ExpectedBehavior}

// Exception path
{Method}_WhenGiven{InvalidInput}_ShouldThrow{ExceptionType}

// State transition
{Method}_WhenStartingAt{InitialState}_ShouldReach{ExpectedState}

// Parameterised
{Method}_WhenGivenVarious{InputType}_ShouldReturn{ExpectedPattern}
```

For a full catalogue of names organised by domain (arithmetic, validation, business logic, pricing, state, collections, async, exceptions, `[Theory]`), see **[templates/naming/naming-catalogue.md](../templates/naming/naming-catalogue.md)**.

## Readability check

The names should produce a test report that reads like a spec:

```text
PASS CalculatorTests
   PASS Add_WhenGiven1And2_ShouldReturn3
   PASS Add_WhenGivenNegativeAndPositive_ShouldReturnCorrectResult
   FAIL Divide_WhenDivisorIsZero_ShouldThrowDivideByZeroException

PASS EmailHelperTests
   PASS IsValidEmail_WhenEmailIsValid_ShouldReturnTrue
   PASS IsValidEmail_WhenInputIsNull_ShouldReturnFalse
```

If a name in your report doesn't tell you what failed without opening the test, rename it.

## Checklist

- [ ] All three parts present (`Method_Scenario_ExpectedBehavior`).
- [ ] Scenario describes the input/state/event, not a row index.
- [ ] Expected behavior is specific — the exception type is named, not just "throws".
- [ ] No `Test1`, `TestAdd`, `Tests` suffix on methods, no numbered tests.
- [ ] `[Theory]` methods use `Various`, `Valid`, `Invalid`, `Corresponding`, etc., not single-row phrasing.
- [ ] Class name is `{ClassUnderTest}Tests` and lives in a `.Tests`-suffixed namespace mirror.
- [ ] Long classes broken into nested classes by method-under-test, not by tag.

## Cross-links

- 3A + FIRST — [fundamentals.md](fundamentals.md)
- Builder names that read well in tests — [builder-pattern.md](builder-pattern.md)
- AutoFixture `[AutoData]` parameter naming — [autofixture.md](autofixture.md)
