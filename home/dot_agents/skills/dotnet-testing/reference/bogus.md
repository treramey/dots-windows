# Bogus — realistic fake data

Bogus produces *believable* test data: actual-looking names, addresses, emails, phone numbers, company names, product names, IBANs. Right tool when the test or seed needs data that looks like production data — integration tests, demo seeds, performance loads, UI prototypes.

For pure unit tests where the data content doesn't matter, prefer AutoFixture — it's lower ceremony. See [reference/autofixture.md](autofixture.md). Three-part naming and FIRST apply — see SKILL.md.

## Core: `Faker<T>` and `RuleFor`

```csharp
using Bogus;

var productFaker = new Faker<Product>()
    .RuleFor(p => p.Id, f => f.IndexFaker)
    .RuleFor(p => p.Name, f => f.Commerce.ProductName())
    .RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000))
    .RuleFor(p => p.Description, f => f.Lorem.Sentence())
    .RuleFor(p => p.CreatedDate, f => f.Date.Past());

var product = productFaker.Generate();          // one
var products = productFaker.Generate(10);       // batch
```

`f` inside the lambda is the underlying `Faker`. `RuleFor` overrides one property at a time; properties without a rule keep their default value. `IndexFaker` is a monotonic counter useful for fake primary keys.

**Full example:** [templates/bogus/basic-usage.cs](../templates/bogus/basic-usage.cs) — five sections covering `Faker<T>`, `RuleFor` variations, seeded randomness, probability/conditional generation, and localisation, each as a runnable xUnit fact.

### `Generate` vs `GenerateLazy`

```csharp
var products = productFaker.Generate(10);              // eager List<T>

IEnumerable<Product> lazyProducts = productFaker.GenerateLazy(100);
var firstFive = lazyProducts.Take(5).ToList();         // creates 5, not 100
```

`GenerateLazy` is the right choice when streaming into a database or piping into LINQ that might short-circuit.

## Common datasets

A whirlwind tour — the full set with every method lives in the template:

```csharp
var faker = new Faker();

// Person   — coherent identity
faker.Person.FullName; faker.Person.Email; faker.Person.DateOfBirth;

// Name     — ad-hoc names (not a single identity)
faker.Name.FirstName(); faker.Name.LastName(); faker.Name.JobTitle();

// Address  — composable street/city/state/country parts
faker.Address.FullAddress(); faker.Address.ZipCode(); faker.Address.Latitude();

// Company / Commerce
faker.Company.CompanyName(); faker.Commerce.ProductName(); faker.Commerce.Price(1, 1000, 2);

// Internet
faker.Internet.Email(); faker.Internet.Url(); faker.Internet.Ip();

// Finance
faker.Finance.CreditCardNumber(); faker.Finance.Iban(); faker.Finance.Amount(100, 10_000, 2);

// Date
faker.Date.Past(); faker.Date.Future(); faker.Date.Between(start, end);

// Random scalars
faker.Random.Int(1, 100); faker.Random.Bool(0.2f); faker.Random.Enum<DayOfWeek>();

// Text
faker.Lorem.Sentence(); faker.Lorem.Paragraph(); faker.Lorem.Slug();
```

**Full example:** [templates/bogus/datasets-examples.cs](../templates/bogus/datasets-examples.cs) — every built-in DataSet (Person, Name, Address, Company, Commerce, Internet, Finance, Date, Lorem, Phone, System, Random, Vehicle, Image, Rant, Hacker, Database) with a runnable example per dataset.

## Cross-property rules

`RuleFor((f, e) => …)` receives the partly-populated object so later rules can derive from earlier ones:

```csharp
var customerFaker = new Faker<Customer>()
    .RuleFor(c => c.Id, f => f.Random.Guid())
    .RuleFor(c => c.Name, f => f.Person.FullName)
    .RuleFor(c => c.Email, (f, c) =>
    {
        var parts = c.Name.Split(' ');
        return f.Internet.Email(parts[0], parts[^1], "company.com");
    });
```

Building block for everything below (cascading levels/salaries, nested children, hierarchical organisations).

## Probability and conditional generation

```csharp
var userFaker = new Faker<User>()
    .RuleFor(u => u.Name, f => f.Person.FullName)
    .RuleFor(u => u.IsPremium, f => f.Random.Bool(0.8f))                      // 80% true
    .RuleFor(u => u.MiddleName, f => f.Name.FirstName().OrNull(f, 0.5f))      // 50% null
    .RuleFor(u => u.Department, f => f.PickRandom("IT", "HR", "Finance"))
    .RuleFor(u => u.Role, f => f.PickRandomWeighted(
        new[] { "User", "Admin", "SuperAdmin" },
        new[] { 0.7f, 0.25f, 0.05f }));
```

Verify distribution as a sanity check:

```csharp
var customers = userFaker.Generate(1000);
customers.Count(c => c.IsPremium).Should().BeInRange(750, 850);   // ~80%
```

## Nested objects and computed totals

`RuleFor` can return a list produced by another `Faker<T>`, and a later `RuleFor` can compute totals from that list:

```csharp
var orderFaker = new Faker<Order>()
    .RuleFor(o => o.Id, f => f.IndexFaker)
    .RuleFor(o => o.OrderDate, f => f.Date.Past())
    .RuleFor(o => o.Items, f =>
    {
        var itemFaker = new Faker<OrderItem>()
            .RuleFor(i => i.ProductName, f => f.Commerce.ProductName())
            .RuleFor(i => i.Quantity, f => f.Random.Int(1, 10))
            .RuleFor(i => i.UnitPrice, f => f.Random.Decimal(10, 500));
        return itemFaker.Generate(f.Random.Int(1, 5));
    })
    .RuleFor(o => o.TotalAmount, (f, o) => o.Items.Sum(i => i.Quantity * i.UnitPrice));
```

## Cascading business rules

For larger domain objects, chain `(f, e)` rules so each property respects the previously-generated state — e.g. level depends on age, salary depends on level, hire date is constrained by age.

```csharp
var employeeFaker = new Faker<Employee>()
    .RuleFor(e => e.Age, f => f.Random.Int(22, 65))
    .RuleFor(e => e.Level, (f, e) => e.Age switch
    {
        < 25 => "Junior", < 35 => "Senior", < 45 => "Lead", _ => "Principal",
    })
    .RuleFor(e => e.Salary, (f, e) => e.Level switch
    {
        "Junior"    => f.Random.Decimal(35_000,  50_000),
        "Senior"    => f.Random.Decimal(50_000,  80_000),
        "Lead"      => f.Random.Decimal(80_000, 120_000),
        "Principal" => f.Random.Decimal(120_000, 200_000),
        _           => f.Random.Decimal(35_000,  50_000),
    });
```

**Full example:** [templates/bogus/advanced-patterns.cs](../templates/bogus/advanced-patterns.cs) — cascading employee rules (with project history), hierarchical organisation build, custom Taiwan DataSet extensions, multi-locale per-row generation, boundary-value data, performance patterns, and database-seeder integration.

## Reproducibility — seeded randomness

A test using fake data must still be *Repeatable* (FIRST). Set the seed:

```csharp
// Process-wide
Randomizer.Seed = new Random(42);

// Or per-Faker
var userFaker = new Faker<User>("en")
    .RuleFor(u => u.Name, f => f.Person.FullName);
userFaker.UseSeed(42);
```

Once seeded, `.Generate()` produces the same sequence every run. Reset to non-deterministic when done:

```csharp
Randomizer.Seed = new Random();   // back to system entropy
```

For database seeders that must be deterministic across environments, set `Randomizer.Seed` once at the top of the seed routine.

## Localisation

```csharp
var twFaker = new Faker<Person>("zh_TW");
var jpFaker = new Faker<Person>("ja");
```

Supported locales include `en_US`, `en_GB`, `zh_CN`, `zh_TW`, `ja`, `ko`, `fr`, `de`, `es`, `it`, `nl`, `pt_BR`, `ru`. Useful when your domain has locale-specific format expectations (phone numbers, postcodes, name order).

Per-row locale picks let you generate multi-region tables:

```csharp
var locales = new[] { "en_US", "zh_TW", "ja", "ko", "fr", "de" };

var globalFaker = new Faker<GlobalUser>()
    .RuleFor(u => u.Locale, f => f.PickRandom(locales))
    .RuleFor(u => u.Name,    (f, u) => new Faker(u.Locale).Person.FullName)
    .RuleFor(u => u.Address, (f, u) => new Faker(u.Locale).Address.FullAddress());
```

## Custom datasets — extension methods

For domain-specific data (your own SKUs, regional postcodes, internal product codes), extend `Faker` with extension methods on a static class. The template ships a complete `TaiwanDataSetExtensions` example with city/district/mobile/landline/ID-card generators.

```csharp
public static class TaiwanDataSetExtensions
{
    public static string TaiwanCity(this Faker faker) => faker.PickRandom("Taipei", "Kaohsiung", "Taichung");

    // Format-correct Taiwan ID card number — not a real valid ID
    public static string TaiwanIdCard(this Faker faker)
    {
        var firstChar    = faker.PickRandom("ABCDEFGHJKLMNPQRSTUVXYWZIO".ToCharArray());
        var genderDigit  = faker.Random.Int(1, 2);
        var digits       = faker.Random.String2(8, "0123456789");
        return $"{firstChar}{genderDigit}{digits}";
    }
}
```

Custom extensions belong in a `Fakers/` folder next to the entity fakers, named after the dataset they extend.

## Boundary-value generation

Bogus is also useful for generating *deliberately ugly* data — empty strings, min/max numerics, XSS payloads, exotic Unicode — to feed property-based or fuzz-style tests:

```csharp
var boundaryFaker = new Faker<TestBoundaryData>()
    .RuleFor(t => t.NullableString, f => f.PickRandom<string?>(null, "", " ", "valid"))
    .RuleFor(t => t.MinValue,       _ => int.MinValue)
    .RuleFor(t => t.MaxValue,       _ => int.MaxValue)
    .RuleFor(t => t.SpecialChars,   f => f.PickRandom(
        "!@#$%^&*()",
        "<script>alert('xss')</script>",
        "emoji: 🎉🔥",
        "Unicode: 日本語"));
```

## Use cases

```csharp
// Unit test that asserts on data shape (email format, name embedded in template)
[Fact]
public void EmailService_GenerateWelcomeEmail_ShouldEmbedNameAndEmail()
{
    var userFaker = new Faker<User>()
        .RuleFor(u => u.Name, f => f.Person.FullName)
        .RuleFor(u => u.Email, f => f.Internet.Email());
    var user = userFaker.Generate();
    var sut = new EmailService();

    var emailContent = sut.GenerateWelcomeEmail(user);

    emailContent.Should().Contain(user.Name);
    emailContent.Should().Contain(user.Email);
}

// Database seeder — deterministic across environments
public static void SeedDatabase(AppDbContext context)
{
    Randomizer.Seed = new Random(42);

    var customerFaker = new Faker<Customer>("en_US")
        .RuleFor(c => c.Name, f => f.Person.FullName)
        .RuleFor(c => c.Email, f => f.Internet.Email());

    context.Customers.AddRange(customerFaker.Generate(100));
    context.SaveChanges();
}
```

Repo convention: seeders live in the consumer application, not in `SvApi.Database.*` packages. See the project CLAUDE.md note.

## Bogus vs AutoFixture — picking one

| Scenario                                                               | Use         | Why                                |
| ---------------------------------------------------------------------- | ----------- | ---------------------------------- |
| Pure unit test, data content doesn't matter                            | AutoFixture | Anonymous data, zero ceremony.     |
| Test that asserts on data *shape* (e.g. email pattern, address layout) | Bogus       | Realistic format.                  |
| Integration test with seeded data                                      | Bogus       | Production-like rows.              |
| UI prototype / demo                                                    | Bogus       | Looks real to humans.              |
| Performance / load test                                                | Bogus       | Realistic distribution and length. |
| Complex object graph with cyclic references                            | AutoFixture | Resolves the graph automatically.  |

You can combine them — AutoFixture produces the SUT and its dependencies; Bogus produces the realistic payload you pass to a method. See the AutoFixture–Bogus integration section of [reference/autofixture.md](autofixture.md).

## Performance — reuse `Faker<T>` instances

Each `Faker<T>` does up-front work to compile its rules. Don't reconstruct it per row when you don't have to:

```csharp
public static class OptimisedDataGenerator
{
    private static readonly Faker<User> _userFaker = new Faker<User>()
        .RuleFor(u => u.Id, f => f.Random.Guid())
        .RuleFor(u => u.Name, f => f.Person.FullName)
        .RuleFor(u => u.Email, f => f.Internet.Email());

    public static List<User> GenerateUsers(int count) => _userFaker.Generate(count);
}
```

For very large generations, batch and stream via `yield return`. For complex fakers used only sometimes, wrap in `Lazy<>` so initialisation cost is paid only on first use. Both patterns are in the template.

## Organisation

```text
MyProject.Tests/
├── Fakers/
│   ├── CustomerFaker.cs
│   ├── OrderFaker.cs
│   └── TaiwanDataSetExtensions.cs
└── Services/
    └── CustomerServiceTests.cs
```

Naming: `{Entity}Faker` for the class, exposing a `static Faker<{Entity}> Default { get; }` or a `New()` factory if per-test customisation is common.

## Pitfalls

- **Over-configuration.** Only set the rules that the test actually depends on. Setting every property turns a six-line test into thirty.
- **Forgetting the seed.** Without `Randomizer.Seed = …` or `Faker.UseSeed(…)`, the suite is technically non-repeatable. Boundary-bug reproductions become impossible.
- **Asserting on the random values.** If the SUT echoes back the data you generated, assert against the *generated value* (`emailContent.Should().Contain(user.Email)`) — never hard-code a string that happens to match this run's randomness.
- **Reconstructing `Faker<T>` per row.** Rule compilation isn't free. Make it `static readonly` (or `Lazy`) once you generate more than a handful of rows.
- **Forgetting `IndexFaker` resets.** `IndexFaker` resets per `Generate()` call — if you need a continuous sequence across calls, use `f.IndexFaker` alone, which keeps counting.

## Packages

- `Bogus` — production-quality fake data. Tests and seeders only.

## Further reading

- [bchavez/Bogus on GitHub](https://github.com/bchavez/Bogus)
- [Bogus on NuGet](https://www.nuget.org/packages/Bogus/)

## Sibling references

[reference/autofixture.md](autofixture.md) · [reference/builder-pattern.md](builder-pattern.md) · [reference/datetime.md](datetime.md) · [reference/fundamentals.md](fundamentals.md)
