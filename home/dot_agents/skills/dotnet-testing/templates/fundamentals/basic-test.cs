using Xunit;

namespace MyProject.Tests;

/// <summary>
/// Basic unit test template — follows FIRST principles and the 3A pattern.
/// </summary>
public class BasicTestTemplate
{
    // -------------------------------------------------------------------------
    // [Fact] single test case template
    // -------------------------------------------------------------------------

    [Fact]
    public void MethodName_WhenScenarioOccurs_ShouldExhibitExpectedBehavior()
    {
        // Arrange - prepare test data and dependencies
        // var sut = new SystemUnderTest();
        // const int input = 1;
        // const int expected = 2;

        // Act - invoke the method under test
        // var result = sut.Method(input);

        // Assert - verify the outcome
        // result.Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // Happy-path test template
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_WhenGivenTwoPositiveIntegers_ShouldReturnSum()
    {
        // Arrange
        var calculator = new Calculator();
        const int a = 1;
        const int b = 2;
        const int expected = 3;

        // Act
        var result = calculator.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // Boundary-condition test template
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_WhenOneOperandIsZero_ShouldReturnTheOther()
    {
        // Arrange
        var calculator = new Calculator();
        const int a = 0;
        const int b = 5;
        const int expected = 5;

        // Act
        var result = calculator.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // Invalid-input test template
    // -------------------------------------------------------------------------

    [Fact]
    public void IsValid_WhenInputIsNull_ShouldReturnFalse()
    {
        // Arrange
        var validator = new Validator();
        string? input = null;

        // Act
        var result = validator.IsValid(input);

        // Assert
        Assert.False(result);
    }

    // -------------------------------------------------------------------------
    // Exception test template
    // -------------------------------------------------------------------------

    [Fact]
    public void Divide_WhenDivisorIsZero_ShouldThrowDivideByZeroException()
    {
        // Arrange
        var calculator = new Calculator();
        const decimal dividend = 10m;
        const decimal divisor = 0m;

        // Act & Assert
        var exception = Assert.Throws<DivideByZeroException>(
            () => calculator.Divide(dividend, divisor)
        );

        Assert.Equal("Divisor cannot be zero", exception.Message);
    }

    // -------------------------------------------------------------------------
    // Sample classes (for template illustration only)
    // -------------------------------------------------------------------------

    private class Calculator
    {
        public int Add(int a, int b) => a + b;

        public decimal Divide(decimal dividend, decimal divisor)
        {
            if (divisor == 0)
                throw new DivideByZeroException("Divisor cannot be zero");
            return dividend / divisor;
        }
    }

    private class Validator
    {
        public bool IsValid(string? input) => !string.IsNullOrWhiteSpace(input);
    }
}
