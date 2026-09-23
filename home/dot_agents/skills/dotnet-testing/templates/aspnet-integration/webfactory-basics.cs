// =============================================================================
// ASP.NET Core integration testing — custom WebApplicationFactory
// Configures the test DI container and replaces the database with InMemory.
// =============================================================================

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace YourProject.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// </summary>
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 1. Remove the production database registration.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            // Alternative: services.RemoveAll(typeof(DbContextOptions<AppDbContext>));

            // 2. Register an in-memory database.
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDatabase");
            });

            // 3. Replace external services with test versions.
            services.Replace(ServiceDescriptor.Scoped<IEmailService, TestEmailService>());
            services.Replace(ServiceDescriptor.Scoped<IExternalApiService, MockExternalApiService>());
            services.Replace(ServiceDescriptor.Scoped<IFileService, InMemoryFileService>());

            // 4. Initialise the database.
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureCreated();
            // Optional seed:
            // SeedTestData(context);
        });

        // 5. Use the Testing environment.
        builder.UseEnvironment("Testing");

        // 6. Override configuration values.
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Logging:LogLevel:Default", "Warning"),
                new KeyValuePair<string, string?>("ConnectionStrings:TestDb", "InMemory"),
            });
        });
    }

    /// <summary>Seed test data.</summary>
    private static void SeedTestData(AppDbContext context)
    {
        if (!context.Shippers.Any())
        {
            context.Shippers.AddRange(
                new Shipper
                {
                    CompanyName = "Test Logistics A",
                    Phone = "02-12345678",
                    CreatedAt = DateTime.UtcNow
                },
                new Shipper
                {
                    CompanyName = "Test Logistics B",
                    Phone = "02-87654321",
                    CreatedAt = DateTime.UtcNow
                }
            );
            context.SaveChanges();
        }
    }
}

// =============================================================================
// Sample test service implementations
// =============================================================================

/// <summary>Test email service — captures sends instead of dispatching.</summary>
public class TestEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> SentEmails { get; } = new();

    public Task SendEmailAsync(string to, string subject, string body)
    {
        SentEmails.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

/// <summary>Test external API service — returns canned data.</summary>
public class MockExternalApiService : IExternalApiService
{
    public Task<string> GetDataAsync() => Task.FromResult("Mock API Response");
}

/// <summary>Test file service — backs storage with a Dictionary.</summary>
public class InMemoryFileService : IFileService
{
    private readonly Dictionary<string, byte[]> _files = new();

    public Task SaveFileAsync(string path, byte[] content)
    {
        _files[path] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetFileAsync(string path) =>
        Task.FromResult(_files.GetValueOrDefault(path));

    public Task<bool> FileExistsAsync(string path) =>
        Task.FromResult(_files.ContainsKey(path));
}
