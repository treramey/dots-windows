# .NET Aspire Integration Testing

`Aspire.Hosting.Testing` lets integration tests boot the same `AppHost` topology you ship to production — Postgres, Redis, your API project, and every other resource the AppHost declares — inside a `DistributedApplicationTestingBuilder`. If the project is not already using Aspire, prefer plain Testcontainers ([reference/testcontainers.md](testcontainers.md)) instead of adopting Aspire just for tests.

## Prerequisites

- .NET 8 SDK or higher.
- Docker (Desktop, WSL 2, or Hyper-V).
- An AppHost project that declares the application topology — without one, `Aspire.Hosting.Testing` has nothing to bring up.

## Aspire Testing vs Testcontainers

| Aspect                | .NET Aspire Testing           | Testcontainers                 |
| --------------------- | ----------------------------- | ------------------------------ |
| Goal                  | Cloud-native distributed apps | Generic container-backed tests |
| Configuration         | Declarative AppHost project   | Imperative per-container code  |
| Service orchestration | Automatic (via AppHost)       | Manual                         |
| Learning curve        | Higher                        | Lower                          |
| Best for              | Projects already on Aspire    | Traditional .NET solutions     |

Pick Aspire testing when the project already runs on Aspire, you need to exercise interactions across services, and a unified dev/test topology is valuable. Pick Testcontainers when the project is a traditional .NET solution, you want fine-grained control over individual containers, or the team isn't already invested in Aspire.

## Project shape

```text
MyApp/
├── src/
│   ├── MyApp.Api/                    # Web API
│   ├── MyApp.Application/
│   ├── MyApp.Domain/
│   └── MyApp.Infrastructure/
├── MyApp.AppHost/                    # Aspire orchestration (required)
│   ├── MyApp.AppHost.csproj
│   └── Program.cs
└── tests/
    └── MyApp.Tests.Integration/
        ├── Infrastructure/
        │   ├── AspireAppFixture.cs
        │   ├── IntegrationTestCollection.cs
        │   ├── IntegrationTestBase.cs
        │   ├── DatabaseManager.cs
        │   └── TestHelpers.cs
        └── Controllers/
            └── MyControllerTests.cs
```

## AppHost

The host declares every resource and project the suite needs. Aspire wires connection strings and service discovery via `WithReference` — don't pin endpoints manually.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
                      .WithLifetime(ContainerLifetime.Session);
var postgresDb = postgres.AddDatabase("productdb");

var redis = builder.AddRedis("redis")
                   .WithLifetime(ContainerLifetime.Session);

var apiProject = builder.AddProject<Projects.MyApp_Api>("myapp-api")
                        .WithReference(postgresDb)
                        .WithReference(redis);

builder.Build().Run();
```

**Full AppHost example:** [templates/aspire/apphost-program.cs](../templates/aspire/apphost-program.cs)
**AppHost csproj:** [templates/aspire/apphost-project.csproj](../templates/aspire/apphost-project.csproj)

## Required packages

Versions live in `Directory.Packages.props` — do not pin them in the csproj files.

- AppHost project: `Aspire.AppHost.Sdk` SDK plus `Aspire.Hosting.AppHost` and `Aspire.Hosting.<Resource>` integrations (`Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.Redis`, `Aspire.Hosting.SqlServer`, `Aspire.Hosting.MongoDB`, `Aspire.Hosting.RabbitMQ`, etc.) plus project references to the apps being orchestrated.
- Test project: `Aspire.Hosting.Testing`, `Microsoft.AspNetCore.Mvc.Testing`, `Respawn`, plus the umbrella's standard test stack (xunit, NSubstitute, AwesomeAssertions, AutoFixture, coverlet.collector) and a project reference to the AppHost.

**Test csproj:** [templates/aspire/test-project.csproj](../templates/aspire/test-project.csproj)

## Lifecycle: one DistributedApplication per session

Spinning the AppHost up per test class is too slow. Use an xUnit collection fixture so all tests share the same instance.

```csharp
public sealed class AspireAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MyApp_AppHost>();

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        await WaitForServicesReadyAsync();

        _httpClient = _app.CreateHttpClient("myapp-api", "http");
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        if (_app != null) await _app.DisposeAsync();
    }
    // GetConnectionStringAsync / WaitForServicesReadyAsync omitted; see template.
}
```

**Full fixture:** [templates/aspire/aspire-app-fixture.cs](../templates/aspire/aspire-app-fixture.cs)
**Collection definition:** [templates/aspire/integration-test-collection.cs](../templates/aspire/integration-test-collection.cs)
**Test base class:** [templates/aspire/integration-test-base.cs](../templates/aspire/integration-test-base.cs)

## Container lifetime

Default to `ContainerLifetime.Session` so resources are cleaned up when the test session ends. `Persistent` keeps containers running across sessions and requires manual cleanup — avoid unless you have a concrete reason.

## Wait for resource readiness

Container start ≠ service ready. The AppHost has signalled it started before Postgres is accepting connections; tests must poll. Bounded retry against an external signal is fine here — it isn't the banned `Thread.Sleep` "wait for code to finish":

```csharp
private async Task WaitForPostgreSqlReadyAsync()
{
    const int maxRetries = 30;
    const int delayMs    = 1000;

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var connectionString = await App.GetConnectionStringAsync("postgres");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return;
        }
        catch when (i < maxRetries - 1)
        {
            await Task.Delay(delayMs);
        }
    }

    throw new InvalidOperationException("PostgreSQL did not become ready.");
}
```

## Bring the application database into existence

The Aspire Postgres resource starts a server, not your application's database. Create it before any test runs — connect to the default `postgres` database, check `pg_database`, run `CREATE DATABASE` if needed.

## `DatabaseManager` — schema + Respawn

`DatabaseManager` is the seam between the AppHost-supplied connection string and the Respawn-backed cleanup. Each test gets a fresh database via `Respawner.ResetAsync` between runs.

```csharp
_respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
{
    TablesToIgnore   = new Table[] { "__EFMigrationsHistory" },
    SchemasToInclude = new[] { "public" },
    DbAdapter        = DbAdapter.Postgres
});
```

**Full DatabaseManager:** [templates/aspire/database-manager.cs](../templates/aspire/database-manager.cs)
**Seed/probe helpers:** [templates/aspire/test-helpers.cs](../templates/aspire/test-helpers.cs)

Respawn 7.0 notes:

- Adapter can be inferred from the `DbConnection` (Postgres for `NpgsqlConnection`); keep it explicit anyway.
- `Microsoft.Data.SqlClient` is no longer a transitive dependency — reference it yourself if you need SQL Server.
- New adapters: SQLite, IBM DB2, Snowflake.
- `FormatDeleteStatement` lets you customise the generated DELETE.

## AppHost configuration mistakes

Let Aspire wire endpoints; don't pin them manually:

```csharp
// Wrong — collides with the endpoint Aspire allocates.
builder.AddProject<Projects.MyApp_Api>("my-api")
       .WithHttpEndpoint(port: 8080, name: "http");

// Right — reference resources, let Aspire pick the ports.
builder.AddProject<Projects.MyApp_Api>("my-api")
       .WithReference(postgresDb)
       .WithReference(redis);
```

## Inter-service communication tests

`App.CreateHttpClient("resource-name")` returns an `HttpClient` aimed at whichever Aspire-allocated endpoint that resource is on, so tests don't care about ports. With Flurl for URL building and AwesomeAssertions.Web for fluent HTTP assertions:

```csharp
[Fact]
public async Task GetProducts_WithPagingParameters_ShouldReturnRequestedPage()
{
    await TestHelpers.SeedProductsAsync(DatabaseManager, 15);

    var url = "/products".SetQueryParam("pageSize", 5).SetQueryParam("page", 2);
    var response = await HttpClient.GetAsync(url);

    response.Should().Be200Ok()
            .And.Satisfy<PagedResult<ProductResponse>>(result =>
            {
                result.Total.Should().Be(15);
                result.PageCount.Should().Be(3);
                result.Items.Should().HaveCount(5);
            });
}
```

**Full controller test class:** [templates/aspire/controller-tests.cs](../templates/aspire/controller-tests.cs)

## Testable time

Abstract `DateTime.UtcNow` behind `TimeProvider`, register `TimeProvider.System` in production, and swap to `FakeTimeProvider` in tests:

```csharp
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
```

See [reference/datetime.md](datetime.md) for `FakeTimeProvider` usage.

## Dapper + Postgres column mapping

Postgres returns snake_case; Dapper expects PascalCase. Either initialise a global mapping or alias in SQL:

```csharp
DapperTypeMapping.Initialize();
// or
const string sql = @"
    SELECT id, name, price,
           created_at AS CreatedAt,
           updated_at AS UpdatedAt
    FROM products";
```

## Checklist

- [ ] AppHost project exists and declares every resource the tests need.
- [ ] All containers use `ContainerLifetime.Session`.
- [ ] `AspireAppFixture` polls each external resource for readiness — start ≠ ready.
- [ ] Application database is created before tests run.
- [ ] Respawn is configured with the correct adapter and ignored tables.
- [ ] No manual `WithHttpEndpoint(port: …)` — Aspire allocates ports.
- [ ] Tests share one `DistributedApplication` via `ICollectionFixture<>`.
- [ ] `TimeProvider` is registered for time-dependent services.

## Cross-links

- Testcontainers for non-Aspire projects — [reference/testcontainers.md](testcontainers.md)
- ASP.NET integration testing — [reference/aspnet-integration.md](aspnet-integration.md)
- `FakeTimeProvider` — [reference/datetime.md](datetime.md)
- AwesomeAssertions HTTP helpers — [reference/awesome-assertions.md](awesome-assertions.md)
- Builder pattern for test entities — [reference/builder-pattern.md](builder-pattern.md)
