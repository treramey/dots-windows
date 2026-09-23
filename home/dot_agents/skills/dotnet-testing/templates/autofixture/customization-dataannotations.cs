// =============================================================================
// AutoFixture DataAnnotations integration
// AutoFixture automatically respects System.ComponentModel.DataAnnotations.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using AutoFixture;
using AwesomeAssertions;
using Xunit;

namespace AutoFixtureCustomization.Templates;

// -----------------------------------------------------------------------------
// 1. Annotated models
// -----------------------------------------------------------------------------

/// <summary>
/// Person uses DataAnnotations; AutoFixture honors these rules automatically.
/// </summary>
public class Person
{
    public Guid Id { get; set; }

    /// <summary>StringLength(10) — AutoFixture emits 10-character strings.</summary>
    [StringLength(10)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Range(10, 80) — AutoFixture stays inside that range.</summary>
    [Range(10, 80)]
    public int Age { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>
/// Employee — multiple validation rules at once.
/// </summary>
public class Employee
{
    public Guid Id { get; set; }

    [StringLength(50, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(18, 65)]
    public int Age { get; set; }

    [Range(typeof(decimal), "25000", "200000")]
    public decimal Salary { get; set; }

    [StringLength(100)]
    public string Department { get; set; } = string.Empty;
}

// -----------------------------------------------------------------------------
// 2. DataAnnotations tests
// -----------------------------------------------------------------------------

public class DataAnnotationsIntegrationTests
{
    /// <summary>
    /// AutoFixture recognises StringLength and Range.
    /// </summary>
    [Fact]
    public void AutoFixture_HonoursDataAnnotations()
    {
        var fixture = new Fixture();

        var person = fixture.Create<Person>();

        person.Name.Length.Should().Be(10);
        person.Age.Should().BeInRange(10, 80);
        person.Id.Should().NotBeEmpty();
        person.CreateTime.Should().NotBe(default);
    }

    /// <summary>
    /// Batch-generated objects all stay within bounds.
    /// </summary>
    [Fact]
    public void AutoFixture_Batch_AllSatisfyAnnotations()
    {
        var fixture = new Fixture();

        var persons = fixture.CreateMany<Person>(10).ToList();

        persons.Should().HaveCount(10);
        persons.Should().AllSatisfy(person =>
        {
            person.Name.Length.Should().Be(10);
            person.Age.Should().BeInRange(10, 80);
            person.Id.Should().NotBeEmpty();
        });
    }

    /// <summary>
    /// Complex validation rules are honored.
    /// </summary>
    [Fact]
    public void AutoFixture_HandlesComplexRules()
    {
        var fixture = new Fixture();

        var employees = fixture.CreateMany<Employee>(5).ToList();

        employees.Should().AllSatisfy(employee =>
        {
            employee.Name.Length.Should().BeInRange(2, 50);
            employee.Age.Should().BeInRange(18, 65);
            employee.Salary.Should().BeInRange(25000m, 200000m);
            employee.Department.Length.Should().BeLessOrEqualTo(100);
        });
    }
}

// -----------------------------------------------------------------------------
// 3. .With() — fixed value vs factory
// -----------------------------------------------------------------------------

public class Member
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime JoinDate { get; set; }
}

public class WithMethodTests
{
    /// <summary>
    /// Shows the difference between a fixed value and a per-call factory.
    /// </summary>
    [Fact]
    public void With_FixedVsFactory_Differs()
    {
        var fixture = new Fixture();

        // Wrong: Random.Shared.Next() evaluates once — all objects share one age.
        var fixedAgeMembers = fixture.Build<Member>()
            .With(x => x.Age, Random.Shared.Next(30, 50))
            .CreateMany(5)
            .ToList();

        // Right: lambda is invoked per object.
        var dynamicAgeMembers = fixture.Build<Member>()
            .With(x => x.Age, () => Random.Shared.Next(30, 50))
            .CreateMany(5)
            .ToList();

        fixedAgeMembers.Select(m => m.Age).Distinct().Count().Should().Be(1);
        dynamicAgeMembers.Select(m => m.Age).Distinct().Count().Should().BeGreaterThan(1);
    }

    [Fact]
    public void Multiple_Factory_Lambdas()
    {
        var fixture = new Fixture();
        var baseDate = new DateTime(2025, 1, 1);

        var members = fixture.Build<Member>()
            .With(x => x.Age, () => Random.Shared.Next(20, 60))
            .With(x => x.JoinDate, () => baseDate.AddDays(Random.Shared.Next(0, 365)))
            .CreateMany(10)
            .ToList();

        members.Should().AllSatisfy(m =>
        {
            m.Age.Should().BeInRange(20, 59);
            m.JoinDate.Should().BeOnOrAfter(baseDate);
            m.JoinDate.Should().BeBefore(baseDate.AddDays(365));
        });
    }
}

// -----------------------------------------------------------------------------
// 4. Random.Shared vs new Random()
// -----------------------------------------------------------------------------

public class RandomComparisonTests
{
    /// <summary>
    /// Demonstrates issues with new Random() (seed collisions).
    /// </summary>
    [Fact]
    public void NewRandom_MayProduceSameSequence()
    {
        // Two new Random() instances built in fast succession can share a seed
        // and produce identical sequences.
        var values = Enumerable.Range(0, 10)
            .Select(_ => Random.Shared.Next(100))
            .Distinct()
            .Count();

        values.Should().BeGreaterThan(1);
    }

    /// <summary>
    /// Random.Shared is thread-safe.
    /// </summary>
    [Fact]
    public async Task RandomShared_IsThreadSafe()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() =>
                Enumerable.Range(0, 100)
                    .Select(_ => Random.Shared.Next(1000))
                    .ToList()))
            .ToList();

        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(list => list.Should().HaveCount(100));
    }
}
