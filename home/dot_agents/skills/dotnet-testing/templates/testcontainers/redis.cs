// Redis container template — Collection Fixture pattern covering all
// five core data structures (String, Hash, List, Set, Sorted Set), TTL
// behavior, and key-pattern isolation.

using StackExchange.Redis;
using Testcontainers.Redis;
using AwesomeAssertions;
using Xunit;

namespace YourProject.Integration.Tests.Fixtures;

// ===== Step 1: container fixture =====

/// <summary>
/// Redis container fixture using the Collection Fixture pattern. One
/// container shared across all five data-structure test classes.
/// </summary>
public class RedisContainerFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    /// <summary>Connection multiplexer (manages the connection pool).</summary>
    public IConnectionMultiplexer Connection { get; private set; } = null!;

    /// <summary>Default database (DB 0).</summary>
    public IDatabase Database { get; private set; } = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new RedisBuilder()
                     .WithImage("redis:7.2")
                     .WithPortBinding(6379, true)
                     .Build();

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();
        Connection = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
        Database = Connection.GetDatabase();
    }

    public async Task DisposeAsync()
    {
        if (Connection != null)
        {
            await Connection.DisposeAsync();
        }

        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Clears the database via KeyDelete (not FLUSHDB). Many Redis images
    /// disable the admin command set by default, which makes FLUSHDB fail.
    /// </summary>
    public async Task ClearDatabaseAsync()
    {
        var server = Connection.GetServer(Connection.GetEndPoints().First());
        var keys = server.Keys(Database.Database);
        if (keys.Any())
        {
            await Database.KeyDeleteAsync(keys.ToArray());
        }
    }

    /// <summary>Returns the Redis server (for Keys scans, etc.).</summary>
    public IServer GetServer()
    {
        return Connection.GetServer(Connection.GetEndPoints().First());
    }

    /// <summary>Deletes all keys matching a glob pattern.</summary>
    public async Task DeleteKeysByPatternAsync(string pattern)
    {
        var server = GetServer();
        var keys = server.Keys(Database.Database, pattern);
        if (keys.Any())
        {
            await Database.KeyDeleteAsync(keys.ToArray());
        }
    }
}

[CollectionDefinition("Redis Collection")]
public class RedisCollectionFixture : ICollectionFixture<RedisContainerFixture>
{
    // Marker only.
}

// ===== Step 2: data-structure tests =====

/// <summary>
/// Tests for Redis's five core data structures. Each test uses a unique
/// key prefix (GUID-derived) so tests can run in parallel without
/// interfering with each other.
/// </summary>
[Collection("Redis Collection")]
public class RedisDataStructureTests
{
    private readonly RedisCacheService _redisCacheService;
    private readonly RedisContainerFixture _fixture;

    public RedisDataStructureTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
        _redisCacheService = new RedisCacheService(
            fixture.Connection,
            Options.Create(new RedisSettings()),
            NullLogger<RedisCacheService>.Instance,
            TimeProvider.System);
    }

    // ----- String -----

    [Fact]
    public async Task String_SetAndGet_WithSimpleValue_ShouldRoundTrip()
    {
        var key = $"test_string_{Guid.NewGuid():N}";
        var value = "test_string_value";

        var setResult = await _redisCacheService.SetStringAsync(key, value);
        var getResult = await _redisCacheService.GetStringAsync<string>(key);

        setResult.Should().BeTrue();
        getResult.Should().Be(value);
    }

    [Fact]
    public async Task String_SetObject_WithComplexObject_ShouldSerializeAndDeserialize()
    {
        var key = $"object_test_{Guid.NewGuid():N}";
        var user = new UserDocument
        {
            Username = "objecttest",
            Email = "object@test.com"
        };

        var setResult = await _redisCacheService.SetStringAsync(key, user, TimeSpan.FromMinutes(30));
        var getResult = await _redisCacheService.GetStringAsync<UserDocument>(key);

        setResult.Should().BeTrue();
        getResult.Should().NotBeNull();
        getResult!.Username.Should().Be("objecttest");
        getResult.Email.Should().Be("object@test.com");
    }

    [Fact]
    public async Task String_GetNonExistent_ShouldReturnNull()
    {
        var key = $"nonexistent_{Guid.NewGuid():N}";

        var result = await _redisCacheService.GetStringAsync<string>(key);

        result.Should().BeNull();
    }

    // ----- Hash -----

    [Fact]
    public async Task Hash_SetAndGet_ShouldRoundTripField()
    {
        var key = $"hash_test_{Guid.NewGuid():N}";
        var field = "test_field";
        var value = "test_value";

        var setResult = await _redisCacheService.SetHashAsync(key, field, value, TimeSpan.FromMinutes(30));
        var getResult = await _redisCacheService.GetHashAsync<string>(key, field);

        setResult.Should().BeTrue();
        getResult.Should().Be(value);
    }

    [Fact]
    public async Task Hash_SetAll_WithObject_ShouldPopulateAllFields()
    {
        var key = $"hash_all_{Guid.NewGuid():N}";
        var session = new UserSession
        {
            UserId = "user123",
            SessionId = "session456",
            IpAddress = "192.168.1.1",
            UserAgent = "Test Browser",
            IsActive = true
        };

        var setResult = await _redisCacheService.SetHashAllAsync(key, session, TimeSpan.FromHours(1));
        var getResult = await _redisCacheService.GetHashAllAsync<UserSession>(key);

        setResult.Should().BeTrue();
        getResult.Should().NotBeNull();
        getResult!.UserId.Should().Be("user123");
        getResult.SessionId.Should().Be("session456");
        getResult.IpAddress.Should().Be("192.168.1.1");
        getResult.IsActive.Should().BeTrue();
    }

    // ----- List -----

    [Fact]
    public async Task List_LeftPush_ShouldStoreInLifoOrder()
    {
        var key = $"list_test_{Guid.NewGuid():N}";
        var view1 = new RecentView { ItemId = "item1", Title = "Product 1" };
        var view2 = new RecentView { ItemId = "item2", Title = "Product 2" };
        var view3 = new RecentView { ItemId = "item3", Title = "Product 3" };

        await _redisCacheService.ListLeftPushAsync(key, view1);
        await _redisCacheService.ListLeftPushAsync(key, view2);
        await _redisCacheService.ListLeftPushAsync(key, view3);

        var views = await _redisCacheService.ListRangeAsync<RecentView>(key);

        // Last in is first out.
        views.Should().HaveCount(3);
        views[0].ItemId.Should().Be("item3");
        views[1].ItemId.Should().Be("item2");
        views[2].ItemId.Should().Be("item1");
    }

    [Fact]
    public async Task List_Trim_ShouldKeepOnlyTheSpecifiedRange()
    {
        var key = $"list_trim_{Guid.NewGuid():N}";
        for (int i = 1; i <= 10; i++)
        {
            await _redisCacheService.ListLeftPushAsync(key, new RecentView
            {
                ItemId = $"item{i}",
                Title = $"Product {i}"
            });
        }

        await _redisCacheService.ListTrimAsync(key, 0, 4);
        var views = await _redisCacheService.ListRangeAsync<RecentView>(key);

        views.Should().HaveCount(5);
        views[0].ItemId.Should().Be("item10");
        views[4].ItemId.Should().Be("item6");
    }

    // ----- Set -----

    [Fact]
    public async Task Set_Add_WithUniqueValues_ShouldStoreAll()
    {
        var key = $"set_test_{Guid.NewGuid():N}";

        var result1 = await _redisCacheService.SetAddAsync(key, "programming");
        var result2 = await _redisCacheService.SetAddAsync(key, "testing");
        var result3 = await _redisCacheService.SetAddAsync(key, "dotnet");

        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();

        var tags = await _redisCacheService.SetMembersAsync<string>(key);
        tags.Should().HaveCount(3);
        tags.Should().Contain("programming");
        tags.Should().Contain("testing");
        tags.Should().Contain("dotnet");
    }

    [Fact]
    public async Task Set_Add_WithDuplicate_ShouldReturnFalse()
    {
        var key = $"set_dup_{Guid.NewGuid():N}";

        var result1 = await _redisCacheService.SetAddAsync(key, "programming");
        var result2 = await _redisCacheService.SetAddAsync(key, "programming"); // duplicate

        result1.Should().BeTrue();
        result2.Should().BeFalse();

        var tags = await _redisCacheService.SetMembersAsync<string>(key);
        tags.Should().HaveCount(1);
    }

    // ----- Sorted Set -----

    [Fact]
    public async Task SortedSet_Add_ShouldOrderByScore()
    {
        var key = $"zset_test_{Guid.NewGuid():N}";
        var entry1 = new LeaderboardEntry { UserId = "user1", Username = "Player1", Score = 100 };
        var entry2 = new LeaderboardEntry { UserId = "user2", Username = "Player2", Score = 200 };
        var entry3 = new LeaderboardEntry { UserId = "user3", Username = "Player3", Score = 150 };

        await _redisCacheService.SortedSetAddAsync(key, entry1, entry1.Score);
        await _redisCacheService.SortedSetAddAsync(key, entry2, entry2.Score);
        await _redisCacheService.SortedSetAddAsync(key, entry3, entry3.Score);

        var rankings = await _redisCacheService.SortedSetRangeWithScoresAsync<LeaderboardEntry>(
            key, 0, -1, Order.Descending);

        rankings.Should().HaveCount(3);
        rankings[0].Member.Username.Should().Be("Player2");
        rankings[0].Score.Should().Be(200);
        rankings[1].Member.Username.Should().Be("Player3");
        rankings[2].Member.Username.Should().Be("Player1");
    }

    [Fact]
    public async Task SortedSet_IncrementScore_ShouldAccumulate()
    {
        var key = $"zset_incr_{Guid.NewGuid():N}";
        var entry = new LeaderboardEntry { UserId = "user1", Username = "Player1", Score = 100 };
        await _redisCacheService.SortedSetAddAsync(key, entry, entry.Score);

        var newScore = await _redisCacheService.SortedSetIncrementAsync(key, entry, 50);

        newScore.Should().Be(150);
    }

    // ----- TTL -----

    [Fact]
    public async Task Expire_WithTimeSpan_ShouldSetTtl()
    {
        var key = $"expire_test_{Guid.NewGuid():N}";
        await _redisCacheService.SetStringAsync(key, "expire_value");

        var result = await _redisCacheService.ExpireAsync(key, TimeSpan.FromMinutes(5));

        result.Should().BeTrue();
        var ttl = await _redisCacheService.GetTtlAsync(key);
        ttl.Should().NotBeNull();
        ttl!.Value.TotalMinutes.Should().BeGreaterThan(4);
    }

    // ----- Isolation -----

    [Fact]
    public async Task DataIsolation_WithUniqueKeyPrefix_ShouldNotCollideAcrossTests()
    {
        var testId = Guid.NewGuid().ToString("N")[..8];
        var key1 = $"isolation:{testId}:key1";
        var key2 = $"isolation:{testId}:key2";

        await _redisCacheService.SetStringAsync(key1, "value1");
        await _redisCacheService.SetStringAsync(key2, "value2");

        var value1 = await _redisCacheService.GetStringAsync<string>(key1);
        var value2 = await _redisCacheService.GetStringAsync<string>(key2);

        value1.Should().Be("value1");
        value2.Should().Be("value2");

        // Clean up only what this test created.
        await _redisCacheService.DeleteAsync(key1);
        await _redisCacheService.DeleteAsync(key2);
    }
}

// ===== Domain models =====

public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class RecentView
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public double Score { get; set; }
}

// RedisCacheService / RedisSettings / UserDocument live in your application code.
public class RedisSettings { }
