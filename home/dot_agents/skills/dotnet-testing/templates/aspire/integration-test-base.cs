namespace MyApp.Tests.Integration.Infrastructure;

/// <summary>
/// Integration test base class — Aspire Testing flavor.
/// All integration test classes should derive from this.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly AspireAppFixture Fixture;
    protected readonly HttpClient HttpClient;
    protected readonly DatabaseManager DatabaseManager;

    protected IntegrationTestBase(AspireAppFixture fixture)
    {
        Fixture = fixture;
        HttpClient = fixture.HttpClient;
        DatabaseManager = new DatabaseManager(() => fixture.GetConnectionStringAsync());
    }

    /// <summary>
    /// Per-test setup — ensure the database schema exists.
    /// </summary>
    public async Task InitializeAsync()
    {
        await DatabaseManager.InitializeDatabaseAsync();
    }

    /// <summary>
    /// Per-test teardown — Respawn the database to keep tests isolated.
    /// </summary>
    public async Task DisposeAsync()
    {
        await DatabaseManager.CleanDatabaseAsync();
    }

    #region Time-control helpers (FakeTimeProvider)

    // If a test class needs to control time, add FakeTimeProvider helpers here.
    // For example:
    // protected void AdvanceTime(TimeSpan timeSpan) { ... }
    // protected void SetTime(DateTimeOffset dateTime) { ... }

    #endregion
}
