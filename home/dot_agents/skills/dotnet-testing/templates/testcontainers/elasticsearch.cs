// Elasticsearch container template — single-class IAsyncLifetime fixture.
// Upstream kevintsengtw NoSQL skill does not ship a dedicated ES template;
// this template applies the same Testcontainers patterns to the official
// Elastic.Clients.Elasticsearch client. Tune the image tag to match the
// production cluster version.

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Testcontainers.Elasticsearch;
using AwesomeAssertions;

namespace YourProject.Integration.Tests.Fixtures;

/// <summary>
/// Elasticsearch container fixture — manages container lifecycle and
/// exposes a typed client for the tests.
/// </summary>
public class ElasticsearchContainerTests : IAsyncLifetime
{
    private readonly ElasticsearchContainer _container;
    private ElasticsearchClient _client = null!;

    public ElasticsearchContainerTests()
    {
        _container = new ElasticsearchBuilder()
            // Pin to the major version that matches the deployed cluster.
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.13.0")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // ElasticsearchContainer enables HTTPS with a self-signed cert; the
        // test client must accept it.
        var settings = new ElasticsearchClientSettings(new Uri(_container.GetConnectionString()))
            .ServerCertificateValidationCallback(CertificateValidations.AllowAll)
            .Authentication(new BasicAuthentication("elastic", ElasticsearchBuilder.DefaultPassword))
            .DefaultIndex("test-index");

        _client = new ElasticsearchClient(settings);

        // Ensure the index exists before tests run.
        var existsResponse = await _client.Indices.ExistsAsync("test-index");
        if (!existsResponse.Exists)
        {
            await _client.Indices.CreateAsync("test-index");
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    // ===== Example tests =====

    [Fact]
    public async Task IndexAndGet_WithSimpleDocument_ShouldRoundTrip()
    {
        // Arrange
        var doc = new Article
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "Testcontainers integration testing",
            Body = "Using Testcontainers to run Elasticsearch in CI."
        };

        // Act
        var indexResponse = await _client.IndexAsync(doc, idx => idx.Index("test-index").Id(doc.Id));
        await _client.Indices.RefreshAsync("test-index"); // make doc immediately searchable

        var getResponse = await _client.GetAsync<Article>(doc.Id, idx => idx.Index("test-index"));

        // Assert
        indexResponse.IsValidResponse.Should().BeTrue();
        getResponse.Found.Should().BeTrue();
        getResponse.Source.Should().NotBeNull();
        getResponse.Source!.Title.Should().Be(doc.Title);
    }

    [Fact]
    public async Task Search_WithMatchQuery_ShouldReturnRelevantDocuments()
    {
        // Arrange — seed two documents with distinct text.
        var doc1 = new Article { Id = "doc1", Title = "Cats and dogs", Body = "Pets are great." };
        var doc2 = new Article { Id = "doc2", Title = "Quantum physics", Body = "Schrödinger's experiment." };

        await _client.IndexAsync(doc1, idx => idx.Index("test-index").Id(doc1.Id));
        await _client.IndexAsync(doc2, idx => idx.Index("test-index").Id(doc2.Id));
        await _client.Indices.RefreshAsync("test-index");

        // Act
        var searchResponse = await _client.SearchAsync<Article>(s => s
            .Index("test-index")
            .Query(q => q.Match(m => m.Field(f => f.Title).Query("cats"))));

        // Assert
        searchResponse.IsValidResponse.Should().BeTrue();
        searchResponse.Documents.Should().ContainSingle()
            .Which.Id.Should().Be("doc1");
    }

    [Fact]
    public void GetConnectionString_AfterContainerStarted_ShouldReturnHttpsEndpoint()
    {
        var connectionString = _container.GetConnectionString();
        connectionString.Should().StartWith("https://");
    }
}

// ===== Domain model =====

public class Article
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
