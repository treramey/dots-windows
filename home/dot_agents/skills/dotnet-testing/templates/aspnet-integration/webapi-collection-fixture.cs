// =============================================================================
// WebApi integration testing — Collection Fixture and IntegrationTestBase
// =============================================================================

using Flurl.Http;
using Microsoft.Extensions.Time.Testing;

namespace YourProject.Tests.Integration.Fixtures;

/// <summary>
/// Collection definition — all tests share the same TestWebApplicationFactory.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>
{
    public const string Name = "Integration Tests";
}

/// <summary>
/// Integration test base — Collection Fixture shares the containers.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient HttpClient;
    protected readonly DatabaseManager DatabaseManager;
    protected readonly IFlurlClient FlurlClient;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
        DatabaseManager = new DatabaseManager(factory.PostgresContainer.GetConnectionString());

        // Flurl wraps the same HttpClient.
        FlurlClient = new FlurlClient(HttpClient);
    }

    /// <summary>Before each test — initialise the schema.</summary>
    public virtual async Task InitializeAsync()
    {
        await DatabaseManager.InitializeDatabaseAsync();
        ResetTime();
    }

    /// <summary>After each test — reset rows via Respawn.</summary>
    public virtual async Task DisposeAsync()
    {
        await DatabaseManager.CleanDatabaseAsync();
        FlurlClient.Dispose();
    }

    /// <summary>Reset to the test start time (2024-01-01 00:00:00 UTC).</summary>
    protected void ResetTime() =>
        Factory.TimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>Advance time.</summary>
    protected void AdvanceTime(TimeSpan timeSpan) =>
        Factory.TimeProvider.Advance(timeSpan);

    /// <summary>Set a specific time.</summary>
    protected void SetTime(DateTimeOffset time) =>
        Factory.TimeProvider.SetUtcNow(time);

    /// <summary>Current test time.</summary>
    protected DateTimeOffset GetCurrentTime() =>
        Factory.TimeProvider.GetUtcNow();
}
