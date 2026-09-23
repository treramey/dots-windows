// TUnit execution control — [Retry], [Timeout], [DisplayName].
// Use [Retry] only for genuinely flaky external dependencies. Use
// [Timeout] to guard SLAs. Use [DisplayName] to phrase tests in
// business terms.

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Diagnostics;

namespace TUnit.Advanced.ExecutionControl.Examples;

public enum CustomerLevel
{
    Regular = 0,
    Vip = 1,
    Platinum = 2,
    Diamond = 3
}

// ===== Retry =====

public class RetryMechanismExamples
{
    /// <summary>Basic retry — up to three additional attempts on failure.</summary>
    [Test]
    [Retry(3)]
    [Property("Category", "Flaky")]
    public async Task NetworkCall_MayBeFlaky_RetryUntilSuccess()
    {
        var random = new Random();
        var success = random.Next(1, 4) == 1; // ~33% chance per attempt

        if (!success)
        {
            throw new HttpRequestException("Simulated network error");
        }

        await Assert.That(success).IsTrue();
    }

    /// <summary>External API call — wrap timeout-as-flake in HttpRequestException.</summary>
    [Test]
    [Retry(3)]
    [Property("Category", "ExternalDependency")]
    public async Task CallExternalApi_OnTransientFailure_ShouldEventuallySucceed()
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var response = await httpClient.GetAsync("https://jsonplaceholder.typicode.com/posts/1");
            await Assert.That(response.IsSuccessStatusCode).IsTrue();

            var content = await response.Content.ReadAsStringAsync();
            await Assert.That(content).IsNotNull();
        }
        catch (TaskCanceledException)
        {
            // Convert the timeout to a transient exception so retry kicks in.
            throw new HttpRequestException("Request timed out; will retry");
        }
    }

    /// <summary>
    /// Anti-pattern guard — do not retry tests that intentionally throw.
    /// </summary>
    [Test]
    public async Task Divide_ByZero_ShouldThrow()
    {
        await Assert.That(() => { var _ = 10 / int.Parse("0"); }).Throws<DivideByZeroException>();
    }
}

// ===== Timeout =====

public class TimeoutControlExamples
{
    /// <summary>Five-second guard for routine operations.</summary>
    [Test]
    [Timeout(5000)]
    [Property("Category", "Performance")]
    public async Task LongRunningOperation_ShouldFinishWithinFiveSeconds()
    {
        await Task.Delay(1000);
        await Assert.That(true).IsTrue();
    }

    /// <summary>Thirty-second guard for batch / integration work.</summary>
    [Test]
    [Timeout(30000)]
    [Property("Category", "Integration")]
    public async Task BatchProcessing_LargeWorkload_ShouldFinishInTime()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(ProcessDataBatch(i));
        }

        await Task.WhenAll(tasks);
        await Assert.That(tasks.All(t => t.IsCompletedSuccessfully)).IsTrue();
    }

    private static async Task ProcessDataBatch(int batchNumber)
    {
        await Task.Delay(50);
    }

    /// <summary>Performance baseline — combine Timeout with Stopwatch.</summary>
    [Test]
    [Timeout(1000)]
    [Property("Category", "Performance")]
    [Property("Baseline", "true")]
    public async Task Search_ShouldMeetSlaWithinFiveHundredMs()
    {
        var stopwatch = Stopwatch.StartNew();

        var results = await PerformSearch("test query");

        stopwatch.Stop();

        await Assert.That(results).IsNotNull();
        await Assert.That(results.Count()).IsGreaterThan(0);
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(500);
    }

    private static async Task<IEnumerable<string>> PerformSearch(string query)
    {
        await Task.Delay(100);
        return new[] { "result1", "result2", "result3" };
    }
}

// ===== DisplayName =====

public class DisplayNameExamples
{
    /// <summary>Static display name.</summary>
    [Test]
    [DisplayName("Verify the user registration flow")]
    public async Task UserRegistration_ShouldPassValidEmail()
    {
        await Assert.That("user@example.com").Contains("@");
    }

    /// <summary>
    /// Display name with placeholder interpolation for parameterized tests.
    /// </summary>
    [Test]
    [Arguments("valid@email.com", true)]
    [Arguments("invalid-email", false)]
    [Arguments("", false)]
    [Arguments("test@domain.co.uk", true)]
    [DisplayName("Email validation: {0} expected {1}")]
    public async Task EmailValidation_WithVariousInputs_ShouldMatchExpected(string email, bool expectedValid)
    {
        var isValid = !string.IsNullOrEmpty(email) && email.Contains("@") && email.Contains(".");
        await Assert.That(isValid).IsEqualTo(expectedValid);
    }

    /// <summary>
    /// Business-language display name — reads like a requirement, not a
    /// method name.
    /// </summary>
    [Test]
    [Arguments(CustomerLevel.Regular, 1000, 0)]
    [Arguments(CustomerLevel.Vip, 1000, 50)]
    [Arguments(CustomerLevel.Platinum, 1000, 100)]
    [Arguments(CustomerLevel.Diamond, 1000, 200)]
    [DisplayName("Member tier {0} buying ${1} should receive ${2} discount")]
    public async Task MemberDiscount_PerTier_ShouldComputeExpectedAmount(
        CustomerLevel level, decimal amount, decimal expectedDiscount)
    {
        var discount = CalculateDiscount(amount, level);
        await Assert.That(discount).IsEqualTo(expectedDiscount);
    }

    private static decimal CalculateDiscount(decimal amount, CustomerLevel level) => level switch
    {
        CustomerLevel.Diamond => amount * 0.2m,
        CustomerLevel.Platinum => amount * 0.1m,
        CustomerLevel.Vip => amount * 0.05m,
        _ => 0m
    };
}

// ===== Combined: retry + timeout + display name =====

public class CombinedExecutionControlExamples
{
    [Test]
    [Retry(2)]
    [Timeout(5000)]
    [Property("Category", "Integration")]
    [DisplayName("External API integration: health check")]
    public async Task ExternalApi_HealthCheck_ShouldRespondSuccessfully()
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(3);

        try
        {
            var response = await httpClient.GetAsync("https://jsonplaceholder.typicode.com/posts/1");
            await Assert.That(response.IsSuccessStatusCode).IsTrue();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new HttpRequestException($"API health check failed: {ex.Message}");
        }
    }

    [Test]
    [Timeout(10000)]
    [Property("Category", "Performance")]
    [Property("Priority", "High")]
    [DisplayName("Performance baseline: batch processing under 10s")]
    public async Task BatchProcessing_ShouldStayUnderTenSeconds()
    {
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, 50)
            .Select(async i =>
            {
                await Task.Delay(Random.Shared.Next(10, 50));
                return i;
            });

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        await Assert.That(results.Length).IsEqualTo(50);
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(5000);
    }
}
