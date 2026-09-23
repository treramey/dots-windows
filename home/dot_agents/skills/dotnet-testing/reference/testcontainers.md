# Testcontainers

Containerized integration testing for real database, NoSQL, message-broker, and search-engine behavior under Docker. Spins up a fresh container per test class (or per test, when isolation matters), runs your code against the real engine, and tears it down. The shared rules from `SKILL.md` still apply — naming, 3A, no `Thread.Sleep`, no test ordering.

## When to use

- The code under test issues SQL the in-memory provider does not translate identically (window functions, JSON ops, full-text search, vendor-specific functions, collations).
- The code under test relies on MongoDB / Redis / Elasticsearch semantics that no mock can credibly fake (Redis Lua scripting, Mongo aggregation pipelines, ES scoring, geo queries).
- You are testing an EF Core migration that changes schema in a way the SQLite in-memory provider cannot represent (computed columns, triggers, stored procedures, full-text indexes).
- You need to verify connection-pool, retry, transaction, or concurrency-token behavior end-to-end.
- You are exercising raw Dapper SQL that depends on a specific dialect.

Do **not** reach for Testcontainers when a `Substitute.For<IRepository>()` would do — see [reference/nsubstitute.md](nsubstitute.md). Real containers cost startup time and a Docker daemon; spend that cost only when the test result is actually different.

### Why EF Core InMemory is not enough

`Microsoft.EntityFrameworkCore.InMemory` is fine for unit-testing controller behavior, but it cannot model:

- Transactions and rollback — `SaveChanges` commits unconditionally.
- Locking and concurrency — there is no row-level lock and no `xmin` / `RowVersion` enforcement.
- Translation of nontrivial LINQ — complex `GroupBy`, joins, vendor functions, and JSON columns either silently succeed in-memory and fail in production, or silently differ.
- Collation and case sensitivity — the in-memory provider is case-insensitive; real databases follow the collation.
- Stored procedures, triggers, views, foreign-key constraints, check constraints, computed columns, decimal precision.

Whenever any of the above matters to a test's outcome, the test must run against a real engine — that is what Testcontainers is for.

## Package set

Pinned in `Directory.Packages.props`:

```xml
<PackageVersion Include="Testcontainers" Version="..." />
<PackageVersion Include="Testcontainers.MsSql" Version="..." />          <!-- pick the ones you need -->
<PackageVersion Include="Testcontainers.PostgreSql" Version="..." />
<PackageVersion Include="Testcontainers.MySql" Version="..." />
<PackageVersion Include="Testcontainers.MongoDb" Version="..." />
<PackageVersion Include="Testcontainers.Redis" Version="..." />
<PackageVersion Include="Testcontainers.Elasticsearch" Version="..." />
```

In the test `.csproj`:

```xml
<PackageReference Include="Testcontainers" />
<PackageReference Include="Testcontainers.MsSql" />
<!-- ...add any of the engine modules above -->
```

For SQL Server access, use `Microsoft.Data.SqlClient` — the older `System.Data.SqlClient` is not maintained.

### Docker environment

- Docker Desktop (Windows / macOS) or any Docker-compatible engine (Colima, Rancher Desktop, Podman with the Docker socket shim, Linux native).
- Allocate enough RAM. Multi-container suites (Mongo + Redis + ES) typically need 6 GB+.
- CI agents must have the Docker socket available; gate the test class with `[Trait("Category", "Docker")]` so non-Docker pipelines can exclude it.

## Container lifecycle patterns

### One container per test class

Use `IAsyncLifetime` when each class needs its own pristine engine. Cheapest pattern to reason about, but the startup cost is paid once per class.

```csharp
public sealed class OrderRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder().Build();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        // open connection, run migrations, seed minimum data
    }

    public Task DisposeAsync() => _sql.DisposeAsync().AsTask();

    [Fact]
    public async Task GetById_WhenOrderExists_ShouldReturnOrder() { /* ... */ }
}
```

### Sharing a container across a collection

When startup cost is high and tests in different classes can share the container, use `ICollectionFixture<T>`. Each test class still cleans its own rows. The wall-clock saving is typically 50–80% on small suites and even larger on big ones.

```csharp
[CollectionDefinition(nameof(SqlServerCollectionFixture))]
public class SqlServerCollectionFixture : ICollectionFixture<SqlServerContainerFixture> { }

[Collection(nameof(SqlServerCollectionFixture))]
public sealed class OrderRepositoryTests : IDisposable { /* ... */ }
```

### Cleanup between tests

Truncate or delete, do **not** drop and recreate the database. Per-test cleanup runs in the test thread, in foreign-key order (children first):

```csharp
public void Dispose()
{
    DbContext.Database.ExecuteSqlRaw("DELETE FROM OrderItems");
    DbContext.Database.ExecuteSqlRaw("DELETE FROM Orders");
    DbContext.Database.ExecuteSqlRaw("DELETE FROM Products");
    DbContext.Database.ExecuteSqlRaw("DELETE FROM Categories");
}
```

For non-relational engines, prefer per-test unique keys (`$"user_{Guid.NewGuid():N}"`) over wholesale clears — it lets the same Mongo / Redis container service parallel tests without contention.

### Wait strategies

The Testcontainers default is "wait until the container is `Running`", but `Running` does not mean "ready to accept connections". For engines with a meaningful warm-up phase, pin an explicit wait strategy:

```csharp
var postgres = new PostgreSqlBuilder()
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilCommandIsCompleted("pg_isready")
        .UntilMessageIsLogged("database system is ready to accept connections"))
    .Build();

var sqlServer = new MsSqlBuilder()
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilMessageIsLogged("SQL Server is now ready for client connections"))
    .Build();
```

### Externalized SQL scripts

Keep DDL out of C# strings. Put `.sql` files under `SqlScripts/Tables/` and `SqlScripts/StoredProcedures/`, mark them `<Content CopyToOutputDirectory="Always" />` in the `.csproj`, and load them in dependency order in `InitializeAsync`. This is the only sustainable pattern when more than two or three tables come into play.

## SQL Server

Use `Testcontainers.MsSql`. Image: `mcr.microsoft.com/mssql/server:2022-latest`. SQL Server password policy requires upper/lower/digit/symbol, so dummy passwords like `Test123456!` are typical. The container needs a brief settle delay (~2 s) after the port is open before sustained connections succeed.

```csharp
_container = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .WithPassword("Test123456!")
    .WithCleanUp(true)
    .Build();
```

For test classes that mutate data, prefer the Collection Fixture pattern with row-level cleanup — see [templates/testcontainers/mssql.cs](../templates/testcontainers/mssql.cs) and Dapper-specific patterns in [templates/testcontainers/dapper.cs](../templates/testcontainers/dapper.cs).

## PostgreSQL

Use `Testcontainers.PostgreSql`. Prefer the Alpine image (`postgres:15-alpine`) for fast startup. Pair with `Npgsql.EntityFrameworkCore.PostgreSQL` for EF Core access.

```csharp
_postgres = new PostgreSqlBuilder()
    .WithImage("postgres:15-alpine")
    .WithDatabase("testdb")
    .WithUsername("testuser")
    .WithPassword("testpass")
    .WithPortBinding(5432, true) // random host port
    .Build();
```

Two variants are useful on top of the basic fixture:

- Explicit wait strategy (`pg_isready` + boot log message) for flake-resistant CI.
- A tmpfs mount on `/var/lib/postgresql/data` for RAM-backed I/O, dramatically faster for ephemeral suites.

Full template: [templates/testcontainers/postgres.cs](../templates/testcontainers/postgres.cs).

## MySQL

Use `Testcontainers.MySql`. Image: `mysql:8.0`. Pair with `MySqlConnector` for raw access (or Pomelo / EF Core for ORM access). The pattern mirrors Postgres — the upstream skill bundle focuses on MSSQL/Postgres, so the MySQL template applies the same fixture shape to the MySQL module.

```csharp
_container = new MySqlBuilder()
    .WithImage("mysql:8.0")
    .WithDatabase("testdb")
    .WithUsername("testuser")
    .WithPassword("testpass")
    .Build();
```

Full template: [templates/testcontainers/mysql.cs](../templates/testcontainers/mysql.cs).

## MongoDB

Use `Testcontainers.MongoDb`. Image: `mongo:7.0`. Pair with `MongoDB.Driver` 3.x. The idiomatic pattern is a Collection Fixture (`MongoDbContainerFixture`) shared across every test class, with per-test isolation achieved through unique usernames / emails (`$"user_{Guid.NewGuid():N}"`) rather than wholesale clears. This makes parallel tests safe and keeps each test's intent explicit.

```csharp
_container = new MongoDbBuilder()
    .WithImage("mongo:7.0")
    .WithPortBinding(27017, true)
    .Build();
```

Cover three layers in MongoDB tests:

1. **Document CRUD** — round-trip a domain document, exercise optimistic locking via a `Version` field, hit unique-index violations.
2. **BSON serialization** — `ObjectId.GenerateNewId()`, `BsonNull.Value`, nested `BsonArray`. These do not need a container; they validate driver behavior.
3. **Indexes** — `CreateIndexModel` with `Unique = true`, compound indexes, geospatial.

Full template (CRUD + BSON + index tests): [templates/testcontainers/mongodb.cs](../templates/testcontainers/mongodb.cs).

## Redis

Use `Testcontainers.Redis`. Image: `redis:7.2`. Pair with `StackExchange.Redis` 2.x. Cover all five data structures — String, Hash, List, Set, Sorted Set — plus TTL behavior. Keep tests isolated with a per-test key prefix (`$"isolation:{testId}:..."`) rather than `FLUSHDB`, which most Redis images disable by default.

```csharp
_container = new RedisBuilder()
    .WithImage("redis:7.2")
    .WithPortBinding(6379, true)
    .Build();
```

To clean the database between collections without `FLUSHDB`, scan and delete keys via `IServer.Keys()`:

```csharp
var server = Connection.GetServer(Connection.GetEndPoints().First());
var keys = server.Keys(Database.Database);
if (keys.Any())
{
    await Database.KeyDeleteAsync(keys.ToArray());
}
```

Full template (all five data structures + TTL + isolation): [templates/testcontainers/redis.cs](../templates/testcontainers/redis.cs).

## Elasticsearch

Use `Testcontainers.Elasticsearch`. Image: `docker.elastic.co/elasticsearch/elasticsearch:8.13.0` (pin the major to your cluster). Pair with `Elastic.Clients.Elasticsearch` 8.x.

The container ships with HTTPS and a self-signed certificate by default, and authentication is on. The test client must accept the cert and authenticate as `elastic` with the container's default password:

```csharp
var settings = new ElasticsearchClientSettings(new Uri(_container.GetConnectionString()))
    .ServerCertificateValidationCallback(CertificateValidations.AllowAll)
    .Authentication(new BasicAuthentication("elastic", ElasticsearchBuilder.DefaultPassword))
    .DefaultIndex("test-index");
```

Refresh the index (`Indices.RefreshAsync`) immediately after writes so the document is searchable in the same test — by default the refresh interval is one second and a naive `SearchAsync` will miss the just-indexed document.

Full template: [templates/testcontainers/elasticsearch.cs](../templates/testcontainers/elasticsearch.cs).

## Top-level pitfalls

- **Docker not running on the agent.** CI agents must expose the Docker socket. Tag the test class `[Trait("Category", "Docker")]` and exclude it from pipelines that target Docker-less environments.
- **Port collisions.** Never hard-code host ports. Always read them via `container.GetConnectionString()` or `GetMappedPublicPort()`. The Testcontainers default is a random host port, which is what you want.
- **Image pulls in CI.** The first run on a fresh agent pulls the image and can add 30–60 s. Pre-pull in a CI setup step, or cache the Docker layer between runs.
- **`ContainerNotRunningException` on startup.** Since Testcontainers 4.8 the default wait is "container is running". If the engine itself fails to come up (bad image, insufficient memory, port conflict) you will see this exception — check the container logs, not the test stack trace.
- **Slow suite.** If Testcontainers tests add 30+ seconds to every PR build, the suite has overreached. Push pure-logic tests back to unit tests with `NSubstitute`. Keep the integration tier for behavior that genuinely depends on the engine.
- **`FLUSHDB` failures on Redis.** Many container images disable the admin command set. Use `IServer.Keys()` + `KeyDeleteAsync` instead.
- **Stale Elasticsearch results.** Always `RefreshAsync` after writes if the same test reads back the document.

## Cross-references

- [reference/nsubstitute.md](nsubstitute.md) — first try a substitute. Reach for Testcontainers only when a mock cannot reproduce the behavior under test.
- [reference/aspnet-integration.md](aspnet-integration.md) — when testing an ASP.NET endpoint that needs a real database, combine `WebApplicationFactory` with a Testcontainers fixture.
- [reference/aspire.md](aspire.md) — for projects already on Aspire, use `Aspire.Hosting.Testing` instead of orchestrating containers by hand.
- [reference/coverage.md](coverage.md) — Testcontainers tests count toward integration coverage; configure coverlet to exclude them from unit-test coverage gates.
- [reference/xunit-upgrade.md](xunit-upgrade.md) — xUnit v3 users can use `Testcontainers.XunitV3` (4.9+) to manage lifecycle without manually implementing `IAsyncLifetime`.
