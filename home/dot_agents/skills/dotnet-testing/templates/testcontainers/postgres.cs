// PostgreSQL container template — single-class IAsyncLifetime fixture.
// Use this when a test class needs its own isolated database; use the
// Collection Fixture pattern (see mssql.cs) when sharing across classes.

using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace YourNamespace.Tests.Fixtures;

/// <summary>
/// PostgreSQL container fixture — manages the container lifecycle via
/// <see cref="IAsyncLifetime"/>.
/// </summary>
/// <remarks>
/// Use when:
/// - A single test class needs its own database.
/// - You don't need to share the container across classes.
/// - Each test must see a completely fresh environment.
/// </remarks>
public class PostgreSqlContainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private YourDbContext _dbContext = null!;

    public PostgreSqlContainerTests()
    {
        _postgres = new PostgreSqlBuilder()
            // Alpine image keeps the container small and startup fast.
            .WithImage("postgres:15-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            // Random host port to avoid collisions on the dev machine.
            .WithPortBinding(5432, true)
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<YourDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new YourDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ===== Example tests =====

    [Fact]
    public async Task CreateEntity_WithValidData_ShouldPersistToDatabase()
    {
        // Arrange
        var entity = new YourEntity
        {
            Name = "Test entity",
            Description = "Created inside the Postgres container"
        };

        // Act
        _dbContext.YourEntities.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Assert
        var saved = await _dbContext.YourEntities.FindAsync(entity.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test entity");
    }

    [Fact]
    public void GetConnectionString_AfterContainerStarted_ShouldReturnValidConnectionString()
    {
        var connectionString = _postgres.GetConnectionString();
        var mappedPort = _postgres.GetMappedPublicPort(5432);

        connectionString.Should().NotBeNullOrEmpty();
        connectionString.Should().Contain($"Port={mappedPort}");
        connectionString.Should().Contain("Database=testdb");
        connectionString.Should().Contain("Username=testuser");
    }
}

// ===== Variant: explicit Wait Strategy =====

/// <summary>
/// PostgreSQL container configured with an explicit wait strategy so the
/// test does not start work until the engine is fully ready.
/// </summary>
public class PostgreSqlWithWaitStrategyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public PostgreSqlWithWaitStrategyTests()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            // Wait until pg_isready succeeds AND the boot message appears.
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready")
                .UntilMessageIsLogged("database system is ready to accept connections"))
            .Build();
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}

// ===== Variant: tmpfs-backed data directory (CI speed-up) =====

/// <summary>
/// PostgreSQL container using a tmpfs mount for the data directory. The
/// database lives in RAM, so I/O is much faster but data is volatile —
/// perfect for ephemeral test runs in CI.
/// </summary>
public class PostgreSqlWithResourceLimitsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public PostgreSqlWithResourceLimitsTests()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            // RAM-backed filesystem for the data directory.
            .WithTmpfsMount("/var/lib/postgresql/data")
            .Build();
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}

// ===== Replace with your real domain types =====

public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options) : base(options) { }
    public DbSet<YourEntity> YourEntities { get; set; }
}

public class YourEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
