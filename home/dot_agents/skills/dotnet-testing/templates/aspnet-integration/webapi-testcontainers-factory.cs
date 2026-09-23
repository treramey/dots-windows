// =============================================================================
// WebApi integration testing — TestWebApplicationFactory with Testcontainers
// PostgreSQL + Redis containers + FakeTimeProvider.
// =============================================================================

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace YourProject.Tests.Integration.Fixtures;

/// <summary>
/// Test WebApplicationFactory that manages Testcontainers and service overrides.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private RedisContainer? _redisContainer;
    private FakeTimeProvider? _timeProvider;

    public PostgreSqlContainer PostgresContainer => _postgresContainer
        ?? throw new InvalidOperationException("PostgreSQL container not initialized");

    public RedisContainer RedisContainer => _redisContainer
        ?? throw new InvalidOperationException("Redis container not initialized");

    public FakeTimeProvider TimeProvider => _timeProvider
        ?? throw new InvalidOperationException("TimeProvider not initialized");

    /// <summary>Initialise Testcontainers.</summary>
    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("test_db")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithCleanUp(true)
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();

        // FakeTimeProvider with a fixed starting time.
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    /// <summary>Configure the test WebHost.</summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            // Drop existing sources and provide test-only configuration.
            config.Sources.Clear();

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = PostgresContainer.GetConnectionString(),
                ["ConnectionStrings:Redis"] = RedisContainer.GetConnectionString(),
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:System"] = "Warning",
                ["Logging:LogLevel:Microsoft"] = "Warning"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace TimeProvider with the FakeTimeProvider.
            var timeProviderDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TimeProvider));
            if (timeProviderDescriptor != null)
            {
                services.Remove(timeProviderDescriptor);
            }
            services.AddSingleton<TimeProvider>(TimeProvider);
        });

        builder.UseEnvironment("Testing");
    }

    public new async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
