// SQL Server (MSSQL) container template — Collection Fixture pattern.
// One container shared across many test classes; classes derive from the
// base, override seeding, and clean their own rows in Dispose.

using Testcontainers.MsSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace YourNamespace.Tests.Fixtures;

// ===== Step 1: container fixture =====

/// <summary>
/// SQL Server container Collection Fixture — shares a single container
/// instance across multiple test classes.
/// </summary>
/// <remarks>
/// Performance impact:
/// - Per-class container: every test class starts its own (~10s each).
/// - Collection Fixture: all classes share one container.
/// - Typical wall-clock improvement: ~67% on a small suite.
/// </remarks>
public class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public SqlServerContainerFixture()
    {
        _container = new MsSqlBuilder()
            // SQL Server 2022 image.
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            // SQL Server password policy requires upper/lower/digit/symbol.
            .WithPassword("Test123456!")
            // Auto-clean container after tests complete.
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Static connection string so test classes can read it without a
    /// fixture-injection ceremony.
    /// </summary>
    public static string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // SQL Server needs a short settle delay after the port is open.
        await Task.Delay(2000);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

// ===== Step 2: collection definition =====

/// <summary>
/// Marks a collection so multiple test classes share the same
/// <see cref="SqlServerContainerFixture"/>. Apply
/// <c>[Collection(nameof(SqlServerCollectionFixture))]</c> on each class.
/// </summary>
[CollectionDefinition(nameof(SqlServerCollectionFixture))]
public class SqlServerCollectionFixture : ICollectionFixture<SqlServerContainerFixture>
{
    // Marker only — xUnit manages the fixture lifecycle.
}

// ===== Step 3: base test class (recommended) =====

/// <summary>
/// Base class for EF Core integration tests. Owns the DbContext, runs
/// schema scripts on construction, and truncates tables on Dispose so
/// each test sees a clean slate.
/// </summary>
public abstract class EfCoreTestBase : IDisposable
{
    protected readonly ECommerceDbContext DbContext;
    protected readonly ITestOutputHelper TestOutputHelper;

    protected EfCoreTestBase(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;
        var connectionString = SqlServerContainerFixture.ConnectionString;

        var options = new DbContextOptionsBuilder<ECommerceDbContext>()
            .UseSqlServer(connectionString)
            // Sensitive data logging is acceptable in test environments only.
            .EnableSensitiveDataLogging()
            // Stream EF Core SQL into the xUnit output.
            .LogTo(testOutputHelper.WriteLine, LogLevel.Information)
            .Options;

        DbContext = new ECommerceDbContext(options);
        DbContext.Database.EnsureCreated();

        EnsureTablesExist();
    }

    /// <summary>
    /// Loads externalized SQL scripts from the test output directory.
    /// Scripts are copied via <c>Content Include="SqlScripts\**\*.sql"</c>.
    /// </summary>
    protected virtual void EnsureTablesExist()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory)) return;

        // Run scripts in dependency order.
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
                DbContext.Database.ExecuteSqlRaw(script);
            }
        }
    }

    /// <summary>
    /// Deletes rows in foreign-key order. Truncate would be faster but
    /// fails when FK constraints are present.
    /// </summary>
    public virtual void Dispose()
    {
        DbContext.Database.ExecuteSqlRaw("DELETE FROM ProductTags");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM OrderItems");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM Orders");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM Products");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM Categories");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM Tags");
        DbContext.Dispose();
    }
}

// ===== Step 4: example test classes =====

/// <summary>EF Core CRUD tests.</summary>
[Collection(nameof(SqlServerCollectionFixture))]
public class EfCoreCrudTests : EfCoreTestBase
{
    private readonly IProductRepository _productRepository;

    public EfCoreCrudTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _productRepository = new EfCoreProductRepository(DbContext);
    }

    [Fact]
    public async Task AddAsync_WithValidProduct_ShouldPersistToDatabase()
    {
        // Arrange
        await SeedCategoryAsync();
        var category = await DbContext.Categories.FirstAsync();
        var product = new Product
        {
            Name = "Test product",
            Price = 1500,
            Stock = 25,
            CategoryId = category.Id,
            SKU = "TEST-001",
            IsActive = true
        };

        // Act
        await _productRepository.AddAsync(product);

        // Assert
        product.Id.Should().BeGreaterThan(0);
        var saved = await DbContext.Products.FindAsync(product.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test product");
    }

    private async Task SeedCategoryAsync()
    {
        if (!await DbContext.Categories.AnyAsync())
        {
            DbContext.Categories.Add(new Category
            {
                Name = "Electronics",
                Description = "Electronic devices",
                IsActive = true
            });
            await DbContext.SaveChangesAsync();
        }
    }
}

/// <summary>EF Core advanced loading patterns (Include / NoTracking).</summary>
[Collection(nameof(SqlServerCollectionFixture))]
public class EfCoreAdvancedTests : EfCoreTestBase
{
    private readonly IProductByEFCoreRepository _advancedRepository;

    public EfCoreAdvancedTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _advancedRepository = new EfCoreProductRepository(DbContext);
    }

    [Fact]
    public async Task GetProductWithCategoryAndTagsAsync_ShouldLoadRelatedData()
    {
        await CreateProductWithCategoryAndTagsAsync();

        var product = await _advancedRepository.GetProductWithCategoryAndTagsAsync(1);

        product.Should().NotBeNull();
        product!.Category.Should().NotBeNull();
        product.ProductTags.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetProductsWithNoTrackingAsync_ShouldNotTrackEntities()
    {
        await CreateMultipleProductsAsync();

        var products = await _advancedRepository.GetProductsWithNoTrackingAsync(500m);

        products.Should().NotBeEmpty();
        var trackedEntities = DbContext.ChangeTracker.Entries<Product>().Count();
        trackedEntities.Should().Be(0, "AsNoTracking queries must not track entities");
    }

    private Task CreateProductWithCategoryAndTagsAsync() => Task.CompletedTask;
    private Task CreateMultipleProductsAsync() => Task.CompletedTask;
}

// ===== Replace with your real domain types =====

public class ECommerceDbContext : DbContext
{
    public ECommerceDbContext(DbContextOptions<ECommerceDbContext> options) : base(options) { }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ProductTag> ProductTags { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public virtual Category Category { get; set; } = null!;
    public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}

public class Tag { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
public class ProductTag { public int Id { get; set; } public int ProductId { get; set; } public int TagId { get; set; } }
public class Order { public int Id { get; set; } }
public class OrderItem { public int Id { get; set; } }

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
}

public interface IProductByEFCoreRepository
{
    Task<Product?> GetProductWithCategoryAndTagsAsync(int productId);
    Task<IEnumerable<Product>> GetProductsWithNoTrackingAsync(decimal minPrice);
}

public class EfCoreProductRepository : IProductRepository, IProductByEFCoreRepository
{
    private readonly ECommerceDbContext _context;
    public EfCoreProductRepository(ECommerceDbContext context) => _context = context;

    public async Task<IEnumerable<Product>> GetAllAsync() => await _context.Products.ToListAsync();
    public async Task<Product?> GetByIdAsync(int id) => await _context.Products.FindAsync(id);
    public async Task AddAsync(Product product) { _context.Products.Add(product); await _context.SaveChangesAsync(); }
    public async Task UpdateAsync(Product product) { _context.Products.Update(product); await _context.SaveChangesAsync(); }
    public async Task DeleteAsync(int id) { var entity = await GetByIdAsync(id); if (entity != null) { _context.Products.Remove(entity); await _context.SaveChangesAsync(); } }

    public async Task<Product?> GetProductWithCategoryAndTagsAsync(int productId) =>
        await _context.Products.Include(p => p.Category).Include(p => p.ProductTags).FirstOrDefaultAsync(p => p.Id == productId);

    public async Task<IEnumerable<Product>> GetProductsWithNoTrackingAsync(decimal minPrice) =>
        await _context.Products.AsNoTracking().Where(p => p.Price >= minPrice).ToListAsync();
}
