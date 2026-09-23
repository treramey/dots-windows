// TUnit lifecycle templates — constructor + Dispose pattern, and
// [Before/After] attributes at Test / Class / Assembly scope.

namespace MyApp.Tests;

// ===== Pattern 1: constructor + IDisposable (xUnit-compatible) =====

/// <summary>
/// Constructor / Dispose mirrors xUnit. Constructor runs before each
/// test; Dispose runs after each test.
/// </summary>
public class BasicLifecycleTests : IDisposable
{
    private readonly Calculator _calculator;

    public BasicLifecycleTests()
    {
        _calculator = new Calculator();
    }

    [Test]
    public async Task Add_Basic_ShouldReturnSum()
    {
        await Assert.That(_calculator.Add(1, 2)).IsEqualTo(3);
    }

    [Test]
    public async Task Multiply_Basic_ShouldReturnProduct()
    {
        await Assert.That(_calculator.Multiply(3, 4)).IsEqualTo(12);
    }

    public void Dispose()
    {
        // release per-test resources here
    }
}

// ===== Pattern 2: [Before/After] attributes at multiple scopes =====

/// <summary>
/// Advanced lifecycle using attributes. <c>[Before(Class)]</c> runs once
/// before any test in the class; <c>[Before(Test)]</c> runs before each
/// test. After-equivalents mirror them. Class hooks are static.
/// </summary>
public class DatabaseLifecycleTests
{
    private static TestDatabase? _database;

    /// <summary>Class-level setup — runs once.</summary>
    [Before(Class)]
    public static async Task ClassSetup()
    {
        _database = new TestDatabase();
        await _database.InitializeAsync();
    }

    /// <summary>Per-test setup — runs before each test.</summary>
    [Before(Test)]
    public async Task TestSetup()
    {
        await _database!.ClearDataAsync();
    }

    [Test]
    public async Task UserCreation_ShouldPersist()
    {
        var userService = new UserService(_database!);

        var user = await userService.CreateUserAsync("test@example.com");

        await Assert.That(user.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(user.Email).IsEqualTo("test@example.com");
    }

    [Test]
    public async Task UserLookup_AfterCreate_ShouldReturnUser()
    {
        var userService = new UserService(_database!);
        await userService.CreateUserAsync("query@example.com");

        var user = await userService.GetUserByEmailAsync("query@example.com");

        await Assert.That(user).IsNotNull();
        await Assert.That(user!.Email).IsEqualTo("query@example.com");
    }

    [After(Test)]
    public async Task TestTearDown()
    {
        await Task.CompletedTask;
    }

    [After(Class)]
    public static async Task ClassTearDown()
    {
        if (_database != null)
        {
            await _database.DisposeAsync();
        }
    }
}

// ===== Pattern 3: execution order demonstration =====

/// <summary>
/// Demonstrates the full execution order:
/// Before(Class) -> ctor -> Before(Test) -> test method ->
/// After(Test) -> Dispose -> After(Class).
/// </summary>
public class LifecycleOrderDemoTests : IDisposable
{
    public LifecycleOrderDemoTests()
    {
        Console.WriteLine("2. constructor");
    }

    [Before(Class)]
    public static void ClassSetup() => Console.WriteLine("1. Before(Class)");

    [Before(Test)]
    public async Task TestSetup()
    {
        Console.WriteLine("3. Before(Test)");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Demo()
    {
        Console.WriteLine("4. test method");
        await Assert.That(true).IsTrue();
    }

    [After(Test)]
    public async Task TestTearDown()
    {
        Console.WriteLine("5. After(Test)");
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        Console.WriteLine("6. Dispose");
    }

    [After(Class)]
    public static void ClassTearDown() => Console.WriteLine("7. After(Class)");
}

// ===== Pattern 4: async lifecycle hooks =====

/// <summary>Demonstrates async setup and teardown.</summary>
public class AsyncLifecycleTests
{
    private HttpClient? _httpClient;

    [Before(Test)]
    public async Task SetupAsync()
    {
        _httpClient = new HttpClient();
        await Task.Delay(100); // simulate async init
    }

    [Test]
    public async Task HttpRequest_ShouldSucceed()
    {
        var response = await _httpClient!.GetAsync("https://httpbin.org/status/200");
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }

    [After(Test)]
    public async Task TearDownAsync()
    {
        _httpClient?.Dispose();
        await Task.CompletedTask;
    }
}

// ===== supporting types =====

public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}

public class TestDatabase : IAsyncDisposable
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task ClearDataAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class UserService
{
    private readonly TestDatabase _database;
    public UserService(TestDatabase database) => _database = database;

    public Task<User> CreateUserAsync(string email) =>
        Task.FromResult(new User { Id = Guid.NewGuid(), Email = email });

    public Task<User?> GetUserByEmailAsync(string email) =>
        Task.FromResult<User?>(new User { Id = Guid.NewGuid(), Email = email });
}

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
}
