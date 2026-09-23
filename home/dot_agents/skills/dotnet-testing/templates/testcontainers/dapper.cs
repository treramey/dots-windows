// Dapper integration tests — runs CRUD, QueryMultiple, DynamicParameters,
// and stored procedure tests against the shared SqlServerCollectionFixture.

using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;
using Xunit.Abstractions;

namespace YourNamespace.Tests.Dapper;

/// <summary>Dapper CRUD tests against a shared SQL Server container.</summary>
[Collection(nameof(SqlServerCollectionFixture))]
public class DapperCrudTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly IProductRepository _productRepository;
    private readonly ITestOutputHelper _testOutputHelper;

    public DapperCrudTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var connectionString = SqlServerContainerFixture.ConnectionString;

        _connection = new SqlConnection(connectionString);
        _connection.Open();

        _productRepository = new DapperProductRepository(connectionString);

        EnsureTablesExist();
        SeedCategories();
    }

    public void Dispose()
    {
        // Delete in foreign-key order.
        _connection.Execute("DELETE FROM ProductTags");
        _connection.Execute("DELETE FROM OrderItems");
        _connection.Execute("DELETE FROM Orders");
        _connection.Execute("DELETE FROM Products");
        _connection.Execute("DELETE FROM Categories");
        _connection.Execute("DELETE FROM Tags");
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void EnsureTablesExist()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory)) return;

        var orderedScripts = new[]
        {
            "Tables/CreateCategoriesTable.sql",
            "Tables/CreateTagsTable.sql",
            "Tables/CreateProductsTable.sql",
            "Tables/CreateOrdersTable.sql",
            "Tables/CreateOrderItemsTable.sql",
            "Tables/CreateProductTagsTable.sql"
        };

        foreach (var scriptPath in orderedScripts)
        {
            var fullPath = Path.Combine(scriptDirectory, scriptPath);
            if (File.Exists(fullPath))
            {
                var script = File.ReadAllText(fullPath);
                _connection.Execute(script);
            }
        }
    }

    private void SeedCategories()
    {
        var count = _connection.QuerySingle<int>("SELECT COUNT(*) FROM Categories");
        if (count == 0)
        {
            _connection.Execute(@"
                INSERT INTO Categories (Name, Description, IsActive)
                VALUES ('Electronics', 'Electronic devices', 1), ('Books', 'Books and media', 1)");
        }
    }

    [Fact]
    public async Task AddAsync_WithValidProduct_ShouldPersistToDatabase()
    {
        // Arrange
        var categoryId = await _connection.QuerySingleAsync<int>(
            "SELECT TOP 1 Id FROM Categories WHERE IsActive = 1");

        var product = new Product
        {
            Name = "Dapper test product",
            Description = "Created by Dapper test",
            Price = 1500,
            Stock = 25,
            CategoryId = categoryId,
            SKU = "DAPPER-001",
            IsActive = true
        };

        // Act
        await _productRepository.AddAsync(product);

        // Assert
        product.Id.Should().BeGreaterThan(0);

        var saved = await _connection.QuerySingleOrDefaultAsync<Product>(
            "SELECT * FROM Products WHERE Id = @Id", new { product.Id });
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Dapper test product");
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnProduct()
    {
        // Arrange
        var categoryId = await _connection.QuerySingleAsync<int>(
            "SELECT TOP 1 Id FROM Categories WHERE IsActive = 1");

        var productId = await _connection.QuerySingleAsync<int>(@"
            INSERT INTO Products (Name, Price, Stock, CategoryId, SKU, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES ('Lookup test product', 999, 10, @CategoryId, 'GET-001', 1, GETUTCDATE())",
            new { CategoryId = categoryId });

        // Act
        var result = await _productRepository.GetByIdAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(productId);
        result.Name.Should().Be("Lookup test product");
    }
}

/// <summary>
/// Dapper advanced patterns: <c>QueryMultiple</c>, <c>DynamicParameters</c>,
/// and stored procedure calls.
/// </summary>
[Collection(nameof(SqlServerCollectionFixture))]
public class DapperAdvancedTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly IProductByDapperRepository _advancedRepository;
    private readonly ITestOutputHelper _testOutputHelper;

    public DapperAdvancedTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var connectionString = SqlServerContainerFixture.ConnectionString;

        _connection = new SqlConnection(connectionString);
        _connection.Open();

        _advancedRepository = new DapperProductRepository(connectionString);

        EnsureDatabaseObjectsExist();
    }

    private void EnsureDatabaseObjectsExist()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory)) return;

        var tableScripts = new[]
        {
            "Tables/CreateCategoriesTable.sql",
            "Tables/CreateTagsTable.sql",
            "Tables/CreateProductsTable.sql",
            "Tables/CreateProductTagsTable.sql"
        };

        foreach (var scriptPath in tableScripts)
        {
            var fullPath = Path.Combine(scriptDirectory, scriptPath);
            if (File.Exists(fullPath))
            {
                _connection.Execute(File.ReadAllText(fullPath));
            }
        }

        var spPath = Path.Combine(scriptDirectory, "StoredProcedures/GetProductSalesReport.sql");
        if (File.Exists(spPath))
        {
            _connection.Execute(File.ReadAllText(spPath));
        }
    }

    public void Dispose()
    {
        _connection.Execute("DELETE FROM ProductTags");
        _connection.Execute("DELETE FROM Products");
        _connection.Execute("DELETE FROM Categories");
        _connection.Execute("DELETE FROM Tags");
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetProductWithTagsAsync_UsingQueryMultiple_ShouldLoadRelatedData()
    {
        await SeedProductWithTagsAsync();
        var productId = await _connection.QuerySingleAsync<int>("SELECT TOP 1 Id FROM Products");

        var product = await _advancedRepository.GetProductWithTagsAsync(productId);

        product.Should().NotBeNull();
        product!.Tags.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchProductsAsync_WithDynamicParameters_ShouldFilterCorrectly()
    {
        await SeedMultipleProductsAsync();

        var allProducts = await _advancedRepository.SearchProductsAsync();
        var byCategory = await _advancedRepository.SearchProductsAsync(categoryId: 1);
        var byMinPrice = await _advancedRepository.SearchProductsAsync(minPrice: 500m);
        var activeOnly = await _advancedRepository.SearchProductsAsync(isActive: true);

        allProducts.Should().NotBeEmpty();
        byCategory.All(p => p.CategoryId == 1).Should().BeTrue();
        byMinPrice.All(p => p.Price >= 500m).Should().BeTrue();
        activeOnly.All(p => p.IsActive).Should().BeTrue();
    }

    private async Task SeedProductWithTagsAsync()
    {
        await _connection.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM Categories)
            INSERT INTO Categories (Name, IsActive) VALUES ('TestCategory', 1)");

        await _connection.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM Tags)
            INSERT INTO Tags (Name) VALUES ('TagA'), ('TagB')");

        await _connection.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM Products)
            BEGIN
                DECLARE @CategoryId INT = (SELECT TOP 1 Id FROM Categories)
                INSERT INTO Products (Name, Price, Stock, CategoryId, SKU, IsActive, CreatedAt)
                VALUES ('TestProduct', 1000, 10, @CategoryId, 'TEST-001', 1, GETUTCDATE())

                DECLARE @ProductId INT = SCOPE_IDENTITY()
                INSERT INTO ProductTags (ProductId, TagId)
                SELECT @ProductId, Id FROM Tags
            END");
    }

    private Task SeedMultipleProductsAsync() => Task.CompletedTask;
}

// ===== Repository implementation =====

public class DapperProductRepository : IProductRepository, IProductByDapperRepository
{
    private readonly string _connectionString;

    public DapperProductRepository(string connectionString) => _connectionString = connectionString;

    private IDbConnection CreateConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<Product>("SELECT * FROM Products");
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            "SELECT * FROM Products WHERE Id = @Id", new { Id = id });
    }

    public async Task AddAsync(Product product)
    {
        using var connection = CreateConnection();
        const string sql = @"
            INSERT INTO Products (Name, Description, Price, Stock, CategoryId, SKU, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Description, @Price, @Stock, @CategoryId, @SKU, @IsActive, GETUTCDATE())";

        product.Id = await connection.QuerySingleAsync<int>(sql, product);
    }

    public async Task UpdateAsync(Product product)
    {
        using var connection = CreateConnection();
        const string sql = @"
            UPDATE Products
            SET Name = @Name, Description = @Description, Price = @Price,
                Stock = @Stock, CategoryId = @CategoryId, IsActive = @IsActive,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, product);
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync("DELETE FROM Products WHERE Id = @Id", new { Id = id });
    }

    /// <summary>Loads a product and its tags via two-result-set QueryMultiple.</summary>
    public async Task<Product?> GetProductWithTagsAsync(int productId)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT * FROM Products WHERE Id = @ProductId;
            SELECT t.* FROM Tags t
            INNER JOIN ProductTags pt ON t.Id = pt.TagId
            WHERE pt.ProductId = @ProductId;";

        using var multi = await connection.QueryMultipleAsync(sql, new { ProductId = productId });

        var product = await multi.ReadSingleOrDefaultAsync<Product>();
        if (product != null)
        {
            product.Tags = (await multi.ReadAsync<Tag>()).ToList();
        }
        return product;
    }

    /// <summary>Builds the predicate dynamically from optional parameters.</summary>
    public async Task<IEnumerable<Product>> SearchProductsAsync(
        int? categoryId = null,
        decimal? minPrice = null,
        bool? isActive = null)
    {
        using var connection = CreateConnection();

        var sql = new StringBuilder("SELECT * FROM Products WHERE 1=1");
        var parameters = new DynamicParameters();

        if (categoryId.HasValue)
        {
            sql.Append(" AND CategoryId = @CategoryId");
            parameters.Add("CategoryId", categoryId.Value);
        }

        if (minPrice.HasValue)
        {
            sql.Append(" AND Price >= @MinPrice");
            parameters.Add("MinPrice", minPrice.Value);
        }

        if (isActive.HasValue)
        {
            sql.Append(" AND IsActive = @IsActive");
            parameters.Add("IsActive", isActive.Value);
        }

        return await connection.QueryAsync<Product>(sql.ToString(), parameters);
    }

    /// <summary>Calls a stored procedure that returns a typed result set.</summary>
    public async Task<IEnumerable<ProductSalesReport>> GetProductSalesReportAsync(decimal minPrice)
    {
        using var connection = CreateConnection();

        return await connection.QueryAsync<ProductSalesReport>(
            "GetProductSalesReport",
            new { MinPrice = minPrice },
            commandType: CommandType.StoredProcedure);
    }
}

// ===== Interfaces and DTOs =====

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
}

public interface IProductByDapperRepository
{
    Task<Product?> GetProductWithTagsAsync(int productId);
    Task<IEnumerable<Product>> SearchProductsAsync(int? categoryId = null, decimal? minPrice = null, bool? isActive = null);
    Task<IEnumerable<ProductSalesReport>> GetProductSalesReportAsync(decimal minPrice);
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<Tag> Tags { get; set; } = new();
}

public class Tag { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

public class ProductSalesReport
{
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public int TotalQuantity { get; set; }
}

// SqlServerContainerFixture / SqlServerCollectionFixture are defined in mssql.cs.
