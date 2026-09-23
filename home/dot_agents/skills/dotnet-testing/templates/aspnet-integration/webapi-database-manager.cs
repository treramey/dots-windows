// =============================================================================
// WebApi integration testing — DatabaseManager with Respawn
// PostgreSQL schema bootstrap and per-test row reset.
// =============================================================================

using Npgsql;
using Respawn;

namespace YourProject.Tests.Integration.Fixtures;

/// <summary>
/// Database manager — initialises schema and clears rows between tests.
/// </summary>
public class DatabaseManager
{
    private readonly string _connectionString;
    private Respawner? _respawner;
    private bool _isInitialized;

    public DatabaseManager(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>Initialise schema and Respawner.</summary>
    public async Task InitializeDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await EnsureTablesExistAsync(connection);

        if (_respawner == null)
        {
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = new[] { "public" },
                TablesToIgnore = new Respawn.Graph.Table[]
                {
                    // Ignore tables that should not be reset, e.g.:
                    // "schema_migrations"
                }
            });
        }

        _isInitialized = true;
    }

    /// <summary>Clear table rows (keeps the schema).</summary>
    public async Task CleanDatabaseAsync()
    {
        if (_respawner == null)
        {
            throw new InvalidOperationException("Respawner not initialized — call InitializeDatabaseAsync first.");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    /// <summary>Bootstrap tables from SQL scripts under ./SqlScripts.</summary>
    private async Task EnsureTablesExistAsync(NpgsqlConnection connection)
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory))
        {
            throw new DirectoryNotFoundException($"SQL scripts directory not found: {scriptDirectory}");
        }

        // Run scripts in dependency order.
        var orderedScripts = new[]
        {
            "Tables/CreateProductsTable.sql"
            // Add more as needed
        };

        foreach (var scriptPath in orderedScripts)
        {
            var fullPath = Path.Combine(scriptDirectory, scriptPath);
            if (File.Exists(fullPath))
            {
                var script = await File.ReadAllTextAsync(fullPath);
                await using var command = new NpgsqlCommand(script, connection);
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                throw new FileNotFoundException($"SQL script not found: {fullPath}");
            }
        }
    }

    /// <summary>Execute arbitrary SQL.</summary>
    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Single-row query.</summary>
    public async Task<T?> QuerySingleAsync<T>(string sql, Func<NpgsqlDataReader, T> mapper)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return mapper(reader);
        }

        return default;
    }

    /// <summary>Seed a single product.</summary>
    public async Task<Guid> SeedProductAsync(string name, decimal price)
    {
        var id = Guid.NewGuid();
        var sql = $@"
            INSERT INTO products (id, name, price, created_at, updated_at)
            VALUES ('{id}', '{name}', {price}, NOW(), NOW())";

        await ExecuteAsync(sql);
        return id;
    }

    /// <summary>Seed N products.</summary>
    public async Task SeedProductsAsync(int count)
    {
        var tasks = Enumerable.Range(1, count)
            .Select(i => SeedProductAsync($"Product {i:D2}", i * 10.0m));
        await Task.WhenAll(tasks);
    }
}
