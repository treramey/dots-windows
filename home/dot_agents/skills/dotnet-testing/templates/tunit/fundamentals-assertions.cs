// TUnit assertion examples — equality, booleans, numerics, strings,
// collections, exceptions, And/Or composition. The fluent API is the
// idiomatic TUnit style; AwesomeAssertions still works alongside it.

namespace MyApp.Tests;

public class AssertionExamplesTests
{
    // ----- equality -----

    [Test]
    public async Task Equality_BasicValueComparison()
    {
        var expected = 42;
        var actual = 40 + 2;
        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(actual).IsNotEqualTo(43);

        // Reference equality.
        var obj1 = new object();
        var obj2 = obj1;
        await Assert.That(obj2).IsEqualTo(obj1);
    }

    [Test]
    public async Task Equality_NullChecks()
    {
        string? nullValue = null;
        string notNullValue = "test";

        await Assert.That(nullValue).IsNull();
        await Assert.That(notNullValue).IsNotNull();
    }

    // ----- booleans -----

    [Test]
    public async Task Booleans_BasicChecks()
    {
        await Assert.That(1 + 1 == 2).IsTrue();
        await Assert.That(1 + 1 == 3).IsFalse();

        var number = 10;
        await Assert.That(number > 5).IsTrue();
        await Assert.That(number < 5).IsFalse();
    }

    // ----- numerics -----

    [Test]
    public async Task Numerics_Comparison()
    {
        var actualValue = 5 + 5;
        var compareValue = 3 + 2;
        var equalValue = 4 + 6;

        await Assert.That(actualValue).IsGreaterThan(compareValue);
        await Assert.That(actualValue).IsGreaterThanOrEqualTo(equalValue);
        await Assert.That(compareValue).IsLessThan(actualValue);
        await Assert.That(compareValue).IsLessThanOrEqualTo(compareValue);
        await Assert.That(actualValue).IsBetween(5, 15);
    }

    [Test]
    [Arguments(3.14159, 3.14, 0.01)]
    [Arguments(1.0001, 1.0, 0.001)]
    public async Task Numerics_FloatToleranceWithin(double actual, double expected, double tolerance)
    {
        await Assert.That(actual).IsEqualTo(expected).Within(tolerance);
    }

    // ----- strings -----

    [Test]
    public async Task Strings_BasicChecks()
    {
        var email = "user@example.com";

        await Assert.That(email).Contains("@");
        await Assert.That(email).Contains("example");
        await Assert.That(email).DoesNotContain(" ");
        await Assert.That(email).StartsWith("user");
        await Assert.That(email).EndsWith(".com");
        await Assert.That("").IsEmpty();
        await Assert.That(email).IsNotEmpty();
    }

    // ----- collections -----

    [Test]
    public async Task Collections_BasicChecks()
    {
        var numbers = new List<int> { 1, 2, 3, 4, 5 };
        var emptyList = new List<string>();

        await Assert.That(numbers).HasCount(5);
        await Assert.That(emptyList).IsEmpty();
        await Assert.That(numbers).IsNotEmpty();
        await Assert.That(numbers).Contains(3);
        await Assert.That(numbers).DoesNotContain(10);
        await Assert.That(numbers.First()).IsEqualTo(1);
        await Assert.That(numbers.Last()).IsEqualTo(5);
        await Assert.That(numbers.All(x => x > 0)).IsTrue();
        await Assert.That(numbers.Any(x => x > 3)).IsTrue();
    }

    // ----- exceptions -----

    [Test]
    public async Task Exceptions_TypeMessageAndDoesNotThrow()
    {
        var calculator = new Calculator();

        // Type only.
        await Assert.That(() => calculator.Divide(10, 0))
            .Throws<DivideByZeroException>();

        // Type and message.
        await Assert.That(() => calculator.Divide(10, 0))
            .Throws<DivideByZeroException>()
            .WithMessage("Divisor cannot be zero");

        // Negative assertion.
        await Assert.That(() => calculator.Add(1, 2))
            .DoesNotThrow();
    }

    // ----- And / Or composition -----

    [Test]
    public async Task Composition_AndAllConditionsMustHold()
    {
        var number = 10;

        await Assert.That(number)
            .IsGreaterThan(5)
            .And.IsLessThan(15)
            .And.IsEqualTo(10);

        var email = "test@example.com";
        await Assert.That(email)
            .Contains("@")
            .And.EndsWith(".com")
            .And.StartsWith("test");
    }

    [Test]
    public async Task Composition_OrAnyConditionMatches()
    {
        var number = 15;

        await Assert.That(number)
            .IsEqualTo(10)
            .Or.IsEqualTo(15)
            .Or.IsEqualTo(20);

        // HTTP success status codes.
        var httpStatusCode = 200;
        await Assert.That(httpStatusCode)
            .IsEqualTo(200)
            .Or.IsEqualTo(201)
            .Or.IsEqualTo(204);
    }
}
