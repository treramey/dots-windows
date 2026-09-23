// TUnit + ASP.NET Core integration testing via WebApplicationFactory.
// Implements IDisposable to release the factory and HttpClient.
// For the API project's Program to be visible to tests, add
// `public partial class Program { }` at the bottom of Program.cs.

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TUnit.Advanced.AspNetCore.Examples;

// ===== Basic integration tests =====

/// <summary>
/// ASP.NET Core integration tests against an in-memory host.
/// </summary>
public class WebApiIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public WebApiIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Inject test doubles, swap DB connection strings, etc.
                });
            });

        _client = _factory.CreateClient();
    }

    [Test]
    public async Task WeatherForecast_Get_ShouldReturnContent()
    {
        var response = await _client.GetAsync("/weatherforecast");

        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsNotNull();
        await Assert.That(content.Length).IsGreaterThan(0);
    }

    [Test]
    [Property("Category", "Integration")]
    public async Task WeatherForecast_ResponseHeaders_ShouldBeJson()
    {
        var response = await _client.GetAsync("/weatherforecast");

        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        await Assert.That(contentType).IsEqualTo("application/json");
    }

    [Test]
    [Property("Category", "Smoke")]
    public async Task WeatherForecast_SmokeTest_ShouldRespondWithBody()
    {
        var response = await _client.GetAsync("/weatherforecast");

        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsNotNull();
        await Assert.That(content.Length).IsGreaterThan(10);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}

// ===== Performance / load =====

public class PerformanceIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PerformanceIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [Test]
    [Property("Category", "Performance")]
    [Timeout(10000)]
    public async Task WeatherForecast_ResponseTime_ShouldBeUnderFiveSeconds()
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await _client.GetAsync("/weatherforecast");
        stopwatch.Stop();

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(5000);
    }

    [Test]
    [Property("Category", "Load")]
    [Timeout(30000)]
    public async Task WeatherForecast_FiftyConcurrentRequests_ShouldAllSucceed()
    {
        const int concurrentRequests = 50;
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_client.GetAsync("/weatherforecast"));
        }

        var responses = await Task.WhenAll(tasks);

        await Assert.That(responses.Length).IsEqualTo(concurrentRequests);
        await Assert.That(responses.All(r => r.IsSuccessStatusCode)).IsTrue();

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}

// Required for tests to reference the API host. In a real project add
// this at the bottom of the API's Program.cs; it is kept here so the
// template compiles standalone.
public partial class Program { }
