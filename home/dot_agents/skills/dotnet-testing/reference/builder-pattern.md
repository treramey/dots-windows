# Test Data Builder pattern

A Test Data Builder produces test objects through a fluent `.With…()` API on top of sensible defaults. It replaces both inline `new` blocks crammed with property setters and rigid Object Mother factories.

Three-part naming and FIRST apply — see SKILL.md.

## What it solves

Object Mothers return a single fixed shape; the only way to vary them is to write another factory or mutate the result after construction. Inline `new { Name = "…", Email = "…", Age = … }` blocks fan out across tests and bury the one or two properties the test actually cares about. A builder gives the test a vocabulary:

```csharp
// Inline construction — six lines of setup, one line of intent
var user = new User
{
    Name = "John Doe", Email = "john@example.com", Age = 30,
    Roles = new[] { "User" }, Settings = new UserSettings { Theme = "Dark", Language = "en-US" },
    IsActive = true, CreatedAt = DateTime.UtcNow, ModifiedAt = DateTime.UtcNow
};

// Builder — defaults absorb everything the test doesn't care about
var user = UserBuilder.AUser().WithName("John Doe").WithValidEmail().Build();
```

## When to reach for a builder over AutoFixture

AutoFixture handles "I need *a* `User`, the details don't matter" perfectly. A builder earns its keep when:

- The test cares about **specific values** (`WithName("John Doe")`) and AutoFixture's anonymous data would obscure intent.
- The object has **many required fields** and you want defaults AutoFixture can't infer (e.g., a `User` that should default to active, verified, and in the US locale).
- You need **semantic variants** referenced across tests: `AnAdminUser()`, `ARegularUser()`, `ALockedOutUser()`.
- You're **composing several builders** (`Order` needs `User` and `Product`) and want to express the composition in the test.

Use AutoFixture for bulk or anonymous data; use a builder when intent has to be loud. They compose — an AutoFixture `ISpecimenBuilder` can delegate to your builder for a particular type. See [reference/autofixture.md](autofixture.md).

## Anatomy of a builder

A standard Test Data Builder has four parts:

1. **Defaults** — every required field has a sane value, so `Build()` with no `With…` calls produces a valid object.
2. **Fluent `With…` methods** — return `this` for chaining.
3. **Semantic factories** — `static UserBuilder AUser()`, `AnAdminUser()`, `ARegularUser()` — meaningful starting points.
4. **`Build()`** — produces the object.

```csharp
public class UserBuilder
{
    // 1. Defaults — every required field has a sane value.
    private string _name = "Test User";
    private string _email = "test@example.com";
    private string[] _roles = ["User"];
    private bool _isActive = true;

    // 2. Semantic factories — meaningful starting points.
    public static UserBuilder AUser() => new();
    public static UserBuilder AnAdminUser() => new UserBuilder().WithRoles("Admin");
    public static UserBuilder ALockedOutUser() => new UserBuilder().WithIsActive(false);

    // 3. Fluent With… methods — return `this` for chaining.
    public UserBuilder WithName(string name) { _name = name; return this; }
    public UserBuilder WithValidEmail() { _email = "valid@example.com"; return this; }
    public UserBuilder WithRoles(params string[] roles) { _roles = roles; return this; }
    public UserBuilder WithIsActive(bool isActive) { _isActive = isActive; return this; }

    // 4. Build — produces the object.
    public User Build() => new()
    {
        Name = _name, Email = _email, Roles = _roles, IsActive = _isActive,
    };
}
```

Use in a test:

```csharp
[Fact]
public void Login_WhenUserIsLockedOut_ShouldReturnUnauthorized()
{
    // Arrange
    var user = UserBuilder.ALockedOutUser().WithEmail("locked@example.com").Build();
    var sut = new LoginService();

    // Act
    var result = sut.Login(user);

    // Assert
    result.Should().Be(LoginResult.Unauthorized);
}
```

The Arrange line reads as English: "a locked-out user with email locked@example.com." Everything else is implicit, because the test doesn't care.

**Full example:** [templates/builder-pattern/user-builder-example.cs](../templates/builder-pattern/user-builder-example.cs) — complete `UserBuilder` with collection helpers, semantic combinations (`WithValidEmail`, `WithAdminRights`, `WithDarkTheme`), and five usage scenarios.

## Best practices

### 1. Defaults must produce a valid object

`new UserBuilder().Build()` must produce something the rest of the production code is happy with — not `null` strings, not negative ages, not an empty email. The point of the pattern is that tests only have to specify what they care about; everything else has to "just work" or the tests will be full of incidental setup.

### 2. Semantic naming over setter-shaped naming

Method names should express the test's intent, not parrot the property:

```csharp
public static class UserScenarios
{
    public static UserBuilder ANewUser() => UserBuilder.AUser()
        .CreatedOn(DateTime.UtcNow);

    public static UserBuilder AnExpiredUser() => UserBuilder.AUser()
        .CreatedOn(DateTime.UtcNow.AddYears(-5))
        .IsInactive();

    public static UserBuilder APremiumUser() => UserBuilder.AUser()
        .WithRoles("Premium", "User")
        .WithSettings(new UserSettings { FeatureFlags = new[] { "AdvancedSearch" } });
}
```

`WithValidEmail()` reads better than `WithEmail("valid@example.com")` when "valid" is a recurring concept; `ALockedOutUser()` reads better than `WithIsActive(false)` when locked-out users are a domain idea your tests touch repeatedly.

### 3. Compose builders, don't reach across them

When an outer type contains an inner one, the outer builder accepts a built object (or builder) for the inner so the test still controls the inner shape:

```csharp
var order = OrderBuilder.AnOrder()
    .ForCustomer(UserBuilder.APremiumUser().WithName("Alice Premium").Build())
    .WithProducts(
        ProductBuilder.AProduct().WithName("Laptop").WithPrice(1000m).Build(),
        ProductBuilder.AProduct().WithName("Mouse").WithPrice(50m).Build())
    .WithStatus(OrderStatus.Confirmed)
    .Build();
```

The outer builder never reaches into the inner type's internals — it just holds onto the inner builder (or the built object) and calls `Build()` when needed.

**Full example:** [templates/builder-pattern/advanced-builder-scenarios.cs](../templates/builder-pattern/advanced-builder-scenarios.cs) — `ProductBuilder`, `OrderBuilder` composing user/product, plus a `TestData` static class and an `OrderValidator` Theory.

### 4. Keep builders dumb

A builder is for constructing data. It is not for re-implementing production behaviour:

```csharp
// Don't — branching logic inside a With… method
public UserBuilder WithComplexValidation()
{
    if (_email.Contains("@"))
    {
        var parts = _email.Split('@');
        if (parts[1].Length > 10)
            _email = parts[0] + "@short.com";
    }
    return this;
}

// Do — name a specific shape directly
public UserBuilder WithShortDomainEmail()
{
    _email = "user@short.com";
    return this;
}
```

No I/O, no calls into production services, no validation. If the production object has invariants, let the production constructor or validator enforce them — the builder's job is to give the SUT data, not to be the SUT.

### 5. Centralise canonical data in a `TestData` class

For values that recur across tests (a known-good email, a canonical SKU, a sentinel GUID), put them on a static class so renames happen in one place. Builders pull their defaults from here:

```csharp
public static class TestData
{
    public static class Users
    {
        public const string ValidEmail = "test@example.com";
        public static readonly Guid SystemUserId = new("11111111-1111-1111-1111-111111111111");

        public static User John => UserBuilder.AUser()
            .WithName("John Doe").WithEmail("john@example.com").WithAge(30).Build();

        public static User Admin => UserBuilder.AnAdminUser()
            .WithName("Admin User").WithEmail("admin@company.com").Build();
    }
}
```

Use `static` properties (re-built per access) rather than `static readonly` fields when the object is mutable — otherwise one test's mutation leaks into the next.

## Mutable vs immutable builders

The examples above are **mutable** — each `With…` method returns the same builder and re-assigns a field. This is the most common style; it's cheap and easy to read.

An **immutable** builder returns a new instance on every call:

```csharp
public UserBuilder WithName(string name) => this with { _name = name };
```

Use immutable builders when:

- You want to share a partial setup across tests (`var baseUser = UserBuilder.AUser().WithRoles("User");`) and don't want one test's `With…` call to leak into another.
- The builder backs a `record` — record `with` expressions make immutability essentially free.

For everything else, mutable is fine. Don't over-engineer.

## Theory tests — Builder + `[MemberData]` / `[ClassData]`

Builders shine when paired with parameterised tests. Each row becomes a `Build()` call that names the scenario inline:

```csharp
[Theory]
[MemberData(nameof(GetUserScenarios))]
public void Validate_WithVariousUsers_ShouldReturnExpected(User user, bool expected)
{
    var validator = new UserValidator();

    var actual = validator.IsValid(user);

    actual.Should().Be(expected);
}

public static IEnumerable<object[]> GetUserScenarios()
{
    yield return new object[] { UserBuilder.AUser().WithName("Valid").Build(), true };
    yield return new object[] { UserBuilder.AUser().WithName("").Build(), false };
    yield return new object[] { UserBuilder.AUser().WithAge(10).Build(), false };
    yield return new object[] { UserBuilder.AUser().WithEmail("invalid").Build(), false };
}
```

Strongly-typed alternative — `TheoryData<T1, T2>` via `[ClassData]`:

```csharp
public class CustomerUpgradeTestData : TheoryData<Customer, CustomerType>
{
    public CustomerUpgradeTestData()
    {
        Add(CustomerBuilder.ARegularCustomer().WithCreditLimit(2000m).Build(), CustomerType.Premium);
        Add(CustomerBuilder.APremiumCustomer().WithCreditLimit(7000m).Build(), CustomerType.VIP);
        Add(CustomerBuilder.ARegularCustomer().WithCreditLimit(1000m).Build(), CustomerType.Regular);
    }
}
```

**Full example:** [templates/builder-pattern/builder-with-theory.cs](../templates/builder-pattern/builder-with-theory.cs) — `CustomerBuilder` with four scenarios: tier discount, validation, credit approval, and tier upgrade via `ClassData`.

## Comparison tables

### Builder vs Object Mother

| Aspect          | Builder                                    | Object Mother                                              |
| --------------- | ------------------------------------------ | ---------------------------------------------------------- |
| Flexibility     | Tunable per test via `With…()`             | Returns a fixed object                                     |
| Readability     | Fluent intent at the call site             | Have to open the mother to see what you got                |
| Maintainability | Add a `With…()` once; old tests stay green | Adding a variant either bloats the mother or duplicates it |
| Best for        | Unit tests with specific scenarios         | Tiny smoke tests with no variation                         |

Object Mothers are fine for trivial cases. Reach for a builder the moment you find yourself copy-pasting an Object Mother factory and tweaking one field.

### Builder vs AutoFixture

| Aspect      | Builder                               | AutoFixture                           |
| ----------- | ------------------------------------- | ------------------------------------- |
| Control     | Full — you set every field            | Auto — random anonymous values        |
| Setup cost  | Write the builder once                | Zero                                  |
| Test intent | Loud (`ALockedOutUser`)               | Quiet (the test relies on type alone) |
| Best fit    | Specific scenarios, semantic variants | Bulk fixtures, "any valid X"          |

Not either-or. Use AutoFixture by default; introduce a builder for the type the moment intent matters. The two compose — an AutoFixture `ISpecimenBuilder` can delegate to your builder for a particular type.

## Organisation

Put builders in a dedicated folder beside the tests that use them:

```text
MyProject.Tests/
├── Builders/
│   ├── UserBuilder.cs
│   ├── ProductBuilder.cs
│   ├── OrderBuilder.cs
│   └── TestData.cs
└── Services/
    └── OrderServiceTests.cs
```

Conventions:

- One builder per production type. Reuse via composition, not inheritance.
- Name semantic factories with the indefinite article: `AUser()`, `AnAdminUser()`, `AnOrder()`, `ACompletedOrder()`.
- Pair builders with `TestData` for canonical sample objects.

## Pitfalls

- **Invalid defaults.** If `Build()` on a fresh builder produces an object that fails validation, every test has to undo the damage with extra `With…` calls. Defaults must be valid.
- **Setter-shaped names.** `WithIsActiveFalse()` is noise; `IsInactive()` reads as English.
- **Business logic in `With…`.** If you find yourself branching on `_email`, you've drifted into production-code territory — pull the logic out, or expose a more specific `With…`.
- **Shared mutable `TestData` properties.** A `static readonly User John` lets one test mutate state for the next. Use `static User John { get => UserBuilder.AUser()…Build(); }` so every read returns a fresh instance.
- **Builders that know about the SUT.** A builder should construct the test object, not call repository methods or wire DI. Keep it inert.

## Checklist

- [ ] `Build()` with no `With…` calls returns a valid object.
- [ ] Method names express test intent, not setter syntax.
- [ ] Builders live in a `Builders/` folder under the test project.
- [ ] Composing a complex object goes through an outer builder, not by reaching into nested types directly.
- [ ] Canonical sample objects live in a `TestData` static class.
- [ ] No I/O, no production service calls, no validation logic inside `With…`.

## Sibling references

[reference/fundamentals.md](fundamentals.md) · [reference/naming.md](naming.md) · [reference/autofixture.md](autofixture.md) · [reference/bogus.md](bogus.md) · [reference/xunit-setup.md](xunit-setup.md)
