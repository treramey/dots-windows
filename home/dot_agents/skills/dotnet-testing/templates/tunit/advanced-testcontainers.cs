// TUnit + Testcontainers — assembly-level container orchestration.
// [Before(Assembly)] starts all containers once; [After(Assembly)] tears
// them down. Tests read connection details from static properties.

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace TUnit.Advanced.Testcontainers.Examples;

/// <summary>
/// Global infrastructure for the whole test assembly. With real
/// Testcontainers types this becomes PostgreSqlContainer, RedisContainer,
/// KafkaContainer, plus a shared INetwork. See the commented section at
/// the bottom of this file for the real-package version.
/// </summary>
public static class GlobalTestInfrastructureSetup
{
    public static MockPostgreSqlContainer? PostgreSqlContainer { get; private set; }
    public static MockRedisContainer? RedisContainer { get; private set; }
    public static MockKafkaContainer? KafkaContainer { get; private set; }
    public static string? NetworkName { get; private set; }

    [Before(Assembly)]
    public static async Task SetupGlobalInfrastructure()
    {
        NetworkName = "global-test-network";

        PostgreSqlContainer = new MockPostgreSqlContainer
        {
            ConnectionString = "Host=localhost;Database=test_db;Username=test_user;Password=test_password"
        };
        await PostgreSqlContainer.StartAsync();

        RedisContainer = new MockRedisContainer
        {
            ConnectionString = "127.0.0.1:6379"
        };
        await RedisContainer.StartAsync();

        KafkaContainer = new MockKafkaContainer
        {
            BootstrapAddress = "127.0.0.1:9092"
        };
        await KafkaContainer.StartAsync();
    }

    [After(Assembly)]
    public static async Task TeardownGlobalInfrastructure()
    {
        if (KafkaContainer != null) await KafkaContainer.DisposeAsync();
        if (RedisContainer != null) await RedisContainer.DisposeAsync();
        if (PostgreSqlContainer != null) await PostgreSqlContainer.DisposeAsync();
    }
}

// ===== Mocks stand in for real Testcontainers types =====

public class MockPostgreSqlContainer : IAsyncDisposable
{
    public string ConnectionString { get; set; } = string.Empty;
    public string State { get; private set; } = "Created";
    public Task StartAsync() { State = "Running"; return Task.CompletedTask; }
    public ValueTask DisposeAsync() { State = "Stopped"; return ValueTask.CompletedTask; }
}

public class MockRedisContainer : IAsyncDisposable
{
    public string ConnectionString { get; set; } = string.Empty;
    public string State { get; private set; } = "Created";
    public Task StartAsync() { State = "Running"; return Task.CompletedTask; }
    public ValueTask DisposeAsync() { State = "Stopped"; return ValueTask.CompletedTask; }
}

public class MockKafkaContainer : IAsyncDisposable
{
    public string BootstrapAddress { get; set; } = string.Empty;
    public string State { get; private set; } = "Created";
    public Task StartAsync() { State = "Running"; return Task.CompletedTask; }
    public ValueTask DisposeAsync() { State = "Stopped"; return ValueTask.CompletedTask; }
}

// ===== Tests using the shared infrastructure =====

public class ComplexInfrastructureTests
{
    [Test]
    [Property("Category", "Integration")]
    [Property("Infrastructure", "Complex")]
    [DisplayName("Multi-service workflow: Postgres + Redis + Kafka")]
    public async Task CompleteWorkflow_AcrossAllServices_ShouldExposeConnections()
    {
        var dbConnectionString = GlobalTestInfrastructureSetup.PostgreSqlContainer!.ConnectionString;
        var redisConnectionString = GlobalTestInfrastructureSetup.RedisContainer!.ConnectionString;
        var kafkaBootstrapServers = GlobalTestInfrastructureSetup.KafkaContainer!.BootstrapAddress;

        await Assert.That(dbConnectionString).IsNotNull();
        await Assert.That(dbConnectionString).Contains("test_db");

        await Assert.That(redisConnectionString).IsNotNull();
        await Assert.That(redisConnectionString).Contains("127.0.0.1");

        await Assert.That(kafkaBootstrapServers).IsNotNull();
        await Assert.That(kafkaBootstrapServers).Contains("127.0.0.1");
    }

    [Test]
    [Property("Category", "Database")]
    [DisplayName("PostgreSQL connection string is well-formed")]
    public async Task PostgreSql_ConnectionString_ShouldIncludeExpectedValues()
    {
        var connectionString = GlobalTestInfrastructureSetup.PostgreSqlContainer!.ConnectionString;

        await Assert.That(connectionString).Contains("test_db");
        await Assert.That(connectionString).Contains("test_user");
        await Assert.That(connectionString).Contains("test_password");
    }
}

public class AdvancedDependencyTests
{
    [Test]
    [Property("Category", "Network")]
    [DisplayName("Test network is established")]
    public async Task NetworkInfrastructure_ShouldExposeNetworkName()
    {
        var networkName = GlobalTestInfrastructureSetup.NetworkName;
        await Assert.That(networkName).IsEqualTo("global-test-network");
    }

    [Test]
    [Property("Category", "Infrastructure")]
    [DisplayName("All containers reached Running state")]
    public async Task AllContainers_ShouldBeRunning()
    {
        await Assert.That(GlobalTestInfrastructureSetup.PostgreSqlContainer!.State).IsEqualTo("Running");
        await Assert.That(GlobalTestInfrastructureSetup.RedisContainer!.State).IsEqualTo("Running");
        await Assert.That(GlobalTestInfrastructureSetup.KafkaContainer!.State).IsEqualTo("Running");
    }
}

/*
 * Real Testcontainers wiring (uncomment after adding packages):
 *
 * dotnet add package Testcontainers.PostgreSql
 * dotnet add package Testcontainers.Redis
 * dotnet add package Testcontainers.Kafka
 *
 * using Testcontainers.PostgreSql;
 * using Testcontainers.Redis;
 * using Testcontainers.Kafka;
 * using DotNet.Testcontainers.Builders;
 * using DotNet.Testcontainers.Networks;
 *
 * public static class RealGlobalTestInfrastructureSetup
 * {
 *     public static PostgreSqlContainer? PostgreSqlContainer { get; private set; }
 *     public static RedisContainer? RedisContainer { get; private set; }
 *     public static KafkaContainer? KafkaContainer { get; private set; }
 *     public static INetwork? Network { get; private set; }
 *
 *     [Before(Assembly)]
 *     public static async Task SetupGlobalInfrastructure()
 *     {
 *         Network = new NetworkBuilder().WithName("global-test-network").Build();
 *         await Network.CreateAsync();
 *
 *         PostgreSqlContainer = new PostgreSqlBuilder()
 *             .WithDatabase("test_db").WithUsername("test_user").WithPassword("test_password")
 *             .WithNetwork(Network).WithCleanUp(true).Build();
 *         await PostgreSqlContainer.StartAsync();
 *
 *         RedisContainer = new RedisBuilder().WithNetwork(Network).WithCleanUp(true).Build();
 *         await RedisContainer.StartAsync();
 *
 *         KafkaContainer = new KafkaBuilder().WithNetwork(Network).WithCleanUp(true).Build();
 *         await KafkaContainer.StartAsync();
 *     }
 *
 *     [After(Assembly)]
 *     public static async Task TeardownGlobalInfrastructure()
 *     {
 *         if (KafkaContainer != null) await KafkaContainer.DisposeAsync();
 *         if (RedisContainer != null) await RedisContainer.DisposeAsync();
 *         if (PostgreSqlContainer != null) await PostgreSqlContainer.DisposeAsync();
 *         if (Network != null) await Network.DeleteAsync();
 *     }
 * }
 */
