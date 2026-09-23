// TUnit basic test template — [Test], async assertions, [Arguments].
// All test methods are async Task; that is a framework requirement, not
// a style choice. Assertions are awaited.

namespace MyApp.Tests;

/// <summary>
/// TUnit basic test examples — Calculator under test.
/// </summary>
public class CalculatorTests
{
    private readonly Calculator _calculator;

    public CalculatorTests()
    {
        _calculator = new Calculator();
    }

    // ----- basic test -----

    /// <summary>Basic [Test] — note the async Task signature.</summary>
    [Test]
    public async Task Add_When1And2_ShouldReturn3()
    {
        // Arrange
        int a = 1;
        int b = 2;
        int expected = 3;

        // Act
        var result = _calculator.Add(a, b);

        // Assert — fluent assertions are awaited.
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>Exception assertion.</summary>
    [Test]
    public async Task Divide_When0AsDivisor_ShouldThrowDivideByZeroException()
    {
        int dividend = 10;
        int divisor = 0;

        await Assert.That(() => _calculator.Divide(dividend, divisor))
            .Throws<DivideByZeroException>();
    }

    /// <summary>Exception message check.</summary>
    [Test]
    public async Task Divide_When0AsDivisor_ShouldIncludeExpectedMessage()
    {
        await Assert.That(() => _calculator.Divide(10, 0))
            .Throws<DivideByZeroException>()
            .WithMessage("Divisor cannot be zero");
    }

    // ----- parameterized test -----

    /// <summary>
    /// Parameterized test — TUnit uses [Arguments] in place of xUnit's
    /// [InlineData]. Each row produces one independent test.
    /// </summary>
    [Test]
    [Arguments(1, 2, 3)]
    [Arguments(-1, 1, 0)]
    [Arguments(0, 0, 0)]
    [Arguments(100, -50, 50)]
    [Arguments(int.MaxValue, 0, int.MaxValue)]
    public async Task Add_WithMultipleInputs_ShouldReturnExpectedSum(int a, int b, int expected)
    {
        var result = _calculator.Add(a, b);
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>Boolean parameterized test.</summary>
    [Test]
    [Arguments(1, true)]
    [Arguments(-1, false)]
    [Arguments(0, false)]
    [Arguments(100, true)]
    public async Task IsPositive_WithVariousValues_ShouldReturnExpectedFlag(int number, bool expected)
    {
        var result = _calculator.IsPositive(number);
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>Float comparison with tolerance via .Within().</summary>
    [Test]
    [Arguments(3.14159, 3.14, 0.01)]
    [Arguments(1.0001, 1.0, 0.001)]
    [Arguments(99.999, 100.0, 0.01)]
    public async Task FloatComparison_WithTolerance_ShouldMatchWithinDelta(double actual, double expected, double tolerance)
    {
        await Assert.That(actual)
            .IsEqualTo(expected)
            .Within(tolerance);
    }
}

/// <summary>Subject under test.</summary>
public class Calculator
{
    public int Add(int a, int b) => a + b;

    public double Divide(int dividend, int divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("Divisor cannot be zero");
        }
        return (double)dividend / divisor;
    }

    public bool IsPositive(int number) => number > 0;
}
