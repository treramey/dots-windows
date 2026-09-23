using Npgsql;

namespace MyApp.Tests.Integration.Infrastructure;

/// <summary>
/// Test helper methods — seed + probe utilities.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Seed a batch of test products.
    /// </summary>
    public static async Task SeedProductsAsync(DatabaseManager databaseManager, int count)
    {
        var connectionString = await databaseManager.GetConnectionStringAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        for (int i = 1; i <= count; i++)
        {
            var sql = @"
                INSERT INTO products (id, name, price, created_at, updated_at)
                VALUES (@id, @name, @price, @createdAt, @updatedAt)";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("name", $"Test product {i}");
            command.Parameters.AddWithValue("price", 100.00m + i);
            command.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
            command.Parameters.AddWithValue("updatedAt", DateTimeOffset.UtcNow);

            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Seed a specific test product.
    /// </summary>
    public static async Task<Guid> SeedSpecificProductAsync(
        DatabaseManager databaseManager,
        string name,
        decimal price)
    {
        var connectionString = await databaseManager.GetConnectionStringAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var productId = Guid.NewGuid();
        var sql = @"
            INSERT INTO products (id, name, price, created_at, updated_at)
            VALUES (@id, @name, @price, @createdAt, @updatedAt)";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", productId);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("price", price);
        command.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("updatedAt", DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync();
        return productId;
    }

    /// <summary>
    /// Delete all product rows.
    /// </summary>
    public static async Task CleanAllProductsAsync(DatabaseManager databaseManager)
    {
        var connectionString = await databaseManager.GetConnectionStringAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("DELETE FROM products", connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Return the product count.
    /// </summary>
    public static async Task<int> GetProductCountAsync(DatabaseManager databaseManager)
    {
        var connectionString = await databaseManager.GetConnectionStringAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM products", connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}
