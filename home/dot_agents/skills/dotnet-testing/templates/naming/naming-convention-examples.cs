// =============================================================================
// Test naming convention examples
// Standard form: Method_Scenario_ExpectedBehavior (three-part naming)
// =============================================================================

using Xunit;

namespace TestNamingConventions.Examples;

// =============================================================================
// Test class naming: {ClassUnderTest}Tests
// =============================================================================

/// <summary>
/// Calculator tests — illustrates three-part naming for arithmetic.
/// </summary>
public class CalculatorTests
{
    // Happy-path test
    [Fact]
    public void Add_WhenGiven1And2_ShouldReturn3()
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(1, 2);

        // Assert
        Assert.Equal(3, result);
    }

    // Boundary-condition test
    [Fact]
    public void Add_WhenGiven0And0_ShouldReturn0()
    {
        var calculator = new Calculator();
        var result = calculator.Add(0, 0);
        Assert.Equal(0, result);
    }

    // Negative-number test
    [Fact]
    public void Add_WhenGivenNegativeAndPositive_ShouldReturnCorrectResult()
    {
        var calculator = new Calculator();
        var result = calculator.Add(-1, 3);
        Assert.Equal(2, result);
    }
}

/// <summary>
/// Email validator tests — illustrates naming for validation logic.
/// </summary>
public class EmailValidatorTests
{
    // Valid input
    [Fact]
    public void IsValidEmail_WhenEmailIsValid_ShouldReturnTrue()
    {
        var validator = new EmailValidator();
        var result = validator.IsValidEmail("user@example.com");
        Assert.True(result);
    }

    // Invalid input — null
    [Fact]
    public void IsValidEmail_WhenInputIsNull_ShouldReturnFalse()
    {
        var validator = new EmailValidator();
        var result = validator.IsValidEmail(null!);
        Assert.False(result);
    }

    // Invalid input — empty string
    [Fact]
    public void IsValidEmail_WhenInputIsEmpty_ShouldReturnFalse()
    {
        var validator = new EmailValidator();
        var result = validator.IsValidEmail("");
        Assert.False(result);
    }

    // Invalid format
    [Fact]
    public void IsValidEmail_WhenFormatIsInvalid_ShouldReturnFalse()
    {
        var validator = new EmailValidator();
        var result = validator.IsValidEmail("not-an-email");
        Assert.False(result);
    }
}

/// <summary>
/// Order service tests — illustrates business-logic and exception naming.
/// </summary>
public class OrderServiceTests
{
    // Happy-path flow
    [Fact]
    public void ProcessOrder_WhenOrderIsValid_ShouldReturnProcessedOrder()
    {
        // Arrange & Act & Assert
    }

    // Exception path
    [Fact]
    public void ProcessOrder_WhenInputIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
    }

    // Calculation logic
    [Fact]
    public void Calculate_WhenPriceIs100AndDiscountIs10Percent_ShouldReturn90()
    {
        // Arrange & Act & Assert
    }

    // State-transition exception
    [Fact]
    public void Cancel_WhenOrderIsAlreadyCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange & Act & Assert
    }
}

// =============================================================================
// ❌ Bad naming examples (do NOT imitate)
// =============================================================================

// ❌ public void TestAdd() { }              // No scenario or expected result
// ❌ public void Test1() { }                // Meaningless name
// ❌ public void EmailTest() { }            // No three-part structure
// ❌ public void OrderTest() { }            // Purpose is invisible

// =============================================================================
// Common scenario vocabulary reference
// =============================================================================
// Happy path     : Valid, Correct, Normal, Successful
// Boundary       : Min, Max, Empty, Zero
// Failure paths  : Null, Empty, InvalidFormat, OutOfRange
// State change   : FromXToY, InitialState, AlreadyCompleted, AlreadyCancelled
// Expected result: ShouldReturn, ShouldThrow, ShouldContain, ShouldBeEmpty, ShouldEqual
