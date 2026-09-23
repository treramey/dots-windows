using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// ITestOutputHelper usage examples — shows how to use ITestOutputHelper
/// correctly for diagnostic output in xUnit tests.
/// </summary>
public class ITestOutputHelperExample
{
    private readonly ITestOutputHelper _output;

    // Correct injection: via the constructor.
    public ITestOutputHelperExample(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public void BasicOutputExample_ShouldEmitReadableLines()
    {
        // Arrange
        var productName = "Laptop";
        var price = 30000;

        // Act
        _output.WriteLine("=== Test start ===");
        _output.WriteLine($"Product: {productName}");
        _output.WriteLine($"Price: ${price:N0}");

        var discountedPrice = price * 0.9m;
        _output.WriteLine($"Discounted price: ${discountedPrice:N0}");

        // Assert
        _output.WriteLine("=== Test complete ===");
        Assert.True(discountedPrice < price);
    }

    [Fact]
    public void StructuredOutputExample_ShouldEmitSectionedOutput()
    {
        // Arrange
        LogSection("Setup");
        var customer = new { Name = "Jane Doe", Level = "VIP" };
        LogKeyValue("Customer name", customer.Name);
        LogKeyValue("Member level", customer.Level);

        // Act
        LogSection("Execute");
        var discount = CalculateDiscount(customer.Level);
        LogKeyValue("Discount", $"{discount}%");

        // Assert
        LogSection("Verify");
        Assert.Equal(10, discount);
        _output.WriteLine("Discount calculation passed.");
    }

    [Fact]
    public async Task PerformanceTestExample_ShouldEmitCheckpointTimings()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        _output.WriteLine("=== Performance test start ===");
        _output.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

        // Act - Stage 1
        await Task.Delay(100); // Simulate data load
        var loadTime = stopwatch.Elapsed;
        _output.WriteLine($"Data load complete: {loadTime.TotalMilliseconds:F2} ms");

        // Act - Stage 2
        await Task.Delay(50); // Simulate processing
        var processTime = stopwatch.Elapsed;
        _output.WriteLine($"Data processing complete: {processTime.TotalMilliseconds:F2} ms");

        stopwatch.Stop();

        // Assert & report
        _output.WriteLine("\n=== Performance report ===");
        _output.WriteLine($"Total elapsed: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Within budget (< 200ms): {stopwatch.Elapsed.TotalMilliseconds < 200}");

        Assert.True(stopwatch.Elapsed.TotalMilliseconds < 200);
    }

    // Helpers — structured output
    private void LogSection(string title)
    {
        _output.WriteLine($"\n=== {title} ===");
    }

    private void LogKeyValue(string key, object value)
    {
        _output.WriteLine($"{key}: {value}");
    }

    private int CalculateDiscount(string customerLevel)
    {
        return customerLevel == "VIP" ? 10 : 0;
    }
}

// Anti-patterns — do not copy.
public class WrongITestOutputHelperUsage
{
    // Wrong #1: static field
    private static ITestOutputHelper _staticOutput; // do not do this

    public WrongITestOutputHelperUsage(ITestOutputHelper output)
    {
        _staticOutput = output;
    }

    // Wrong #2: accessing from a static helper
    public static void StaticHelper()
    {
        // _staticOutput.WriteLine("This will throw");
    }

    // Wrong #3: writing from Dispose
    public void Dispose()
    {
        // _staticOutput?.WriteLine("Test cleanup"); // output is discarded
    }
}
