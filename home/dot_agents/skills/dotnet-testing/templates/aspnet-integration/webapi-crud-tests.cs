// =============================================================================
// WebApi integration testing — full CRUD test suite with ProblemDetails
// =============================================================================

using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Flurl;
using Microsoft.AspNetCore.Mvc;
using YourProject.Application.DTOs;
using YourProject.Tests.Integration.Fixtures;

namespace YourProject.Tests.Integration.Controllers;

/// <summary>
/// ProductsController integration tests.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ProductsControllerTests : IntegrationTestBase
{
    public ProductsControllerTests(TestWebApplicationFactory factory) : base(factory) { }

    #region Create product

    [Fact]
    public async Task CreateProduct_WithValidData_CreatesProduct()
    {
        var request = new ProductCreateRequest
        {
            Name = "New product",
            Price = 299.99m
        };

        var response = await HttpClient.PostAsJsonAsync("/products", request);

        response.Should().Be201Created()
            .And.Satisfy<ProductResponse>(product =>
            {
                product.Id.Should().NotBeEmpty();
                product.Name.Should().Be("New product");
                product.Price.Should().Be(299.99m);
                product.CreatedAt.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
                product.UpdatedAt.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
            });
    }

    [Fact]
    public async Task CreateProduct_WhenNameEmpty_Returns400()
    {
        var invalidRequest = new ProductCreateRequest
        {
            Name = "",
            Price = 100.00m
        };

        var response = await HttpClient.PostAsJsonAsync("/products", invalidRequest);

        response.Should().Be400BadRequest()
            .And.Satisfy<ValidationProblemDetails>(problem =>
            {
                problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
                problem.Title.Should().Be("One or more validation errors occurred.");
                problem.Status.Should().Be(400);
                problem.Errors.Should().ContainKey("Name");
                problem.Errors["Name"].Should().Contain("Product name must not be empty");
            });
    }

    [Fact]
    public async Task CreateProduct_WhenPriceNegative_Returns400()
    {
        var invalidRequest = new ProductCreateRequest
        {
            Name = "Test product",
            Price = -10.00m
        };

        var response = await HttpClient.PostAsJsonAsync("/products", invalidRequest);

        response.Should().Be400BadRequest()
            .And.Satisfy<ValidationProblemDetails>(problem =>
            {
                problem.Errors.Should().ContainKey("Price");
                problem.Errors["Price"].Should().Contain("Product price must be greater than 0");
            });
    }

    [Fact]
    public async Task CreateProduct_WhenMultipleFieldsInvalid_ReturnsAllErrors()
    {
        var invalidRequest = new ProductCreateRequest
        {
            Name = "",
            Price = -10.00m
        };

        var response = await HttpClient.PostAsJsonAsync("/products", invalidRequest);

        response.Should().Be400BadRequest()
            .And.Satisfy<ValidationProblemDetails>(problem =>
            {
                problem.Errors.Should().ContainKey("Name");
                problem.Errors.Should().ContainKey("Price");
            });
    }

    #endregion

    #region Query product

    [Fact]
    public async Task GetById_WhenProductExists_ReturnsProduct()
    {
        var productId = await DatabaseManager.SeedProductAsync("Test product", 199.99m);

        var response = await HttpClient.GetAsync($"/products/{productId}");

        response.Should().Be200Ok()
            .And.Satisfy<ProductResponse>(product =>
            {
                product.Id.Should().Be(productId);
                product.Name.Should().Be("Test product");
                product.Price.Should().Be(199.99m);
            });
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404WithProblemDetails()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await HttpClient.GetAsync($"/products/{nonExistentId}");

        response.Should().Be404NotFound();

        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(content);

        problemDetails.GetProperty("type").GetString().Should().Be("https://httpstatuses.com/404");
        problemDetails.GetProperty("title").GetString().Should().Be("Product not found");
        problemDetails.GetProperty("status").GetInt32().Should().Be(404);
        problemDetails.GetProperty("detail").GetString().Should().Contain($"Product with id {nonExistentId} was not found");
    }

    #endregion

    #region Paginated query

    [Fact]
    public async Task GetProducts_WithPagination_ReturnsPagedResult()
    {
        await DatabaseManager.SeedProductsAsync(15);

        // Flurl builds the query string.
        var url = "/products"
            .SetQueryParam("pageSize", 5)
            .SetQueryParam("page", 2);

        var response = await HttpClient.GetAsync(url);

        response.Should().Be200Ok()
            .And.Satisfy<PagedResult<ProductResponse>>(result =>
            {
                result.Total.Should().Be(15);
                result.PageSize.Should().Be(5);
                result.Page.Should().Be(2);
                result.PageCount.Should().Be(3);
                result.Items.Should().HaveCount(5);
                result.Items.Should().AllSatisfy(product =>
                {
                    product.Id.Should().NotBeEmpty();
                    product.Name.Should().NotBeNullOrEmpty();
                    product.Price.Should().BeGreaterThan(0);
                });
            });
    }

    [Fact]
    public async Task GetProducts_WithKeyword_ReturnsFilteredResults()
    {
        await DatabaseManager.SeedProductsAsync(5);
        await DatabaseManager.SeedProductAsync("Special product", 199.99m);

        var url = "/products"
            .SetQueryParam("keyword", "Special")
            .SetQueryParam("pageSize", 10);

        var response = await HttpClient.GetAsync(url);

        response.Should().Be200Ok()
            .And.Satisfy<PagedResult<ProductResponse>>(result =>
            {
                result.Total.Should().Be(1);
                result.Items.Should().HaveCount(1);

                var product = result.Items.First();
                product.Name.Should().Be("Special product");
                product.Price.Should().Be(199.99m);
            });
    }

    #endregion

    #region Update product

    [Fact]
    public async Task UpdateProduct_WithValidData_UpdatesProduct()
    {
        var productId = await DatabaseManager.SeedProductAsync("Original product", 100.00m);
        var updateRequest = new ProductUpdateRequest
        {
            Name = "Updated product",
            Price = 299.99m
        };

        AdvanceTime(TimeSpan.FromHours(1));

        var response = await HttpClient.PutAsJsonAsync($"/products/{productId}", updateRequest);

        response.Should().Be200Ok()
            .And.Satisfy<ProductResponse>(product =>
            {
                product.Name.Should().Be("Updated product");
                product.Price.Should().Be(299.99m);
            });
    }

    #endregion

    #region Delete product

    [Fact]
    public async Task DeleteProduct_WhenExists_DeletesProduct()
    {
        var productId = await DatabaseManager.SeedProductAsync("Product to delete", 100.00m);

        var response = await HttpClient.DeleteAsync($"/products/{productId}");

        response.Should().Be204NoContent();

        var getResponse = await HttpClient.GetAsync($"/products/{productId}");
        getResponse.Should().Be404NotFound();
    }

    [Fact]
    public async Task DeleteProduct_WhenMissing_Returns404()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await HttpClient.DeleteAsync($"/products/{nonExistentId}");

        response.Should().Be404NotFound();
    }

    #endregion
}

#region DTOs (example)

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

#endregion
