// MySQL container template — single-class IAsyncLifetime fixture.
// Mirrors postgres.cs / mssql.cs but uses the MySql module. Upstream
// kevintsengtw's bundle focuses on MSSQL + PostgreSQL; this is an
// extrapolated template using the same patterns for the MySQL module.

using Testcontainers.MySql;
using MySqlConnector;

namespace YourNamespace.Tests.Fixtures;

/// <summary>
/// MySQL container fixture — manages container lifecycle via
/// <see cref="IAsyncLifetime"/>.
/// </summary>
public class MySqlContainerTests : IAsyncLifetime
{
    private readonly MySqlContainer _container;
    private MySqlConnection _connection = null!;

    public MySqlContainerTests()
    {
        _container = new MySqlBuilder()
            // Pin a specific MySQL major version that matches production.
            .WithImage("mysql:8.0")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _connection = new MySqlConnection(_container.GetConnectionString());
        await _connection.OpenAsync();

        // Create schema. In a real project, run migrations or scripts here.
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Products (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Name VARCHAR(255) NOT NULL,
                Price DECIMAL(18,2) NOT NULL
            );";
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    // ===== Example tests =====

    [Fact]
    public async Task InsertProduct_WithValidData_ShouldPersistToDatabase()
    {
        // Arrange
        using var insert = _connection.CreateCommand();
        insert.CommandText = "INSERT INTO Products (Name, Price) VALUES (@name, @price);";
        insert.Parameters.AddWithValue("@name", "Widget");
        insert.Parameters.AddWithValue("@price", 9.99m);

        // Act
        var rowsAffected = await insert.ExecuteNonQueryAsync();

        // Assert
        rowsAffected.Should().Be(1);

        using var select = _connection.CreateCommand();
        select.CommandText = "SELECT COUNT(*) FROM Products WHERE Name = @name;";
        select.Parameters.AddWithValue("@name", "Widget");
        var count = Convert.ToInt32(await select.ExecuteScalarAsync());
        count.Should().Be(1);
    }

    [Fact]
    public void GetConnectionString_AfterContainerStarted_ShouldReturnValidConnectionString()
    {
        var connectionString = _container.GetConnectionString();
        connectionString.Should().NotBeNullOrEmpty();
        connectionString.Should().Contain("Database=testdb");
    }
}

// ===== Variant: Collection Fixture for cross-class sharing =====

/// <summary>
/// MySQL Collection Fixture — share a single container across test
/// classes. Test classes must clean their own rows in Dispose.
/// </summary>
public class MySqlContainerFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container;

    public MySqlContainerFixture()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithCleanUp(true)
            .Build();
    }

    public static string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(nameof(MySqlCollectionFixture))]
public class MySqlCollectionFixture : ICollectionFixture<MySqlContainerFixture>
{
    // Marker only.
}
