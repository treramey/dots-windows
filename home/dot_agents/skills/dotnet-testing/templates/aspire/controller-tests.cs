using AwesomeAssertions;
using Flurl;

namespace MyApp.Tests.Integration.Controllers;

/// <summary>
/// ProductsController integration tests — uses Aspire Testing.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ProductsControllerTests : IntegrationTestBase
{
    public ProductsControllerTests(AspireAppFixture fixture) : base(fixture)
    {
    }

    #region GET /products

    [Fact]
    public async Task GetProducts_WhenNoneExist_ShouldReturnEmptyPagedResult()
    {
        // Arrange — DB is reset before each test.

        // Act
        var response = await HttpClient.GetAsync("/products");

        // Assert
        response.Should().Be200Ok()
                .And.Satisfy<PagedResult<ProductResponse>>(result =>
                {
                    result.Total.Should().Be(0);
                    result.PageSize.Should().Be(10);
                    result.Page.Should().Be(1);
                    result.Items.Should().BeEmpty();
                });
    }

    [Fact]
    public async Task GetProducts_WithPagingParameters_ShouldReturnCorrectPage()
    {
        // Arrange
        await TestHelpers.SeedProductsAsync(DatabaseManager, 15);

        // Act — build the query string with Flurl
        var url = "/products"
                  .SetQueryParam("pageSize", 5)
                  .SetQueryParam("page", 2);

        var response = await HttpClient.GetAsync(url);

        // Assert
        response.Should().Be200Ok()
                .And.Satisfy<PagedResult<ProductResponse>>(result =>
                {
                    result.Total.Should().Be(15);
                    result.PageSize.Should().Be(5);
                    result.Page.Should().Be(2);
                    result.PageCount.Should().Be(3);
                    result.Items.Should().HaveCount(5);
                });
    }

    [Fact]
    public async Task GetProducts_WithSearchParameter_ShouldReturnMatchingProducts()
    {
        // Arrange
        await TestHelpers.SeedProductsAsync(DatabaseManager, 5);
        await TestHelpers.SeedSpecificProductAsync(DatabaseManager, "Special product", 199.99m);

        // Act
        var url = "/products"
                  .SetQueryParam("keyword", "Special");

        var response = await HttpClient.GetAsync(url);

        // Assert
        response.Should().Be200Ok()
                .And.Satisfy<PagedResult<ProductResponse>>(result =>
                {
                    result.Total.Should().Be(1);
                    result.Items.Should().HaveCount(1);
                    result.Items.First().Name.Should().Be("Special product");
                    result.Items.First().Price.Should().Be(199.99m);
                });
    }

    #endregion

    #region POST /products

    [Fact]
    public async Task CreateProduct_WhenRequestIsValid_ShouldReturn201WithProduct()
    {
        // Arrange
        var request = new ProductCreateRequest
        {
            Name = "New product",
            Price = 299.99m
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/products", request);

        // Assert
        response.Should().Be201Created()
                .And.Satisfy<ProductResponse>(product =>
                {
                    product.Id.Should().NotBeEmpty();
                    product.Name.Should().Be("New product");
                    product.Price.Should().Be(299.99m);
                    product.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
                });
    }

    [Fact]
    public async Task CreateProduct_WhenRequestIsInvalid_ShouldReturn400WithValidationErrors()
    {
        // Arrange
        var request = new ProductCreateRequest
        {
            Name = "", // Name is required
            Price = -100m // Price must be non-negative
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/products", request);

        // Assert
        response.Should().Be400BadRequest()
                .And.Satisfy<ValidationProblemDetails>(problem =>
                {
                    problem.Errors.Should().ContainKey("Name");
                    problem.Errors.Should().ContainKey("Price");
                });
    }

    #endregion

    #region GET /products/{id}

    [Fact]
    public async Task GetProductById_WhenProductExists_ShouldReturn200WithProduct()
    {
        // Arrange
        var productId = await TestHelpers.SeedSpecificProductAsync(
            DatabaseManager, "Test product", 199.99m);

        // Act
        var response = await HttpClient.GetAsync($"/products/{productId}");

        // Assert
        response.Should().Be200Ok()
                .And.Satisfy<ProductResponse>(product =>
                {
                    product.Id.Should().Be(productId);
                    product.Name.Should().Be("Test product");
                    product.Price.Should().Be(199.99m);
                });
    }

    [Fact]
    public async Task GetProductById_WhenProductDoesNotExist_ShouldReturn404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await HttpClient.GetAsync($"/products/{nonExistentId}");

        // Assert
        response.Should().Be404NotFound();
    }

    #endregion

    #region PUT /products/{id}

    [Fact]
    public async Task UpdateProduct_WhenProductExists_ShouldReturn200WithUpdatedProduct()
    {
        // Arrange
        var productId = await TestHelpers.SeedSpecificProductAsync(
            DatabaseManager, "Original product", 100m);

        var updateRequest = new ProductUpdateRequest
        {
            Name = "Updated product",
            Price = 150m
        };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/products/{productId}", updateRequest);

        // Assert
        response.Should().Be200Ok()
                .And.Satisfy<ProductResponse>(product =>
                {
                    product.Id.Should().Be(productId);
                    product.Name.Should().Be("Updated product");
                    product.Price.Should().Be(150m);
                    product.UpdatedAt.Should().BeAfter(product.CreatedAt);
                });
    }

    #endregion

    #region DELETE /products/{id}

    [Fact]
    public async Task DeleteProduct_WhenProductExists_ShouldReturn204AndRemoveProduct()
    {
        // Arrange
        var productId = await TestHelpers.SeedSpecificProductAsync(
            DatabaseManager, "Pending delete", 99.99m);

        // Act
        var response = await HttpClient.DeleteAsync($"/products/{productId}");

        // Assert
        response.Should().Be204NoContent();

        // Verify the product was deleted
        var getResponse = await HttpClient.GetAsync($"/products/{productId}");
        getResponse.Should().Be404NotFound();
    }

    #endregion
}

#region Test DTOs

public class ProductCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class PagedResult<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int PageCount { get; set; }
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
}

public class ValidationProblemDetails
{
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public Dictionary<string, string[]> Errors { get; set; } = new();
}

#endregion
