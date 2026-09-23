// MongoDB container template — Collection Fixture pattern.
// Shares a single Mongo container across test classes. Tests achieve
// isolation via unique IDs / collection names rather than reset cycles.

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using Xunit.Abstractions;

namespace YourProject.Integration.Tests.Fixtures;

// ===== Step 1: container fixture =====

/// <summary>
/// MongoDB container fixture using the Collection Fixture pattern. One
/// container instance is shared across every class in the collection —
/// dramatic savings on startup time compared with per-class containers.
/// </summary>
public class MongoDbContainerFixture : IAsyncLifetime
{
    private MongoDbContainer? _container;

    /// <summary>The Mongo database under test.</summary>
    public IMongoDatabase Database { get; private set; } = null!;

    /// <summary>Container connection string.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Database name used by tests.</summary>
    public string DatabaseName { get; } = "testdb";

    public async Task InitializeAsync()
    {
        // MongoDB 7.0 — keep the major version aligned with production.
        _container = new MongoDbBuilder()
                     .WithImage("mongo:7.0")
                     .WithPortBinding(27017, true) // random host port
                     .Build();

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();
        var client = new MongoClient(ConnectionString);
        Database = client.GetDatabase(DatabaseName);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Drops every collection — used for inter-test isolation.</summary>
    public async Task ClearDatabaseAsync()
    {
        var collections = await Database.ListCollectionNamesAsync();
        await collections.ForEachAsync(async collectionName =>
        {
            await Database.DropCollectionAsync(collectionName);
        });
    }

    /// <summary>Convenience accessor.</summary>
    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        return Database.GetCollection<T>(collectionName);
    }
}

[CollectionDefinition("MongoDb Collection")]
public class MongoDbCollectionFixture : ICollectionFixture<MongoDbContainerFixture>
{
    // Marker only.
}

// ===== Step 2: CRUD tests against the shared container =====

/// <summary>
/// MongoDB CRUD tests — full document operations against a real engine.
/// </summary>
[Collection("MongoDb Collection")]
public class MongoUserServiceTests
{
    private readonly MongoUserService _mongoUserService;
    private readonly IMongoDatabase _database;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly MongoDbContainerFixture _fixture;

    public MongoUserServiceTests(MongoDbContainerFixture fixture)
    {
        _fixture = fixture;
        _database = fixture.Database;
        _fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        _mongoUserService = new MongoUserService(
            _database,
            Options.Create(new MongoDbSettings { UsersCollectionName = "users" }),
            NullLogger<MongoUserService>.Instance,
            _fakeTimeProvider);
    }

    [Fact]
    public async Task CreateUserAsync_WhenUserIsValid_ShouldPersistDocument()
    {
        // Arrange — unique username / email avoids collisions with other tests.
        var user = new UserDocument
        {
            Username = $"testuser_{Guid.NewGuid():N}",
            Email = $"test_{Guid.NewGuid():N}@example.com",
            Profile = new UserProfile
            {
                FirstName = "Test",
                LastName = "User",
                Bio = "Test user bio"
            }
        };

        // Act
        var result = await _mongoUserService.CreateUserAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be(user.Username);
        result.Email.Should().Be(user.Email);
        result.Id.Should().NotBeEmpty();
        result.CreatedAt.Should().Be(_fakeTimeProvider.GetUtcNow().DateTime);
        result.Version.Should().Be(1);
    }

    [Fact]
    public async Task CreateUserAsync_WhenEmailAlreadyExists_ShouldThrowInvalidOperation()
    {
        // Arrange
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";
        var user1 = new UserDocument { Username = $"user1_{Guid.NewGuid():N}", Email = email };
        var user2 = new UserDocument { Username = $"user2_{Guid.NewGuid():N}", Email = email };

        await _mongoUserService.CreateUserAsync(user1);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mongoUserService.CreateUserAsync(user2));
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenIdExists_ShouldReturnUser()
    {
        var user = new UserDocument
        {
            Username = $"gettest_{Guid.NewGuid():N}",
            Email = $"gettest_{Guid.NewGuid():N}@example.com",
            Profile = new UserProfile { FirstName = "Get", LastName = "Test" }
        };
        var createdUser = await _mongoUserService.CreateUserAsync(user);

        var result = await _mongoUserService.GetUserByIdAsync(createdUser.Id);

        result.Should().NotBeNull();
        result!.Username.Should().Be(user.Username);
        result.Email.Should().Be(user.Email);
        result.Profile.FirstName.Should().Be("Get");
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenIdDoesNotExist_ShouldReturnNull()
    {
        var nonExistentId = ObjectId.GenerateNewId().ToString();

        var result = await _mongoUserService.GetUserByIdAsync(nonExistentId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUserAsync_WithOptimisticLocking_ShouldBumpVersion()
    {
        var user = new UserDocument
        {
            Username = $"updatetest_{Guid.NewGuid():N}",
            Email = $"updatetest_{Guid.NewGuid():N}@example.com"
        };
        var createdUser = await _mongoUserService.CreateUserAsync(user);
        createdUser.Profile.Bio = "Updated bio";

        var result = await _mongoUserService.UpdateUserAsync(createdUser);

        result.Should().NotBeNull();
        result!.Version.Should().Be(2);
        result.Profile.Bio.Should().Be("Updated bio");
    }

    [Fact]
    public async Task DeleteUserAsync_WhenIdExists_ShouldReturnTrueAndRemoveDocument()
    {
        var user = new UserDocument
        {
            Username = $"deletetest_{Guid.NewGuid():N}",
            Email = $"deletetest_{Guid.NewGuid():N}@example.com"
        };
        var createdUser = await _mongoUserService.CreateUserAsync(user);

        var result = await _mongoUserService.DeleteUserAsync(createdUser.Id);

        result.Should().BeTrue();
        var deletedUser = await _mongoUserService.GetUserByIdAsync(createdUser.Id);
        deletedUser.Should().BeNull();
    }
}

// ===== Step 3: BSON serialization tests (no container needed) =====

public class MongoBsonTests
{
    [Fact]
    public void ObjectId_Generate_ShouldProduceValidId()
    {
        var objectId = ObjectId.GenerateNewId();

        objectId.Should().NotBeNull();
        objectId.CreationTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        objectId.ToString().Should().HaveLength(24);
    }

    [Fact]
    public void BsonDocument_WithNullValue_ShouldRoundTripCorrectly()
    {
        var doc = new BsonDocument
        {
            ["name"] = "John",
            ["email"] = BsonNull.Value,
            ["age"] = 25
        };

        var json = doc.ToJson();

        json.Should().Contain("\"email\" : null");
        doc["email"].IsBsonNull.Should().BeTrue();
    }
}

// ===== Step 4: index tests =====

/// <summary>Index creation / uniqueness / compound-key tests.</summary>
[Collection("MongoDb Collection")]
public class MongoIndexTests
{
    private readonly MongoDbContainerFixture _fixture;
    private readonly IMongoCollection<UserDocument> _users;
    private readonly ITestOutputHelper _output;

    public MongoIndexTests(MongoDbContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _users = fixture.Database.GetCollection<UserDocument>("index_test_users");
        _output = output;
    }

    [Fact]
    public async Task CreateUniqueIndex_OnEmail_ShouldBlockDuplicateInsert()
    {
        // Arrange — start from an empty collection.
        await _users.DeleteManyAsync(FilterDefinition<UserDocument>.Empty);

        var indexKeysDefinition = Builders<UserDocument>.IndexKeys.Ascending(u => u.Email);
        var indexOptions = new CreateIndexOptions { Unique = true };
        await _users.Indexes.CreateOneAsync(
            new CreateIndexModel<UserDocument>(indexKeysDefinition, indexOptions));

        var uniqueEmail = $"unique_{Guid.NewGuid():N}@example.com";
        var user1 = new UserDocument { Username = "user1", Email = uniqueEmail };
        var user2 = new UserDocument { Username = "user2", Email = uniqueEmail };

        // Act & Assert
        await _users.InsertOneAsync(user1);

        var exception = await Assert.ThrowsAsync<MongoWriteException>(
            () => _users.InsertOneAsync(user2));
        exception.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);
    }
}

// ===== Document model =====

public class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("username")]
    [BsonRequired]
    public string Username { get; set; } = string.Empty;

    [BsonElement("email")]
    [BsonRequired]
    public string Email { get; set; } = string.Empty;

    [BsonElement("profile")]
    public UserProfile Profile { get; set; } = new();

    [BsonElement("created_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    public void IncrementVersion(DateTime updateTime)
    {
        Version++;
        UpdatedAt = updateTime;
    }
}

public class UserProfile
{
    [BsonElement("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [BsonElement("last_name")]
    public string LastName { get; set; } = string.Empty;

    [BsonElement("bio")]
    public string Bio { get; set; } = string.Empty;

    [BsonIgnore]
    public string FullName => $"{FirstName} {LastName}".Trim();
}

// MongoUserService / MongoDbSettings live in your application code.
public class MongoDbSettings { public string UsersCollectionName { get; set; } = "users"; }
