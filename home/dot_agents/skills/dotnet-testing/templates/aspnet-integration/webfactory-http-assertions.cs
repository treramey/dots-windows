// =============================================================================
// ASP.NET Core integration testing — HTTP assertion examples
// AwesomeAssertions.Web fluent HTTP assertions.
// =============================================================================

using System.Net.Http.Json;
using AwesomeAssertions;
using AwesomeAssertions.Web;
using Microsoft.AspNetCore.Mvc;

namespace YourProject.IntegrationTests.Examples;

/// <summary>
/// HTTP response assertions using AwesomeAssertions.Web.
/// </summary>
public class HttpAssertionExamples : IntegrationTestBase
{
    // ========================================
    // Status-code assertions
    // ========================================

    [Fact]
    public async Task StatusCode_200_Ok()
    {
        var response = await Client.GetAsync("/api/shippers");
        response.Should().Be200Ok();
    }

    [Fact]
    public async Task StatusCode_201_Created()
    {
        var request = new ShipperCreateParameter
        {
            CompanyName = "New company",
            Phone = "02-1234-5678"
        };

        var response = await Client.PostAsJsonAsync("/api/shippers", request);
        response.Should().Be201Created();
    }

    [Fact]
    public async Task StatusCode_204_NoContent()
    {
        var shipperId = await SeedShipperAsync("To-delete company");

        var response = await Client.DeleteAsync($"/api/shippers/{shipperId}");
        response.Should().Be204NoContent();
    }

    [Fact]
    public async Task StatusCode_400_BadRequest()
    {
        var invalidRequest = new ShipperCreateParameter
        {
            CompanyName = "",
            Phone = ""
        };

        var response = await Client.PostAsJsonAsync("/api/shippers", invalidRequest);
        response.Should().Be400BadRequest();
    }

    [Fact]
    public async Task StatusCode_404_NotFound()
    {
        var response = await Client.GetAsync("/api/shippers/99999");
        response.Should().Be404NotFound();
    }

    // ========================================
    // Satisfy<T> typed body verification
    // ========================================

    [Fact]
    public async Task Satisfy_VerifiesSuccessBody()
    {
        await CleanupDatabaseAsync();
        var shipperId = await SeedShipperAsync("Test company", "02-9876-5432");

        var response = await Client.GetAsync($"/api/shippers/{shipperId}");

        response.Should().Be200Ok()
            .And
            .Satisfy<SuccessResultOutputModel<ShipperOutputModel>>(result =>
            {
                result.Status.Should().Be("Success");
                result.Data.Should().NotBeNull();

                result.Data!.ShipperId.Should().Be(shipperId);
                result.Data.CompanyName.Should().Be("Test company");
                result.Data.Phone.Should().Be("02-9876-5432");
            });
    }

    [Fact]
    public async Task Satisfy_VerifiesCollectionResponse()
    {
        await CleanupDatabaseAsync();
        await SeedShipperAsync("Company A", "02-1111-1111");
        await SeedShipperAsync("Company B", "02-2222-2222");

        var response = await Client.GetAsync("/api/shippers");

        response.Should().Be200Ok()
            .And
            .Satisfy<SuccessResultOutputModel<List<ShipperOutputModel>>>(result =>
            {
                result.Data!.Count.Should().Be(2);
                result.Data.Should().Contain(s => s.CompanyName == "Company A");
                result.Data.Should().Contain(s => s.CompanyName == "Company B");
                result.Data.Should().BeInAscendingOrder(s => s.CompanyName);
            });
    }

    [Fact]
    public async Task Satisfy_VerifiesErrorDetails()
    {
        var invalidRequest = new ShipperCreateParameter
        {
            CompanyName = "",
            Phone = "02-1234-5678"
        };

        var response = await Client.PostAsJsonAsync("/api/shippers", invalidRequest);

        response.Should().Be400BadRequest()
            .And
            .Satisfy<ValidationProblemDetails>(problem =>
            {
                problem.Status.Should().Be(400);
                problem.Title.Should().Contain("validation");
                problem.Errors.Should().ContainKey("CompanyName");
            });
    }

    // ========================================
    // BeAs — anonymous-type verification
    // ========================================

    [Fact]
    public async Task BeAs_QuickAnonymousTypeVerification()
    {
        await CleanupDatabaseAsync();
        var shipperId = await SeedShipperAsync("Anon company", "02-5555-5555");

        var response = await Client.GetAsync($"/api/shippers/{shipperId}");

        response.Should().Be200Ok()
            .And
            .BeAs(new
            {
                Status = "Success",
                Data = new
                {
                    CompanyName = "Anon company",
                    Phone = "02-5555-5555"
                }
            });
    }

    // ========================================
    // Composed CRUD assertions
    // ========================================

    [Fact]
    public async Task ComposedAssertions_FullCrudFlow()
    {
        await CleanupDatabaseAsync();

        // Create
        var createRequest = new ShipperCreateParameter
        {
            CompanyName = "CRUD test company",
            Phone = "02-1234-5678"
        };

        var createResponse = await Client.PostAsJsonAsync("/api/shippers", createRequest);
        createResponse.Should().Be201Created();

        var created = await createResponse.Content
            .ReadFromJsonAsync<SuccessResultOutputModel<ShipperOutputModel>>();
        var shipperId = created!.Data!.ShipperId;

        // Read
        var readResponse = await Client.GetAsync($"/api/shippers/{shipperId}");
        readResponse.Should().Be200Ok()
            .And
            .Satisfy<SuccessResultOutputModel<ShipperOutputModel>>(result =>
            {
                result.Data!.CompanyName.Should().Be("CRUD test company");
            });

        // Update
        var updateRequest = new ShipperCreateParameter
        {
            CompanyName = "Updated company name",
            Phone = "02-8765-4321"
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/shippers/{shipperId}", updateRequest);
        updateResponse.Should().Be200Ok()
            .And
            .Satisfy<SuccessResultOutputModel<ShipperOutputModel>>(result =>
            {
                result.Data!.CompanyName.Should().Be("Updated company name");
                result.Data.Phone.Should().Be("02-8765-4321");
            });

        // Delete
        var deleteResponse = await Client.DeleteAsync($"/api/shippers/{shipperId}");
        deleteResponse.Should().Be204NoContent();

        // Verify deletion
        var verifyResponse = await Client.GetAsync($"/api/shippers/{shipperId}");
        verifyResponse.Should().Be404NotFound();
    }
}

// =============================================================================
// DTO models (adapt to your project)
// =============================================================================

public class SuccessResultOutputModel<T>
{
    public string Status { get; set; } = "Success";
    public T? Data { get; set; }
}

public class ShipperOutputModel
{
    public int ShipperId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class ShipperCreateParameter
{
    public string CompanyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
