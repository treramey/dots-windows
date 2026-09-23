# Complex Equivalency with `BeEquivalentTo`

`BeEquivalentTo` performs deep, member-wise comparison of object graphs. Use it for DTOs, EF entities, API responses, and any case where reference equality and `Equals` would force you to write a custom comparer. The basic assertion vocabulary lives in [reference/awesome-assertions.md](awesome-assertions.md); this file is the companion focused on configuration, exclusion strategy, and performance.

## Options cheat sheet

| Option                                      | Purpose                                 | When to use                                                 |
| ------------------------------------------- | --------------------------------------- | ----------------------------------------------------------- |
| `Excluding(x => x.Property)`                | Exclude a property                      | Skip timestamps, auto-generated ids                         |
| `Excluding(ctx => ctx.Path.EndsWith("At"))` | Path-pattern exclusion                  | Bulk-skip `CreatedAt`/`UpdatedAt`/`ModifiedAt`              |
| `Excluding(ctx => ctx.Name == "X")`         | Name-predicate exclusion (9.3+)         | Skip by member name                                         |
| `Including(x => x.Property)`                | Whitelist                               | Validate only the few properties that matter                |
| `ExcludingMissingMembers()`                 | Ignore members not on the expected side | Compare against an anonymous object / DTO with fewer fields |
| `IgnoringCyclicReferences()`                | Tolerate cycles                         | Trees, bidirectional graphs, EF navigation                  |
| `WithMaxRecursionDepth(n)`                  | Bound recursion                         | Deeply nested structures                                    |
| `WithStrictOrdering()`                      | Order matters                           | Sequences where position is the contract                    |
| `WithoutStrictOrdering()`                   | Order does not matter                   | Default; restate when scoping options                       |
| `RespectingRuntimeTypes()`                  | Compare by runtime type                 | Polymorphic graphs                                          |
| `WithTracing()`                             | Detailed comparison trace               | Debugging a stubborn mismatch                               |

## Pattern 1 — Deep object graph

```csharp
var expected = new Order
{
    Id = 1,
    Customer = new Customer
    {
        Name = "John Doe",
        Address = new Address { Street = "123 Main St", City = "Seattle", ZipCode = "98101" }
    },
    Items = new[]
    {
        new OrderItem { ProductName = "Laptop", Quantity = 1, Price = 999.99m },
        new OrderItem { ProductName = "Mouse",  Quantity = 2, Price = 29.99m }
    }
};

var actual = _sut.GetOrder(1);
actual.Should().BeEquivalentTo(expected);
```

## Pattern 2 — Excluding server-generated / volatile fields

```csharp
actual.Should().BeEquivalentTo(expected, options => options
    .Excluding(x => x.Id)
    .Excluding(x => x.CreatedAt)
    .Excluding(x => x.UpdatedAt));
```

Always pin the dynamic fields separately so the test still has a claim on them:

```csharp
updated.UpdatedAt.Should().BeAfter(original.UpdatedAt);
updated.Version.Should().Be(original.Version + 1);
```

Bulk-exclude with a path predicate:

```csharp
options.Excluding(ctx => ctx.Path.EndsWith("At"))
       .Excluding(ctx => ctx.Path.EndsWith("Time"))
       .Excluding(ctx => ctx.Path.Contains("Timestamp"));
```

By member name (9.3+):

```csharp
actual.Should().BeEquivalentTo(expected, options => options
    .Excluding(ctx => ctx.Name == "InternalState"));
```

## Pattern 3 — Nested timestamp exclusion

Path-pattern exclusions traverse the entire graph, so a single rule catches `Order.CreatedAt`, `Order.Items[].AddedAt`, and `Order.AuditInfo.CreatedAt` together:

```csharp
retrieved.Should().BeEquivalentTo(expected, options => options
    .Excluding(ctx => ctx.Path.EndsWith("At"))
    .Excluding(ctx => ctx.Path.EndsWith("Time")));
```

## Pattern 4 — Cyclic references

Required for trees, parent-child structures, and EF navigation graphs — without it, deep comparison can blow the stack:

```csharp
actualTree.Should().BeEquivalentTo(parent, options => options
    .IgnoringCyclicReferences()
    .WithMaxRecursionDepth(10));
```

Pair `IgnoringCyclicReferences()` with `WithMaxRecursionDepth(n)` — the latter caps the work even when cycles aren't actually what's hurting you.

## Pattern 5 — Selective property comparison (whitelist)

When only a few fields matter, invert the model and use `Including`:

```csharp
actual.Should().BeEquivalentTo(expected, options => options
    .Including(o => o.Id)
    .Including(o => o.CustomerName)
    .Including(o => o.TotalAmount));
```

Often cleaner than a chain of `Excluding` calls, and faster — it short-circuits the graph walk.

## Pattern 6 — Compare against an anonymous object

Validate only key fields without defining a partial DTO. Combine with `ExcludingMissingMembers()` so members absent on the anonymous side are ignored:

```csharp
order.Should().BeEquivalentTo(new
{
    CustomerId  = 123,
    TotalAmount = 999.99m,
    Status      = "Pending"
}, options => options.ExcludingMissingMembers());
```

## Pattern 7 — Ordering rules

`BeEquivalentTo` is **order-insensitive** for collections by default. Opt in explicitly:

```csharp
orderedItems.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
unorderedItems.Should().BeEquivalentTo(expected, options => options.WithoutStrictOrdering());
```

Or use `Equal` instead of `BeEquivalentTo` when you want positional value equality.

## Pattern 8 — EF Core entity comparison

EF tracking properties, shadow state, and navigation collections trip naive comparisons. Drop them explicitly:

```csharp
actual.Should().BeEquivalentTo(expected, options => options
    .ExcludingMissingMembers()      // ignore extra EF members
    .Excluding(o => o.CreatedAt)
    .Excluding(o => o.UpdatedAt)
    .Excluding(o => o.AuditInfo));  // optional navigation
```

## Pattern 9 — API response (extra fields tolerated)

`ExcludingMissingMembers()` makes the comparison "the actual has at least these fields, possibly more":

```csharp
var expected = new UserDto { Id = 1, Username = "john_doe" };
var actual   = await response.Content.ReadFromJsonAsync<UserDto>();

actual.Should().BeEquivalentTo(expected, options => options.ExcludingMissingMembers());
```

## Pattern 10 — Builder-produced expected objects

Pair `BeEquivalentTo` with a test data builder (see [reference/builder-pattern.md](builder-pattern.md)) and exclude the system-generated fields the builder cannot know:

```csharp
var expected = new OrderBuilder().WithId(1).WithCustomer("John Doe").WithItems(3).Build();
var actual   = orderService.CreateOrder(orderRequest);

actual.Should().BeEquivalentTo(expected, options => options
    .Excluding(o => o.OrderNumber)
    .Excluding(o => o.CreatedAt));
```

## Pattern 11 — Comparing across types

`BeEquivalentTo` only requires members to match by name and assignability — domain entity vs DTO with overlapping shape works out of the box:

```csharp
entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
```

## Reusable exclusion extensions

Repeating the same `Excluding(...)` chain in every test is a smell. Extract one helper per category and combine them. The umbrella ships the canonical set in [templates/complex-equivalency/exclusion-strategies.cs](../templates/complex-equivalency/exclusion-strategies.cs):

- `ExcludingAutoGeneratedFields()` — `*At`, `*Time`, `Version`, `RowVersion`, `Timestamp`.
- `ExcludingAuditFields()` — `CreatedBy/At`, `ModifiedBy/At`, `UpdatedBy/At`, `LastModified`.
- `ExcludingAllTimeFields()` — every `DateTime`/`DateTimeOffset` (and nullable variants).
- `ExcludingCommonAutoFields()` — composition of the above two.
- `ExcludingEntityFrameworkFields()` — adds `Navigation`/`Proxy` + `ExcludingMissingMembers()` for EF tracking properties.

Usage:

```csharp
actual.Should().BeEquivalentTo(expected, o => o.ExcludingCommonAutoFields());
```

### Conditional exclusion

When a property should only be excluded in some environments:

```csharp
public static EquivalencyOptions<T> ExcludingWhen<T>(
    this EquivalencyOptions<T> options,
    Expression<Func<T, object>> propertySelector,
    bool condition) =>
    condition ? options.Excluding(propertySelector) : options;

actual.Should().BeEquivalentTo(expected, options => options
    .ExcludingAllTimeFields()
    .ExcludingWhen(p => p.StockQuantity, condition: isIntegrationTest));
```

## Performance: large datasets

Full graph comparison on tens of thousands of items is slow and the failure output is unreadable. Strategies, in order of preference:

1. **Count check first** — fail fast if size is wrong.
2. **Whitelist key properties** with `Including`.
3. **Sample-based comparison** for spot checks across the dataset.
4. **Aggregate statistics** (`processed.Count(r => r.IsProcessed).Should().Be(total)`).

Sample-based helper (full version in templates):

```csharp
actualList.Should().HaveCount(expectedList.Count);

var indices = Enumerable.Range(0, Math.Min(1000, actualList.Count))
    .Select(_ => Random.Shared.Next(actualList.Count))
    .Distinct();

foreach (var i in indices)
    actualList[i].Should().BeEquivalentTo(expectedList[i], o => o.ExcludingCommonAutoFields());
```

Key-only assertion when even sampling is too heavy:

```csharp
ExclusionStrategies.AssertKeyPropertiesOnly(actual, expected,
    o => o.Id, o => o.CustomerName, o => o.TotalAmount);
```

Configure collection display via `AssertionScope.FormattingOptions.MaxItems` (default 32) so failures stay readable on large inputs.

## Batching across many records with `AssertionScope`

```csharp
using (new AssertionScope())
{
    foreach (var order in orders)
    {
        order.Id.Should().BeGreaterThan(0, "Order Id must be > 0");
        order.CustomerName.Should().NotBeNullOrEmpty("CustomerName is required");
        order.TotalAmount.Should().BeGreaterThan(0, "TotalAmount must be > 0");
        order.Items.Should().NotBeEmpty("Order must contain at least one item");
    }
}
```

## Debugging mysterious mismatches

```csharp
actual.Should().BeEquivalentTo(expected, options => options.WithTracing());
```

The failure message gains a step-by-step trace of the comparison — usually exposes a hidden `DateTime.Kind` difference, a property with a custom getter that mutates, or a floating-point drift.

## Troubleshooting

- **`StackOverflowException`** — almost always a cycle. Add `IgnoringCyclicReferences().WithMaxRecursionDepth(10)`.
- **Mismatch but values look identical** — likely a hidden auto-generated field, a `DateTime.Kind` difference, or floating-point drift; turn on `WithTracing()`.
- **Collection comparison fails on order** — decide intentionally: `BeEquivalentTo` (order-insensitive) vs `Equal` / `WithStrictOrdering()`.
- **Slow comparisons** — prefer `Including` on key properties or sample-based assertion over a full graph walk.
- **EF entity mismatch despite matching values** — `ExcludingEntityFrameworkFields()` usually resolves it.

## Best practices

- Prefer `Excluding` over `Including` unless you really only care about two or three properties — `Excluding` documents what's *expected* to change.
- Hoist repeated exclusion chains into named extensions; the test stops reading like a config file.
- Always assert the excluded fields separately when they carry meaning (`UpdatedAt.Should().BeAfter(...)`).
- Use `because:` to explain *why* something is excluded, not just *that* it's excluded — pays off when the test fails for someone else.

## Templates

- [templates/complex-equivalency/comparison-patterns.cs](../templates/complex-equivalency/comparison-patterns.cs) — ten worked patterns covering basic graphs, exclusion, cycles, sampling, EF, and AssertionScope batching.
- [templates/complex-equivalency/exclusion-strategies.cs](../templates/complex-equivalency/exclusion-strategies.cs) — reusable `EquivalencyOptions<T>` extension methods, conditional exclusion, key-property helper.
