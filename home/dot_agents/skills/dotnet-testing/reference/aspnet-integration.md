# ASP.NET Core integration testing

ASP.NET Core integration tests exercise the full request pipeline — routing, model binding, middleware, filters, DI, the controller or endpoint, the response serializer — without leaving the test process. The host runs in-memory via `WebApplicationFactory<TEntryPoint>`; the `HttpClient` it hands you talks to a real `TestServer`, not the network. Unit tests answer "does this method behave correctly in isolation?"; integration tests answer "does the wiring hold up when a real request flows through?"

Position on the test pyramid: roughly the middle 20% of a healthy suite. Unit tests cover branches; integration tests cover composition; end-to-end tests cover the deployed system. If integration tests start dominating the run time, push behavior back into unit tests where possible — most assertions about request bodies, validation rules, and business outcomes belong one layer lower.

FIRST, 3A, and three-part naming still apply — see SKILL.md. Integration tests must clean up after themselves so they remain independent and repeatable across parallel xUnit runs.

## Package set

Pinned in `Directory.Packages.props`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
<PackageReference Include="AwesomeAssertions" />
<PackageReference Include="AwesomeAssertions.Web" />   <!-- HTTP-specific assertions -->
<PackageReference Include="Flurl" />                   <!-- optional URL builder -->
<PackageReference Include="Respawn" />                 <!-- optional row-cleanup for real DBs -->
```

`AwesomeAssertions.Web` only binds against `AwesomeAssertions` (>= 8.0). The older `FluentAssertions.Web` family pairs with `FluentAssertions` (< 8.0) or `FluentAssertions.Web.v8` (>= 8.0) — pick the row matching the base assertion library pinned in the consumer.

For the `Program` class to be visible to the test project, add this to the API project's `Program.cs`:

```csharp
public partial class Program { }
```

(Top-level `Program.cs` files are `internal` by default; the test project's `WebApplicationFactory<Program>` constraint needs a public reference type to bind against.) A complete `Directory.Packages.props`-friendly test `.csproj` lives at [templates/aspnet-integration/test-project.csproj](../templates/aspnet-integration/test-project.csproj).

## `WebApplicationFactory` basics

The factory boots the API in-memory and gives you an `HttpClient` pointed at it. Share one instance per test class via `IClassFixture`:

```csharp
public sealed class ProductsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProductsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetById_WhenProductExists_ShouldReturnProduct()
    {
        var response = await _client.GetAsync("/products/1");

        response.Should().Be200Ok()
            .And.Satisfy<Product>(p => p.Id.Should().Be(1));
    }
}
```

`AwesomeAssertions.Web` provides the fluent HTTP assertions: `Be200Ok()`, `Be201Created()`, `Be204NoContent()`, `Be400BadRequest()`, `Be404NotFound()`, `Be422UnprocessableEntity()`, `Be500InternalServerError()`. The `Satisfy<T>` extension deserializes the body and lets you assert against the typed result — no manual `ReadAsStringAsync` + `JsonSerializer.Deserialize`. For request bodies, prefer `System.Net.Http.Json` (`PostAsJsonAsync`, `ReadFromJsonAsync<T>`) — the traditional `JsonSerializer.Serialize` + `StringContent` + manual deserialize is ten lines for the same effect. The full status-code / Satisfy / BeAs / composed-CRUD catalogue is in [templates/aspnet-integration/webfactory-http-assertions.cs](../templates/aspnet-integration/webfactory-http-assertions.cs).

## Three levels of integration test

Match the test setup to what the API actually depends on. Don't pay for database wiring in a test that hits a controller with no DB.

| Level | Shape                | Test focus                                     | Factory wiring                                                                  |
| ----- | -------------------- | ---------------------------------------------- | ------------------------------------------------------------------------------- |
| 1     | No DB, no services   | Routing, model binding, status codes           | Use `WebApplicationFactory<Program>` directly                                   |
| 2     | Service layer, no DB | DI registration, controller -> service path    | Subclass factory, replace services with substitutes via `ConfigureTestServices` |
| 3     | Full stack, real DB  | CRUD, ProblemDetails, validation, transactions | Subclass factory, replace `DbContextOptions` with InMemory or Testcontainers    |

Most controller tests are Level 2 or 3. Level 1 tests catch routing regressions cheaply and run very fast — keep a handful even when the project has progressed to Level 3.

## Configuring services for tests

The default factory boots the API exactly as in production. To swap dependencies (substitute an external service, replace the database, override `TimeProvider`), subclass the factory and use `ConfigureTestServices`:

```csharp
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPaymentGateway>()));
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FakeTimeProvider()));
        });
    }
}
```

`ConfigureTestServices` runs *after* the app's own `ConfigureServices`, so `services.Replace(...)` (from `Microsoft.Extensions.DependencyInjection.Extensions`) reliably overrides production registrations. `AddSingleton<T>(...)` on its own appends a second registration, which can leave the production one resolving first depending on container behavior — always `Replace` for substitutions, or `RemoveAll<T>()` then add.

For pre-built service stubs, an alternative is to pass them through the factory constructor and resolve them inside `ConfigureTestServices`:

```csharp
public sealed class ServiceStubFactory(IExampleService stub) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<IExampleService>();
            s.AddScoped(_ => stub);
        });
}
```

The full Level-3 factory (InMemory database swap, multiple service replacements, environment + configuration overrides, optional seeders) lives at [templates/aspnet-integration/webfactory-basics.cs](../templates/aspnet-integration/webfactory-basics.cs). The Testcontainers variant (PostgreSQL + Redis + `FakeTimeProvider`) is at [templates/aspnet-integration/webapi-testcontainers-factory.cs](../templates/aspnet-integration/webapi-testcontainers-factory.cs).

## Authentication and authorization in tests

Real auth handlers expect a real identity provider. For tests, register a fake auth handler that admits a known principal:

```csharp
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "Admin"),
        };
        var identity  = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

// In the factory:
builder.ConfigureTestServices(services =>
{
    services.AddAuthentication(defaultScheme: "Test")
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
});
```

For tests that need an *unauthenticated* request, instantiate a separate `HttpClient` from a factory variant that doesn't wire the test scheme, or skip the `Authorization` header — the production policy will reject it as expected.

## Full CRUD workflows

A controller test verifies the round-trip: create, read it back, update, read it back again, delete, confirm gone. Each step asserts the right status code and the right body shape.

```csharp
[Fact]
public async Task ProductLifecycle_WhenCreatedUpdatedDeleted_ShouldHandleAllTransitions()
{
    var create = new CreateProductRequest("Widget", 9.99m);

    var createResponse = await _client.PostAsJsonAsync("/products", create);
    createResponse.Should().Be201Created()
        .And.Satisfy<Product>(p =>
        {
            p.Name.Should().Be("Widget");
            p.Price.Should().Be(9.99m);
        });
    var created = await createResponse.Content.ReadFromJsonAsync<Product>();

    var readResponse = await _client.GetAsync($"/products/{created!.Id}");
    readResponse.Should().Be200Ok();

    var update = new UpdateProductRequest("Widget v2", 12.99m);
    var updateResponse = await _client.PutAsJsonAsync($"/products/{created.Id}", update);
    updateResponse.Should().Be200Ok();

    var deleteResponse = await _client.DeleteAsync($"/products/{created.Id}");
    deleteResponse.Should().Be204NoContent();

    var goneResponse = await _client.GetAsync($"/products/{created.Id}");
    goneResponse.Should().Be404NotFound();
}
```

This is *one* Act per assertion block — the test still respects 3A, just repeated for each transition. If the test would be clearer as four separate `[Fact]`s, split it; the lifecycle form is justified only when later steps materially depend on earlier ones. A full CRUD suite — create, read, update, delete, pagination, keyword filter, validation errors — is in [templates/aspnet-integration/webapi-crud-tests.cs](../templates/aspnet-integration/webapi-crud-tests.cs).

### URL construction

For long query strings, Flurl makes the URL self-documenting:

```csharp
var url = "/products"
    .SetQueryParam("pageSize", 5)
    .SetQueryParam("page", 2)
    .SetQueryParam("keyword", "special");
var response = await _client.GetAsync(url);
```

For simple paths, plain string interpolation is fine. Wrap the `HttpClient` in `new FlurlClient(httpClient)` when the test wants the full Flurl chaining surface, but for most cases the URL builder alone is enough.

## Error response validation

Modern ASP.NET Core returns errors as `ProblemDetails` (RFC 7807) or `ValidationProblemDetails`. Validate both the status code *and* the payload shape:

```csharp
[Fact]
public async Task Create_WhenNameIsEmpty_ShouldReturnValidationProblem()
{
    var request = new CreateProductRequest("", 9.99m);

    var response = await _client.PostAsJsonAsync("/products", request);

    response.Should().Be400BadRequest()
        .And.Satisfy<ValidationProblemDetails>(problem =>
        {
            problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
            problem.Title.Should().Be("One or more validation errors occurred.");
            problem.Errors.Should().ContainKey(nameof(CreateProductRequest.Name));
        });
}
```

For business-logic exceptions, register an `IExceptionHandler` (ASP.NET Core 8+) that maps exception types to status codes and returns `ProblemDetails`. Handlers run in registration order — register specific ones (FluentValidation, custom domain exceptions) before the generic fallback:

```csharp
// Program.cs
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<FluentValidationExceptionHandler>(); // specific
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();           // fallback
app.UseExceptionHandler();
```

A `FluentValidationExceptionHandler.TryHandleAsync` returns `false` when the exception isn't a `ValidationException`, letting the next handler take over. Full implementations: [templates/aspnet-integration/global-exception-handler.cs](../templates/aspnet-integration/global-exception-handler.cs) (`ArgumentException`/`KeyNotFoundException`/`UnauthorizedAccessException`/`TimeoutException`/`InvalidOperationException`/default mapping) and [templates/aspnet-integration/fluent-validation-exception-handler.cs](../templates/aspnet-integration/fluent-validation-exception-handler.cs) (FluentValidation → `ValidationProblemDetails`). See [reference/fluentvalidation.md](fluentvalidation.md) for the validator side of this pipeline.

## Database integration

When the endpoint reads or writes a database, swap the production `DbContextOptions` for either an in-memory provider (cheap, lossy) or a Testcontainers fixture (slower, real engine behavior):

```csharp
services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase("Tests"));

using var scope = services.BuildServiceProvider().CreateScope();
scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
```

In-memory database is appropriate when the queries are simple LINQ that the provider translates faithfully. The moment the production query uses raw SQL, window functions, JSON columns, vendor-specific functions, transactions, or relies on row-level concurrency tokens, the in-memory provider lies — every `SaveChanges` "succeeds", case-sensitivity defaults change, and concurrent writes don't conflict the way the real engine would.

When that becomes the bottleneck for the test, switch to Testcontainers. See [reference/testcontainers.md](testcontainers.md) for the per-engine lifecycle pattern and the `Collection Fixture` model for container reuse across a class set. The `TestWebApplicationFactory` pattern — implementing `IAsyncLifetime` and starting the container in `InitializeAsync` — slots straight into the same `IClassFixture` or `[Collection]` hooks an integration suite already uses.

```csharp
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redis;
    public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        _redis    = new RedisBuilder().WithImage("redis:7-alpine").Build();
        await _postgres.StartAsync();
        await _redis.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(cfg =>
        {
            cfg.Sources.Clear();
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres!.GetConnectionString(),
                ["ConnectionStrings:Redis"]             = _redis!.GetConnectionString(),
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(TimeProvider));
        });

        builder.UseEnvironment("Testing");
    }

    public new async Task DisposeAsync()
    {
        if (_postgres is not null) await _postgres.DisposeAsync();
        if (_redis    is not null) await _redis.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

For row-level cleanup between tests against a real database, Respawn is the canonical choice — initialize a `Respawner` against the container's connection in `InitializeAsync`, then call `respawner.ResetAsync(connectionString)` in `DisposeAsync`. Avoid `EnsureDeleted`/`EnsureCreated` between tests for real engines — the schema rebuild dominates the runtime. The full `DatabaseManager` (Respawn integration + SQL-script schema bootstrap + helpers) is in [templates/aspnet-integration/webapi-database-manager.cs](../templates/aspnet-integration/webapi-database-manager.cs); a sample table script lives at [templates/aspnet-integration/create-products-table.sql](../templates/aspnet-integration/create-products-table.sql).

## Shared test base

Centralize cleanup, seeding, and `HttpClient` plumbing in a base class so individual tests stay focused. The two common shapes are an `IClassFixture` (one factory per test class) or a `[Collection]` (one factory shared across many classes).

```csharp
[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        Client  = factory.CreateClient();
    }

    public virtual async Task InitializeAsync() => await CleanupDatabaseAsync();
    public virtual Task DisposeAsync() => Task.CompletedTask;

    protected async Task CleanupDatabaseAsync() { /* … */ }
    protected async Task<Product> SeedProductAsync(string name, decimal price) { /* … */ }
    protected void ResetTime() => Factory.TimeProvider.SetUtcNow(/* … */);
    protected void AdvanceTime(TimeSpan delta) => Factory.TimeProvider.Advance(delta);
}

[CollectionDefinition("Integration Tests")]
public sealed class IntegrationCollection : ICollectionFixture<TestWebApplicationFactory> { }
```

`InitializeAsync` runs before each test (`IAsyncLifetime` is implemented per-instance, and xUnit creates a fresh test class instance per test). That gives every test a clean database without rebuilding the host. The InMemory-EF variant (seed/cleanup/HTTP helpers) is in [templates/aspnet-integration/webfactory-test-base.cs](../templates/aspnet-integration/webfactory-test-base.cs); the Testcontainers `[Collection]` variant with `FakeTimeProvider` is in [templates/aspnet-integration/webapi-collection-fixture.cs](../templates/aspnet-integration/webapi-collection-fixture.cs).

## Project layout

```text
tests/
├── Sample.Api.UnitTests/                # unit tests
├── Sample.Api.IntegrationTests/         # integration tests
│   ├── Fixtures/
│   │   ├── TestWebApplicationFactory.cs
│   │   ├── IntegrationTestCollection.cs
│   │   └── IntegrationTestBase.cs
│   ├── Handlers/
│   │   ├── GlobalExceptionHandler.cs
│   │   └── FluentValidationExceptionHandler.cs
│   ├── Helpers/
│   │   ├── DatabaseManager.cs
│   │   └── TestHelpers.cs
│   ├── SqlScripts/
│   │   └── Tables/
│   ├── Auth/
│   │   └── TestAuthHandler.cs
│   └── Controllers/
│       └── ProductsControllerTests.cs
└── Sample.Api.E2ETests/                 # end-to-end tests
```

Keep integration tests in their own project. Mixing them with unit tests slows the unit run and makes it tempting to share fixtures across categories — exactly what FIRST forbids.

## Pitfalls

- **HTTP context bleed across tests.** Each test gets its own `HttpClient`, but the underlying `TestServer` is shared via `IClassFixture`/`[Collection]`. State that lives in singletons (caches, in-memory stores) persists across tests in the same group. Either clean it in `InitializeAsync` or move singletons that should be per-test into `ConfigureTestServices` as `Scoped`/`Transient`.
- **In-process vs out-of-process hosting differences.** The test host runs in-process; production may run Kestrel or IIS out-of-process. Behaviors that depend on the host model (`HttpContext.Connection.RemoteIpAddress`, header forwarding, the `Server` header) will differ. Test those at the deployed boundary, not here.
- **Order-dependent test data.** xUnit doesn't guarantee test order, and parallel runs make it worse. Always seed in `InitializeAsync`; never rely on a previous test leaving data behind.
- **`AddSingleton` instead of `Replace`.** Appending a duplicate registration may or may not override the original depending on resolution order. Use `services.Replace(...)` (or `RemoveAll<T>()` then `AddX<T>()`) for substitutions.
- **Assertion package mismatch.** `AwesomeAssertions.Web` is the extension for `AwesomeAssertions`. Older `FluentAssertions.Web` won't bind against it. Check `Directory.Packages.props` for the pinned pair; if you see `ObjectAssertions does not contain a definition for 'Be200Ok'`, you have the wrong combination.
- **Forgetting `public partial class Program`.** Without it, `WebApplicationFactory<Program>` won't compile in the test project.
- **Over-mocking.** Integration tests that substitute everything are unit tests in disguise. Use real components for the things integration is supposed to exercise (routing, middleware, model binding, EF Core); substitute only the things that would make the test slow, flaky, or non-deterministic (external HTTP, payment gateways, mail senders).
- **In-memory EF lying about transactional behavior.** `SaveChanges` always succeeds, concurrency tokens are ignored, case-sensitivity flips. The moment a production query depends on engine behavior, switch to Testcontainers.
- **Exception-handler ordering.** Specific handlers (FluentValidation, domain) must register *before* the generic fallback — handlers run in registration order, and the first one to return `true` wins.
- **Forgetting `cfg.Sources.Clear()` when overriding connection strings.** If you only `AddInMemoryCollection` on top of the existing sources, the production `appsettings.json` value can win depending on the resolution order.

## Templates

- [templates/aspnet-integration/webfactory-basics.cs](../templates/aspnet-integration/webfactory-basics.cs) — `CustomWebApplicationFactory<TProgram>` with InMemory EF swap, service replacements, environment + configuration overrides.
- [templates/aspnet-integration/webfactory-test-base.cs](../templates/aspnet-integration/webfactory-test-base.cs) — `IntegrationTestBase` with seed/cleanup/HTTP helpers.
- [templates/aspnet-integration/webfactory-http-assertions.cs](../templates/aspnet-integration/webfactory-http-assertions.cs) — `AwesomeAssertions.Web` catalogue: status codes, `Satisfy<T>`, `BeAs`, composed CRUD.
- [templates/aspnet-integration/webapi-testcontainers-factory.cs](../templates/aspnet-integration/webapi-testcontainers-factory.cs) — `TestWebApplicationFactory` with PostgreSQL + Redis + `FakeTimeProvider`.
- [templates/aspnet-integration/webapi-collection-fixture.cs](../templates/aspnet-integration/webapi-collection-fixture.cs) — `[CollectionDefinition]` + `IntegrationTestBase` with Flurl + time control.
- [templates/aspnet-integration/webapi-database-manager.cs](../templates/aspnet-integration/webapi-database-manager.cs) — Respawn integration, SQL-script schema bootstrap, seeders.
- [templates/aspnet-integration/webapi-crud-tests.cs](../templates/aspnet-integration/webapi-crud-tests.cs) — full CRUD test suite with `ProblemDetails`, `ValidationProblemDetails`, Flurl pagination.
- [templates/aspnet-integration/global-exception-handler.cs](../templates/aspnet-integration/global-exception-handler.cs) — `IExceptionHandler` fallback returning `ProblemDetails` (mapping for `ArgumentException`/`KeyNotFoundException`/etc.).
- [templates/aspnet-integration/fluent-validation-exception-handler.cs](../templates/aspnet-integration/fluent-validation-exception-handler.cs) — FluentValidation -> `ValidationProblemDetails`.
- [templates/aspnet-integration/test-project.csproj](../templates/aspnet-integration/test-project.csproj) — sample integration-test `.csproj`.
- [templates/aspnet-integration/create-products-table.sql](../templates/aspnet-integration/create-products-table.sql) — PostgreSQL schema bootstrap.

## Cross-references

- [reference/testcontainers.md](testcontainers.md) — real database engines for cases where in-memory EF lies; multi-container orchestration combines cleanly with `WebApplicationFactory` + `IAsyncLifetime`.
- [reference/nsubstitute.md](nsubstitute.md) — substituting external services in `ConfigureTestServices`.
- [reference/awesome-assertions.md](awesome-assertions.md) — base fluent assertions; `AwesomeAssertions.Web` builds on it.
- [reference/datetime.md](datetime.md) — `FakeTimeProvider` registration for time-sensitive endpoints.
- [reference/fluentvalidation.md](fluentvalidation.md) — validator behavior surfaced as `ValidationProblemDetails`.
- [reference/aspire.md](aspire.md) — when the integration spans multiple services, prefer Aspire's `DistributedApplication` host over a hand-rolled multi-factory setup.
