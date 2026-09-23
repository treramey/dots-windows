# AwesomeAssertions

AwesomeAssertions is the community **Apache-2.0** fork of FluentAssertions. The repo standardises on it via Central Package Management — never add `FluentAssertions` (the legacy fork; banned in SKILL.md). The namespace is `AwesomeAssertions`; the API is otherwise highly compatible.

```csharp
using AwesomeAssertions;
```

For deep object graphs, ordering rules, and full `BeEquivalentTo` configuration, see [reference/complex-equivalency.md](complex-equivalency.md). For setup conventions and the bans list (Moq, MSTest, NUnit, FluentAssertions legacy fork) see SKILL.md and [reference/fundamentals.md](fundamentals.md).

## Categories at a glance

| Category           | Common methods                                                                                                                                      |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| Object             | `NotBeNull()`, `BeOfType<T>()`, `BeAssignableTo<T>()`, `BeEquivalentTo()`                                                                           |
| String             | `Contain()`, `StartWith()`, `EndWith()`, `MatchRegex()`, `BeEquivalentTo()` (case-insensitive), `ContainEquivalentOf()`                             |
| Numeric            | `BeGreaterThan()`, `BeLessThan()`, `BeInRange()`, `BeOneOf()`, `BeApproximately()`, `BePositive()`, `BeNegative()`                                  |
| Numeric collection | `EqualApproximately()`, `NotEqualApproximately()` (9.4+, .NET 8+)                                                                                   |
| Collection         | `HaveCount()`, `Contain()`, `ContainSingle()`, `BeEquivalentTo()`, `AllSatisfy()`, `OnlyContain()`, `OnlyHaveUniqueItems()`, `BeInAscendingOrder()` |
| Exception          | `Throw<T>()`, `NotThrow()`, `WithMessage()`, `WithInnerException()`                                                                                 |
| Async              | `ThrowAsync<T>()`, `CompleteWithinAsync()`                                                                                                          |

## Object assertions

```csharp
user.Should().NotBeNull();
user.Should().BeOfType<User>();
user.Should().BeAssignableTo<IUser>();

user.Should().BeEquivalentTo(new { Id = 1, Name = "John" }); // partial anonymous match
```

## String assertions

```csharp
message.Should().NotBeNullOrWhiteSpace();
message.Should().Contain("Hello").And.StartWith("Hello").And.EndWith("World");
message.Should().ContainEquivalentOf("WORLD");          // case-insensitive
message.Should().HaveLengthGreaterThan(5);

email.Should().MatchRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");
```

## Numeric assertions

```csharp
age.Should().BeGreaterThan(18).And.BeLessThan(65);
age.Should().BeInRange(18, 65);
age.Should().BeOneOf(25, 30, 35);

// Floating point — never `Be` on double/float
pi.Should().BeApproximately(3.14, 0.01);

double.NaN.Should().Be(double.NaN);
double.PositiveInfinity.Should().BePositiveInfinity();
```

## Collection assertions

```csharp
numbers.Should().HaveCount(5);
numbers.Should().Contain(3);
numbers.Should().ContainSingle(x => x == 3);

numbers.Should().Equal(1, 2, 3, 4, 5);                          // ordered
numbers.Should().BeEquivalentTo(new[] { 5, 4, 3, 2, 1 });       // unordered

numbers.Should().BeInAscendingOrder();
numbers.Should().OnlyHaveUniqueItems();
numbers.Should().BeSubsetOf(new[] { 1, 2, 3, 4, 5, 6, 7 });
```

Complex object collections — drill into each element with `AllSatisfy`:

```csharp
users.Should().Contain(u => u.Name == "John");
users.Should().OnlyContain(u => u.Age >= 18);

users.Should().AllSatisfy(u =>
{
    u.Id.Should().BeGreaterThan(0);
    u.Name.Should().NotBeNullOrEmpty();
    u.Age.Should().BePositive();
});

users.Where(u => u.Age > 30).Should().HaveCount(1);   // LINQ projection then assert
```

## Exception assertions

Wrap the call in `Action` (sync) or `Func<Task>` (async):

```csharp
Action act = () => sut.Withdraw(-1);

act.Should().Throw<ArgumentOutOfRangeException>()
   .WithMessage("*negative*")
   .Which.ParamName.Should().Be("amount");

act.Should().Throw<ArgumentException>()
   .WithMessage("*User ID must be positive*")
   .And.ParamName.Should().Be("userId");

act.Should().Throw<DatabaseConnectionException>()
   .WithInnerException<ArgumentException>()
   .WithMessage("*connection string*");

act.Should().NotThrow();
act.Should().NotThrow<DivideByZeroException>();
```

Async:

```csharp
Func<Task> act = () => sut.ProcessAsync(badRequest);
await act.Should().ThrowAsync<ValidationException>().WithMessage("*required*");

Func<Task> work = () => service.GetDataAsync();
await work.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
```

## Chained assertions

`Should()` returns a constraint that exposes `.And.` to chain on the same subject and `.Which.` to drill into the result of an assertion:

```csharp
user.Should().NotBeNull()
    .And.BeOfType<AdminUser>()
    .Which.Permissions.Should().Contain("manage-users");
```

## `because:` — failure context

Every assertion accepts a trailing `because:` string. Use it to encode intent for the next maintainer:

```csharp
result.IsSuccess.Should().BeFalse("because negative payment amounts are not allowed");

actual.Should().BeEquivalentTo(expected, options => options
    .Excluding(u => u.Id),
    because: "the Id is server-generated and not part of the contract");
```

## Batching failures with `AssertionScope`

Collects every failure inside the scope and reports them together at the end:

```csharp
using (new AssertionScope())
{
    user.Should().NotBeNull("user creation should not fail");
    user.Id.Should().BeGreaterThan(0, "user should have a valid id");
    user.Email.Should().NotBeNullOrEmpty("email is required");
}
```

Particularly useful when validating a freshly constructed object across multiple fields — you get one consolidated report instead of fixing one failure at a time. Inside the scope you can also tune collection rendering:

```csharp
using var scope = new AssertionScope();
scope.FormattingOptions.MaxItems = 100;   // default 32
largeCollection.Should().BeEquivalentTo(expected);
```

## Version-gated features worth knowing

- **`BeApproximately` / `EqualApproximately`** (9.4+, .NET 8+) — single value vs sequence of `INumber<T>`. With tolerance `0`, `EqualApproximately` collapses to `Equal`.

  ```csharp
  actual.Should().EqualApproximately(new[] { 1f, 2f, 3f }, 0.01f);
  ```

- **Occurrence constraints on `Contain`** (9.2+):

  ```csharp
  var numbers = new[] { 1, 2, 2, 3, 3, 3 };
  numbers.Should().Contain(2, AtLeast.Once());
  numbers.Should().Contain(3, Exactly.Times(3));
  ```

- **Null-safety** (9.4+) — `Should().NotBeNull()` carries `[return: NotNull]`, so the C# nullable analyzer treats the subject as non-null afterwards. No more `!`:

  ```csharp
  user.Should().NotBeNull();
  user.Name.Should().Be("test");   // no ! needed on 9.4+
  ```

- **Member-name exclusion** (9.3+):

  ```csharp
  actual.Should().BeEquivalentTo(expected, options => options
      .Excluding(ctx => ctx.Name == "InternalState"));
  ```

## Domain-specific custom assertions

Wrap recurring multi-step checks behind a domain verb. The shape is an extension method on `ObjectAssertions` that returns an `AndConstraint<ObjectAssertions>` so callers can keep chaining:

```csharp
public static AndConstraint<ObjectAssertions> BeValidProduct(
    this ObjectAssertions assertions, string because = "", params object[] becauseArgs)
{
    var product = assertions.Subject as Product;
    product.Should().NotBeNull();
    product!.Id.Should().BeGreaterThan(0);
    product.Name.Should().NotBeNullOrEmpty();
    product.Price.Should().BeGreaterThan(0);
    return new AndConstraint<ObjectAssertions>(assertions);
}
```

Usage: `product.Should().BeValidProduct().And.Name.Should().Be("Laptop")`.

Full e-commerce examples plus reusable exclusion-extension helpers (`ExcludingAuditFields`, `ExcludingAutoGeneratedFields`, `ExcludingAllTimeFields`, `ExcludingCommonAutoFields`) live in [templates/awesome-assertions/custom-assertions-template.cs](../templates/awesome-assertions/custom-assertions-template.cs).

## Performance: large datasets

`BeEquivalentTo` on a graph with tens of thousands of items is slow and the diff is unreadable. Use the strategies in [reference/complex-equivalency.md](complex-equivalency.md). The short version: count first, then either whitelist key properties with `Including`, or sample-compare. A reusable sampling helper lives in the templates.

## Common scenarios

API response — selective property comparison:

```csharp
response.StatusCode.Should().Be(200);
var user = JsonSerializer.Deserialize<User>(response.Content);
user.Should().BeEquivalentTo(new { Id = userId, Email = expectedEmail },
    options => options.Including(u => u.Id).Including(u => u.Email));
```

Persisted entity — exclude server-managed fields:

```csharp
saved.Should().BeEquivalentTo(user, options => options
    .Excluding(u => u.CreatedAt)
    .Excluding(u => u.UpdatedAt)
    .Excluding(u => u.RowVersion));
```

Captured event — assert the payload, not just the fact:

```csharp
raised.Should().BeTrue("creating an order should raise OrderCreatedEvent");
captured.Should().NotBeNull();
captured!.OrderId.Should().BeGreaterThan(0);
captured.TotalAmount.Should().Be(expectedAmount);
```

## Common pitfalls

- **Collection order mismatch** — `BeEquivalentTo` is order-insensitive by default. Use `Equal` (or `WithStrictOrdering()`) when sequence is part of the contract.
- **Floating point** — never `Be` on `double`/`float`; use `BeApproximately` or `EqualApproximately`.
- **Asserting on log strings** — banned by the umbrella SKILL.md. Assert observable state instead.
- **`BeEquivalentTo` failing on entities** — auto-generated `Id`, `CreatedAt`, `UpdatedAt`, `RowVersion` are the usual culprits; reach for the reusable exclusion extensions instead of re-typing the chain.
- **Nullable warnings after `Should().NotBeNull()`** — make sure you're on 9.4+; the `[return: NotNull]` annotation only landed then.

## Templates

- [templates/awesome-assertions/assertion-examples.cs](../templates/awesome-assertions/assertion-examples.cs) — broad coverage of object/string/numeric/collection/exception/async assertions plus AssertionScope.
- [templates/awesome-assertions/custom-assertions-template.cs](../templates/awesome-assertions/custom-assertions-template.cs) — domain-specific assertion extensions, smart exclusion helpers, performance-optimised sampling.
