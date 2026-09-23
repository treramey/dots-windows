using Xunit;

namespace MyProject.Tests;

/// <summary>
/// Parameterized test template — uses [Theory] with [InlineData] to exercise multiple cases.
/// </summary>
public class ParameterizedTestTemplate
{
    // -------------------------------------------------------------------------
    // [Theory] + [InlineData] basic template
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(100, -50, 50)]
    public void Add_WhenGivenVariousInputCombinations_ShouldReturnCorrectSum(int a, int b, int expected)
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // Testing multiple invalid inputs
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValid_WhenInputIsBlankOrNull_ShouldReturnFalse(string? input)
    {
        // Arrange
        var validator = new Validator();

        // Act
        var result = validator.IsValid(input);

        // Assert
        Assert.False(result);
    }

    // -------------------------------------------------------------------------
    // Testing multiple valid inputs
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.org")]
    [InlineData("admin@company.co.uk")]
    [InlineData("test123@test-domain.com")]
    public void IsValidEmail_WhenEmailFormatIsValid_ShouldReturnTrue(string validEmail)
    {
        // Arrange
        var emailHelper = new EmailHelper();

        // Act
        var result = emailHelper.IsValidEmail(validEmail);

        // Assert
        Assert.True(result);
    }

    // -------------------------------------------------------------------------
    // Testing input-to-output correspondence
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("test@gmail.com", "gmail.com")]
    [InlineData("admin@company.co.uk", "company.co.uk")]
    [InlineData("user@sub.domain.org", "sub.domain.org")]
    public void GetDomain_WhenEmailIsValid_ShouldReturnCorrespondingDomain(string email, string expectedDomain)
    {
        // Arrange
        var emailHelper = new EmailHelper();

        // Act
        var result = emailHelper.GetDomain(email);

        // Assert
        Assert.Equal(expectedDomain, result);
    }

    // -------------------------------------------------------------------------
    // Testing division
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(10, 2, 5)]
    [InlineData(15, 3, 5)]
    [InlineData(7, 2, 3.5)]
    [InlineData(-10, 2, -5)]
    public void Divide_WhenGivenValidValues_ShouldReturnCorrectQuotient(
        decimal dividend,
        decimal divisor,
        decimal expected)
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Divide(dividend, divisor);

        // Assert
        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // Testing setter with arbitrary values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(10)]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(999)]
    public void SetValue_WhenGivenArbitraryValue_ShouldStoreThatValue(int value)
    {
        // Arrange
        var counter = new Counter();

        // Act
        counter.SetValue(value);

        // Assert
        Assert.Equal(value, counter.Value);
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

    private class EmailHelper
    {
        public bool IsValidEmail(string email) =>
            !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Contains('.');

        public string? GetDomain(string email)
        {
            if (!IsValidEmail(email)) return null;
            return email.Split('@')[1];
        }
    }

    private class Counter
    {
        public int Value { get; private set; }
        public void SetValue(int value) => Value = value;
    }
}
