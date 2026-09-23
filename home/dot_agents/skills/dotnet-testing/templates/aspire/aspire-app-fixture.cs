using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace MyApp.Tests.Integration.Infrastructure;

/// <summary>
/// Aspire app testing fixture — manages a distributed application for tests
/// via the .NET Aspire Testing framework.
/// </summary>
public class AspireAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    /// <summary>
    /// The DistributedApplication instance.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException("Application not initialized.");

    /// <summary>
    /// HTTP client for hitting the API service.
    /// </summary>
    public HttpClient HttpClient => _httpClient ?? throw new InvalidOperationException("HTTP client not initialized.");

    /// <summary>
    /// Initialize the Aspire test app.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Build the Aspire testing host using the AppHost-defined topology.
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MyApp_AppHost>();

        // Build and start the app.
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Wait for all services to become ready.
        await WaitForServicesReadyAsync();

        // Create an HTTP client aimed at the API service.
        _httpClient = _app.CreateHttpClient("myapp-api", "http");
    }

    /// <summary>
    /// Wait for all services to become ready.
    /// </summary>
    private async Task WaitForServicesReadyAsync()
    {
        await WaitForPostgreSqlReadyAsync();
        await WaitForRedisReadyAsync();
    }

    /// <summary>
    /// Wait for PostgreSQL readiness.
    /// </summary>
    private async Task WaitForPostgreSqlReadyAsync()
    {
        const int maxRetries = 30;
        const int delayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var connectionString = await GetConnectionStringAsync();
                var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
                builder.Database = "postgres"; // probe the default database

                await using var connection = new Npgsql.NpgsqlConnection(builder.ToString());
                await connection.OpenAsync();
                await connection.CloseAsync();
                Console.WriteLine("PostgreSQL service is ready.");
                return;
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                Console.WriteLine($"Waiting for PostgreSQL readiness, attempt {i + 1}/{maxRetries}: {ex.Message}");
                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException("PostgreSQL did not become ready in time.");
    }

    /// <summary>
    /// Wait for Redis readiness.
    /// </summary>
    private async Task WaitForRedisReadyAsync()
    {
        const int maxRetries = 30;
        const int delayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var connectionString = await GetRedisConnectionStringAsync();
                await using var connection = StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString);
                var database = connection.GetDatabase();
                await database.PingAsync();
                await connection.DisposeAsync();
                Console.WriteLine("Redis service is ready.");
                return;
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                Console.WriteLine($"Waiting for Redis readiness, attempt {i + 1}/{maxRetries}: {ex.Message}");
                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException("Redis did not become ready in time.");
    }

    /// <summary>
    /// Cleanup.
    /// </summary>
    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// Get the PostgreSQL connection string.
    /// </summary>
    public async Task<string> GetConnectionStringAsync()
    {
        return await _app!.GetConnectionStringAsync("productdb")
            ?? throw new InvalidOperationException("PostgreSQL connection string unavailable.");
    }

    /// <summary>
    /// Get the Redis connection string.
    /// </summary>
    public async Task<string> GetRedisConnectionStringAsync()
    {
        return await _app!.GetConnectionStringAsync("redis")
            ?? throw new InvalidOperationException("Redis connection string unavailable.");
    }
}
