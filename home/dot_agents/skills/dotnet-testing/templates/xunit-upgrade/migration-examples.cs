// =============================================================================
// xUnit 2.x -> 3.x code migration examples
// =============================================================================

using System.Runtime.InteropServices;
using Xunit;

namespace XunitUpgradeGuide.Examples;

// =============================================================================
// 1. async void -> async Task
// =============================================================================

/// <summary>
/// Before: async void test (rejected by xUnit 3.x)
/// </summary>
public class AsyncVoidTests_Before
{
    // Fails in xUnit 3.x
    // [Fact]
    // public async void TestAsyncMethod()
    // {
    //     var result = await SomeAsyncOperation();
    //     Assert.True(result);
    // }
}

/// <summary>
/// After: async Task test (correct form).
/// </summary>
public class AsyncVoidTests_After
{
    [Fact]
    public async Task TestAsyncMethod()
    {
        var result = await SomeAsyncOperation();
        Assert.True(result);
    }

    private Task<bool> SomeAsyncOperation() => Task.FromResult(true);
}

// =============================================================================
// 2. IAsyncLifetime + IDisposable
// =============================================================================

/// <summary>
/// Before: a class implements both IAsyncLifetime and IDisposable.
/// xUnit 2.x called both Dispose and DisposeAsync;
/// xUnit 3.x calls only DisposeAsync.
/// </summary>
public class AsyncLifetimeTests_Before // : IAsyncLifetime, IDisposable
{
    // public async Task InitializeAsync() { /* init */ }
    // public async Task DisposeAsync() { /* async cleanup */ }
    // public void Dispose() { /* sync cleanup — NOT called in 3.x */ }
}

/// <summary>
/// After: keep IAsyncLifetime, consolidate cleanup in DisposeAsync.
/// </summary>
public class AsyncLifetimeTests_After : IAsyncLifetime
{
    private IDisposable? _resource;

    public async Task InitializeAsync()
    {
        // Initialise the resource
        _resource = await CreateResourceAsync();
    }

    public async Task DisposeAsync()
    {
        // All cleanup lives here
        _resource?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public void Test1()
    {
        Assert.NotNull(_resource);
    }

    private Task<IDisposable> CreateResourceAsync()
        => Task.FromResult<IDisposable>(new MemoryStream());
}

// =============================================================================
// 3. SkippableFact -> Assert.Skip
// =============================================================================

/// <summary>
/// Before: SkippableFact (removed in xUnit 3.x).
/// </summary>
public class SkippableTests_Before
{
    // [SkippableFact]
    // public void SkippableTest()
    // {
    //     Skip.If(!IsWindowsEnvironment, "Windows only");
    //     // test logic
    // }
}

/// <summary>
/// After (imperative): Assert.Skip from inside the test.
/// </summary>
public class SkippableTests_After_Imperative
{
    [Fact]
    public void SkipsBasedOnPlatform()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Skip("This test only runs on Windows");
        }

        // test logic
        Assert.True(true);
    }

    [Fact]
    public void SkipsBasedOnEnvironmentVariable()
    {
        var enableTests = Environment.GetEnvironmentVariable("ENABLE_INTEGRATION_TESTS");

        if (string.IsNullOrEmpty(enableTests) || enableTests.ToLower() != "true")
        {
            Assert.Skip("Integration tests disabled. Set ENABLE_INTEGRATION_TESTS=true to run.");
        }

        // integration test logic
        Assert.True(true);
    }
}

/// <summary>
/// After (declarative): SkipUnless / SkipWhen attributes on [Fact].
/// </summary>
public class SkippableTests_After_Declarative
{
    [Fact(SkipUnless = nameof(IsWindowsEnvironment),
          Skip = "This test only runs on Windows")]
    public void WindowsOnlyTest()
    {
        Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }

    [Fact(SkipWhen = nameof(IsCIEnvironment),
          Skip = "Skipped in CI environments")]
    public void LocalOnlyTest()
    {
        Assert.True(true);
    }

    // Static properties used by SkipUnless / SkipWhen
    public static bool IsWindowsEnvironment
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsCIEnvironment
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
}

// =============================================================================
// 4. Custom DataAttribute signature
// =============================================================================

/// <summary>
/// Before: xUnit 2.x DataAttribute (synchronous GetData).
/// </summary>
// public class CustomDataAttribute_Before : DataAttribute
// {
//     public override IEnumerable<object[]> GetData(MethodInfo testMethod)
//     {
//         yield return new object[] { 1, "test1" };
//         yield return new object[] { 2, "test2" };
//     }
// }

/// <summary>
/// After: xUnit 3.x DataAttribute (async GetData with DisposalTracker).
/// </summary>
public class CustomDataAttribute_After : DataAttribute
{
    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker)
    {
        // Supports asynchronous data loading
        var data = await LoadDataAsync();

        return data.Select(item => new TheoryDataRow(item.Id, item.Name))
                   .ToList();
    }

    private Task<List<(int Id, string Name)>> LoadDataAsync()
    {
        var data = new List<(int, string)>
        {
            (1, "test1"),
            (2, "test2"),
            (3, "test3")
        };
        return Task.FromResult(data);
    }
}

public class CustomDataAttributeTests
{
    [Theory]
    [CustomData_After]
    public void CustomAttributeProducesData(int id, string name)
    {
        Assert.True(id > 0);
        Assert.NotNullOrEmpty(name);
    }
}

// =============================================================================
// 5. ITestOutputHelper
// =============================================================================

/// <summary>
/// ITestOutputHelper still works in xUnit 3.x; the namespace has moved.
/// </summary>
public class TestOutputTests
{
    private readonly ITestOutputHelper _output;

    public TestOutputTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EmitsDiagnostics()
    {
        _output.WriteLine("Test starting");

        var result = 1 + 1;
        _output.WriteLine($"Computed: {result}");

        Assert.Equal(2, result);
    }
}
