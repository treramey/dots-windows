# Testing FluentValidation Validators

The `TestValidate` / `ShouldHaveValidationErrorFor` API ships inside the main `FluentValidation` package — no separate test-helper package is required. Add `using FluentValidation.TestHelper;` and assert through it.

```csharp
using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using AwesomeAssertions;
using Xunit;
```

Three-part naming and FIRST apply — see [reference/fundamentals.md](fundamentals.md) and [reference/naming.md](naming.md). For mocking dependencies in validators see [reference/nsubstitute.md](nsubstitute.md); for testable time use [reference/datetime.md](datetime.md); for the request-builder approach used in the patterns below see [reference/builder-pattern.md](builder-pattern.md).

## Why test validators?

Validators are the application's first line of defence. Tests for them:

1. **Lock in data integrity** — invalid data cannot slip past.
2. **Document business rules** — each test is a living spec.
3. **Cover error-message wording** — the message *is* the contract; users read it.
4. **Catch regressions on refactor** — rules drift silently otherwise.
5. **Exercise cross-field combinations** — the only place these are forced together.

## FluentValidation 12.x notes

12.0 is a major release targeting .NET 8+. Removed / renamed APIs you will run into when porting older suites:

- `Transform` / `TransformForEach` → `Must(...)` plus manual transformation.
- `InjectValidator` → constructor injection plus `SetValidator(...)`.
- `CascadeMode.StopOnFirstFailure` → `RuleLevelCascadeMode = CascadeMode.Stop`.
- `ShouldHaveAnyValidationError` → `ShouldHaveValidationErrors`.

Official upgrade guide: <https://docs.fluentvalidation.net/en/latest/upgrading-to-12.html>.

## Core API

| Method                                                    | Purpose                                                          |
| --------------------------------------------------------- | ---------------------------------------------------------------- |
| `TestValidate(model)`                                     | Run sync validation; returns `TestValidationResult<T>`           |
| `TestValidateAsync(model)`                                | Async variant                                                    |
| `result.ShouldHaveValidationErrorFor(x => x.Property)`    | Assert at least one error on the property                        |
| `result.ShouldNotHaveValidationErrorFor(x => x.Property)` | Assert no errors on the property                                 |
| `result.ShouldNotHaveAnyValidationErrors()`               | Whole object is valid                                            |
| `result.ShouldHaveValidationErrors()`                     | Whole object has at least one error                              |
| `.WithErrorMessage("...")`                                | Chain after `ShouldHaveValidationErrorFor` to assert the message |
| `.WithErrorCode("...")`                                   | Chain to assert the rule's error code                            |

## Pattern 1 — Basic field validation

```csharp
public class UserValidatorTests
{
    private readonly UserValidator _sut = new();

    [Fact]
    public void Validate_WhenUsernameIsEmpty_ShouldFailValidation()
    {
        var result = _sut.TestValidate(
            new UserRegistrationRequest { Username = "" });

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username must not be null or empty");
    }

    [Fact]
    public void Validate_WhenAllFieldsValid_ShouldPass()
    {
        var result = _sut.TestValidate(TestDataBuilder.CreateValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

## Pattern 2 — Parameterised invalid / valid inputs

Drive the same rule with many inputs via `[Theory]` + `[InlineData]`:

```csharp
[Theory]
[InlineData("",                                   "Username must not be null or empty")]
[InlineData("ab",                                 "Username length must be between 3 and 20")]
[InlineData("a_very_long_username_exceeds_limit", "Username length must be between 3 and 20")]
[InlineData("user@name",                          "Username may only contain letters, digits, and underscore")]
public void Validate_InvalidUsername_ShouldReturnExpectedError(string username, string expectedError)
{
    var result = _sut.TestValidate(new UserRegistrationRequest { Username = username });

    result.ShouldHaveValidationErrorFor(x => x.Username)
          .WithErrorMessage(expectedError);
}
```

## Pattern 3 — Cross-field rules

```csharp
RuleFor(x => x.Password)
    .NotEmpty()
    .Length(8, 50)
    .Must(BeComplexPassword).WithMessage("Password must contain upper, lower, and digit");

RuleFor(x => x.ConfirmPassword)
    .Equal(x => x.Password).WithMessage("Passwords must match");
```

Test mismatch:

```csharp
[Fact]
public void Validate_WhenPasswordsDoNotMatch_ShouldFailValidation()
{
    var request = new UserRegistrationRequest
    {
        Password = "TestPass123",
        ConfirmPassword = "Different"
    };

    _sut.TestValidate(request)
        .ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
        .WithErrorMessage("Passwords must match");
}
```

## Pattern 4 — Time-dependent rules

Inject `TimeProvider` into the validator and drive it with `FakeTimeProvider` — see [reference/datetime.md](datetime.md).

```csharp
public class UserValidator : AbstractValidator<UserRegistrationRequest>
{
    public UserValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.BirthDate)
            .Must((request, birthDate) => IsAgeConsistent(timeProvider, birthDate, request.Age))
            .WithMessage("Birth date and age do not match");
    }
}

public class UserValidatorTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly UserValidator _sut;

    public UserValidatorTests()
    {
        _time.SetUtcNow(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _sut = new UserValidator(_time);
    }

    [Fact]
    public void Validate_WhenBirthdayLaterThisYear_AgeShouldDecrementByOne()
    {
        _time.SetUtcNow(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var sut = new UserValidator(_time);

        var request = TestDataBuilder.CreateValidRequest();
        request.BirthDate = new DateTime(1990, 6, 15);
        request.Age = 33;   // birthday hasn't happened yet this year

        sut.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.BirthDate);
    }
}
```

## Pattern 5 — Conditional rules (`When` / `Unless`) — cover both branches

```csharp
RuleFor(x => x.PhoneNumber)
    .Matches(@"^09\d{8}$").WithMessage("Phone number format is invalid")
    .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
```

```csharp
[Fact]
public void Validate_WhenPhoneOmitted_ShouldSkipPhoneValidation()
{
    var request = TestDataBuilder.CreateValidRequest();
    request.PhoneNumber = null;

    _sut.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
}

[Fact]
public void Validate_WhenPhoneProvidedAndInvalid_ShouldFail()
{
    var request = TestDataBuilder.CreateValidRequest();
    request.PhoneNumber = "not-a-number";

    _sut.TestValidate(request)
        .ShouldHaveValidationErrorFor(x => x.PhoneNumber)
        .WithErrorMessage("Phone number format is invalid");
}
```

Only one branch tested means the rule could be inverted in production and the test would still pass — always cover both sides.

## Pattern 6 — Async rules with `MustAsync`

External lookups (uniqueness, banned-words service, inventory) belong inside `MustAsync`. Mock the dependency with NSubstitute and assert via `TestValidateAsync`. Gate the async rule with `.When(...)` so the expensive call doesn't fire for empty inputs that the basic rule already rejected:

```csharp
RuleFor(x => x.Username)
    .NotEmpty().WithMessage("Username must not be null or empty");

RuleFor(x => x.Username)
    .MustAsync(async (username, ct) => await users.IsUsernameAvailableAsync(username))
    .WithMessage("Username is already taken")
    .When(x => !string.IsNullOrWhiteSpace(x.Username));
```

Three tests carry the contract:

```csharp
[Fact]
public async Task ValidateAsync_WhenUsernameAvailable_ShouldPass()
{
    _users.IsUsernameAvailableAsync("newuser123").Returns(true);

    var result = await _sut.TestValidateAsync(
        new UserRegistrationRequest { Username = "newuser123" });

    result.ShouldNotHaveValidationErrorFor(x => x.Username);
    await _users.Received(1).IsUsernameAvailableAsync("newuser123");
}

[Fact]
public async Task ValidateAsync_WhenUsernameTaken_ShouldFail()
{
    _users.IsUsernameAvailableAsync("taken").Returns(false);

    var result = await _sut.TestValidateAsync(
        new UserRegistrationRequest { Username = "taken" });

    result.ShouldHaveValidationErrorFor(x => x.Username)
          .WithErrorMessage("Username is already taken");
}

[Fact]
public async Task ValidateAsync_WhenUsernameEmpty_ShouldNotCallService()
{
    var result = await _sut.TestValidateAsync(
        new UserRegistrationRequest { Username = "" });

    result.ShouldHaveValidationErrorFor(x => x.Username)
          .WithErrorMessage("Username must not be null or empty");
    await _users.DidNotReceive().IsUsernameAvailableAsync(Arg.Any<string>());
}
```

A more realistic multi-dependency shape (inventory + payments, each gated by its own `When`) lives in [templates/fluentvalidation/async-validator-examples.cs](../templates/fluentvalidation/async-validator-examples.cs).

## Pattern 7 — Collection validation

```csharp
RuleFor(x => x.Roles)
    .NotEmpty().WithMessage("Roles must not be empty")
    .Must(roles => roles == null || roles.All(IsValidRole))
    .WithMessage("Contains an invalid role");
```

For element-level errors, address by index:

```csharp
result.ShouldHaveValidationErrorFor("Roles[1]");
```

## Rule sets

Validators can group rules into named rule sets. Test each set explicitly so you don't accidentally pass rules that wouldn't run in production:

```csharp
[Fact]
public void Validate_RuleSetCreate_ShouldOnlyRunCreateRules()
{
    var result = _sut.TestValidate(new UserRegistrationRequest(),
        options => options.IncludeRuleSets("Create"));

    result.ShouldHaveValidationErrorFor(x => x.Username);
    result.ShouldNotHaveValidationErrorFor(x => x.LastLoginAt);
}
```

## Dependency injection in validators

When a validator's constructor takes services, test it the same way as any service: substitute the dependencies and `new` the validator up directly. Do not test through `IValidator<T>` resolved from a `ServiceProvider` — that adds an integration concern to a unit test.

## Test data builder for happy-path inputs

A single builder of a known-valid request keeps each test focused on the one field it's exercising:

```csharp
public static class TestDataBuilder
{
    public static UserRegistrationRequest CreateValidRequest() => new()
    {
        Username = "testuser123",
        Email = "test@example.com",
        Password = "TestPass123",
        ConfirmPassword = "TestPass123",
        BirthDate = new DateTime(1990, 1, 1),
        Age = 34,
        PhoneNumber = "0912345678",
        Roles = new List<string> { "User" },
        AgreeToTerms = true
    };

    public static UserRegistrationRequest WithUsername(this UserRegistrationRequest r, string username)
    { r.Username = username; return r; }
}

var request = TestDataBuilder.CreateValidRequest().WithUsername("newuser");
```

See [reference/builder-pattern.md](builder-pattern.md) for the general pattern.

## What to cover

- At least one positive and one negative test per rule.
- Boundary values (`min`/`max`/`min-1`/`max+1`).
- Error-message content (the message is part of the contract — users see it).
- Both branches of every `When` / `Unless`.
- Every rule set in isolation.
- Every cross-field combination that matters.
- For async rules, both `Received(1)` on the dependency for the happy path *and* `DidNotReceive()` when the gating `When` should short-circuit.

## Best practices

- **Theory + InlineData** for input families — boundary tests, format families, role lists.
- **Test boundaries explicitly** — `min`, `max`, `min-1`, `max+1` are the bug magnets.
- **Pin time with `FakeTimeProvider`** — never let `DateTime.Now` leak into validators or their tests.
- **Mock external dependencies** — async validators must not hit the real database in tests.
- **Centralise valid data** in a builder so each test mutates only the field under test.
- **Assert error messages, not just presence** — wording is the user-facing contract.

## Pitfalls

- **Forgetting the negative side of `When`** — only one branch tested means the rule could be inverted and still pass.
- **Hard-coding `DateTime.Now`** — flaky birthday/age tests.
- **Coupling many fields in one test** — when it fails, you don't know which rule moved.
- **Using `Assert.ThrowsAsync` for async-validator exceptions** — prefer `await act.Should().ThrowAsync<T>()` for consistency with the rest of the suite (see [reference/awesome-assertions.md](awesome-assertions.md)).

## Templates

- [templates/fluentvalidation/validator-test-template.cs](../templates/fluentvalidation/validator-test-template.cs) — full validator covering every rule kind (username, email, password complexity, cross-field equality, age boundaries, birth-date consistency with `FakeTimeProvider`, optional phone via `When`, role list).
- [templates/fluentvalidation/async-validator-examples.cs](../templates/fluentvalidation/async-validator-examples.cs) — `MustAsync` patterns with NSubstitute mocks, exception propagation, cancellation, and multi-dependency conditional async (inventory + payment).
