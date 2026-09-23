// =============================================================================
// ASP.NET Core integration testing — test base class
// Common setup, database helpers, HttpClient management.
// =============================================================================

using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace YourProject.IntegrationTests;

/// <summary>
/// Base class for integration tests.
/// Provides common setup, database helpers, and HttpClient management.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly CustomWebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase()
    {
        Factory = new CustomWebApplicationFactory<Program>();
        Client = Factory.CreateClient();
    }

    // ========================================
    // Database helpers
    // ========================================

    /// <summary>Adds a test shipper.</summary>
    protected async Task<int> SeedShipperAsync(string companyName, string phone = "02-12345678")
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var shipper = new Shipper
        {
            CompanyName = companyName,
            Phone = phone,
            CreatedAt = DateTime.UtcNow
        };

        context.Shippers.Add(shipper);
        await context.SaveChangesAsync();

        return shipper.ShipperId;
    }

    /// <summary>Bulk-adds shippers.</summary>
    protected async Task<List<int>> SeedShippersAsync(
        params (string CompanyName, string Phone)[] shippers)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entities = shippers.Select(s => new Shipper
        {
            CompanyName = s.CompanyName,
            Phone = s.Phone,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        context.Shippers.AddRange(entities);
        await context.SaveChangesAsync();

        return entities.Select(e => e.ShipperId).ToList();
    }

    /// <summary>Clears the database.</summary>
    protected async Task CleanupDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Shippers.RemoveRange(context.Shippers);
        await context.SaveChangesAsync();
    }

    /// <summary>Returns the current shipper count.</summary>
    protected async Task<int> GetShipperCountAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Shippers.CountAsync();
    }

    // ========================================
    // HTTP helpers
    // ========================================

    protected async Task<T?> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest request)
    {
        var response = await Client.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    protected async Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest request)
    {
        var response = await Client.PutAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    protected async Task DeleteAsync(string url)
    {
        var response = await Client.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    // ========================================
    // IDisposable
    // ========================================

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client?.Dispose();
            Factory?.Dispose();
        }
    }
}
