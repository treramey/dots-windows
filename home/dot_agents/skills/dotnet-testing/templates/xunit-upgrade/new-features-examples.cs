// =============================================================================
// xUnit 3.x new-feature examples
// =============================================================================

using System.Globalization;
using System.Runtime.InteropServices;
using Xunit;

namespace XunitUpgradeGuide.Examples;

// =============================================================================
// 1. [Test] attribute — alternate spelling of [Fact]
// =============================================================================

public class TestAttributeExamples
{
    // [Test] is functionally identical to [Fact]
    [Test]
    public void UsingTestAttribute()
    {
        Assert.True(true);
    }

    [Fact]
    public void UsingFactAttribute()
    {
        Assert.True(true);
    }

    // [Theory] still drives parameterised tests
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 1, 0)]
    public void ParameterisedTest(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }
}

// =============================================================================
// 2. Explicit tests
// =============================================================================

public class ExplicitTestExamples
{
    // Explicit tests are excluded from default discovery — opt in by name
    [Fact(Explicit = true)]
    public void ExpensiveIntegrationTest()
    {
        // Long-running, performance, or special-environment tests
        Thread.Sleep(1000);
        Assert.True(true);
    }

    [Fact(Explicit = true)]
    public void SpecialEnvironmentTest()
    {
        // e.g. needs a specific database or external service
        Assert.True(true);
    }
}

// =============================================================================
// 3. Dynamic skipping
// =============================================================================

public class DynamicSkipExamples
{
    // Imperative — Assert.Skip from inside the body
    [Fact]
    public void SkipBasedOnFeatureFlag()
    {
        var featureEnabled = GetFeatureFlag("NEW_CALCULATION_ENGINE");

        if (!featureEnabled)
        {
            Assert.Skip("New calculation engine is not enabled");
        }

        // test new functionality
        Assert.True(true);
    }

    // Declarative — SkipUnless attribute
    [Fact(SkipUnless = nameof(IsLinuxEnvironment),
          Skip = "Linux only")]
    public void LinuxOnlyTest()
    {
        Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
    }

    // Declarative — SkipWhen attribute
    [Fact(SkipWhen = nameof(IsDebugBuild),
          Skip = "Skipped in Debug builds")]
    public void ReleaseOnlyTest()
    {
        Assert.True(true);
    }

    public static bool IsLinuxEnvironment
        => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public static bool IsDebugBuild
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    private bool GetFeatureFlag(string flagName)
        => bool.TryParse(Environment.GetEnvironmentVariable($"FEATURE_{flagName}"),
                         out var result) && result;
}

// =============================================================================
// 4. MatrixTheoryData
// =============================================================================

public class MatrixTheoryDataExamples
{
    // 3 x 3 = 9 combinations
    public static TheoryData<int, string> MatrixData =>
        new MatrixTheoryData<int, string>(
            [1, 2, 3],
            ["Hello", "World", "Test"]
        );

    [Theory]
    [MemberData(nameof(MatrixData))]
    public void MatrixTest(int number, string text)
    {
        Assert.True(number > 0);
        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    // 3 x 3 x 2 = 18 combinations
    public static TheoryData<string, int, bool> ComplexMatrixData =>
        new MatrixTheoryData<string, int, bool>(
            ["Admin", "User", "Guest"],
            [1, 5, 10],
            [true, false]
        );

    [Theory]
    [MemberData(nameof(ComplexMatrixData))]
    public void ComplexMatrixTest(string role, int level, bool enabled)
    {
        Assert.NotNull(role);
        Assert.InRange(level, 1, 10);
    }
}

// =============================================================================
// 5. Assembly fixtures (assembly-scoped shared resources)
// =============================================================================

/// <summary>
/// Assembly fixture: shared across every test in the assembly.
/// Treat as immutable post-construction.
/// </summary>
public class TestDatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Runs once before any test
        ConnectionString = "Server=localhost;Database=TestDb;";
        await Task.CompletedTask;

        Console.WriteLine("Assembly fixture initialised");
    }

    public async Task DisposeAsync()
    {
        // Runs once after all tests
        await Task.CompletedTask;

        Console.WriteLine("Assembly fixture disposed");
    }
}

// Register the fixture in AssemblyInfo.cs or the project root:
// [assembly: AssemblyFixture(typeof(TestDatabaseFixture))]

public class AssemblyFixtureExamples
{
    private readonly TestDatabaseFixture _dbFixture;

    public AssemblyFixtureExamples(TestDatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    [Fact]
    public void UsesAssemblyFixture()
    {
        Assert.NotNull(_dbFixture.ConnectionString);
    }
}

// =============================================================================
// 6. Test pipeline startup (one-time pre-test initialisation)
// =============================================================================

/// <summary>
/// Runs once before any test in the assembly — ideal for global setup.
/// </summary>
public class CustomTestPipelineStartup : ITestPipelineStartup
{
    public async ValueTask StartAsync(IMessageSink diagnosticMessageSink)
    {
        diagnosticMessageSink.OnMessage(
            new DiagnosticMessage("Initialising test environment..."));

        // Set environment variables, prime caches, etc.
        Environment.SetEnvironmentVariable("TEST_MODE", "true");

        await Task.CompletedTask;
    }
}

// Register in AssemblyInfo.cs:
// [assembly: TestPipelineStartup(typeof(CustomTestPipelineStartup))]

// =============================================================================
// 7. Culture override for multi-locale tests
// =============================================================================

public class CultureTestExamples
{
    [Fact]
    public void UsCurrencyFormat()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            var result = 123.45m.ToString("C");
            Assert.Equal("$123.45", result);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TraditionalChineseDateFormat()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var testDate = new DateTime(2024, 12, 31);

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("zh-TW");
            var result = testDate.ToString("yyyy/MM/dd");
            Assert.Equal("2024/12/31", result);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("en-US", "$123.45")]
    [InlineData("zh-TW", "NT$123.45")]
    [InlineData("ja-JP", "￥123")]
    public void MultiCultureCurrencyFormat(string cultureName, string expected)
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            var result = cultureName == "ja-JP"
                ? 123m.ToString("C")
                : 123.45m.ToString("C");
            Assert.Equal(expected, result);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
}

// =============================================================================
// 8. Improved diagnostics
// =============================================================================

public class DiagnosticsExamples
{
    private readonly ITestOutputHelper _output;

    public DiagnosticsExamples(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EmitsTimingInformation()
    {
        // xUnit 3.x captures more granular diagnostic output by default
        _output.WriteLine("Test starting");
        _output.WriteLine($"Run time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var startTime = DateTime.Now;

        var result = PerformCalculation(5, 3);

        var duration = DateTime.Now - startTime;
        _output.WriteLine($"Took: {duration.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Result: {result}");

        Assert.Equal(8, result);
    }

    private int PerformCalculation(int a, int b) => a + b;
}
