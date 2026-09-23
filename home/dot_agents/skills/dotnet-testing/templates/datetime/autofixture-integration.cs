// =============================================================================
// AutoFixture + TimeProvider integration — automated time-dependent tests.
// =============================================================================

using System;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;
using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace TimeProviderExamples.Tests;

#region FakeTimeProvider customization

/// <summary>
/// AutoFixture customization for FakeTimeProvider.
/// </summary>
public class FakeTimeProviderCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        // Register how AutoFixture should build a FakeTimeProvider.
        fixture.Register(() => new FakeTimeProvider());
    }
}

#endregion

#region AutoDataWithCustomization attribute

/// <summary>
/// AutoData attribute combining NSubstitute and FakeTimeProvider customizations.
/// </summary>
public class AutoDataWithCustomizationAttribute : AutoDataAttribute
{
    public AutoDataWithCustomizationAttribute() : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture()
    {
        return new Fixture()
            .Customize(new AutoNSubstituteCustomization())
            .Customize(new FakeTimeProviderCustomization());
    }
}

/// <summary>
/// InlineAutoData variant — combines InlineData with the customizations above.
/// </summary>
public class InlineAutoDataWithCustomizationAttribute : InlineAutoDataAttribute
{
    public InlineAutoDataWithCustomizationAttribute(params object[] values)
        : base(new AutoDataWithCustomizationAttribute(), values)
    {
    }
}

#endregion

#region Traditional vs AutoFixture comparison

/// <summary>
/// Traditional approach — every test wires the dependency by hand.
/// </summary>
public class OrderServiceTraditionalTests
{
    [Fact]
    public void CanPlaceOrder_WhenInsideBusinessHours_TraditionalStyle()
    {
        // Arrange — every test builds these by hand
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 14, 0, 0));

        var orderService = new OrderService(fakeTimeProvider);

        // Act
        var result = orderService.CanPlaceOrder();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetTimeBasedDiscount_OnFriday_TraditionalStyle()
    {
        // Arrange — same boilerplate again
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 14, 0, 0)); // Friday

        var orderService = new OrderService(fakeTimeProvider);

        // Act
        var discount = orderService.GetTimeBasedDiscount();

        // Assert
        discount.Should().Be("Happy Friday: 10% off");
    }
}

/// <summary>
/// AutoFixture style — boilerplate moved to attributes.
/// </summary>
public class OrderServiceAutoFixtureTests
{
    /// <summary>
    /// [Frozen(Matching.DirectBaseType)] is the key — see the long comment below.
    /// </summary>
    [Theory]
    [AutoDataWithCustomization]
    public void GetTimeBasedDiscount_OnMonday_ShouldReturnNoDiscount(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        OrderService sut) // sut = system under test, built by AutoFixture
    {
        // Arrange — only the test-specific timing detail
        var mondayTime = new DateTime(2024, 3, 11, 14, 0, 0); // Monday
        fakeTimeProvider.SetLocalNow(mondayTime);

        // Act
        var discount = sut.GetTimeBasedDiscount();

        // Assert
        discount.Should().Be("No discount");
    }

    [Theory]
    [AutoDataWithCustomization]
    public void GetTimeBasedDiscount_OnFriday_ShouldReturnTenPercentOff(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        OrderService sut)
    {
        // Arrange — Friday
        var fridayTime = new DateTime(2024, 3, 15, 14, 0, 0); // Friday
        fakeTimeProvider.SetLocalNow(fridayTime);

        // Act
        var discount = sut.GetTimeBasedDiscount();

        // Assert
        discount.Should().Be("Happy Friday: 10% off");
    }

    [Theory]
    [AutoDataWithCustomization]
    public void GetTimeBasedDiscount_OnChristmas_ShouldReturnTwentyPercentOff(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        OrderService sut)
    {
        // Arrange
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 12, 25, 10, 0, 0));

        // Act
        var discount = sut.GetTimeBasedDiscount();

        // Assert
        discount.Should().Be("Christmas special: 20% off");
    }

    [Theory]
    [AutoDataWithCustomization]
    public void CanPlaceOrder_AtBoundary9AM_ShouldReturnTrue(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        OrderService sut)
    {
        // Arrange — 9 AM exactly (open of business hours)
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 9, 0, 0));

        // Act & Assert
        sut.CanPlaceOrder().Should().BeTrue();
    }

    [Theory]
    [AutoDataWithCustomization]
    public void CanPlaceOrder_AtBoundary5PM_ShouldReturnFalse(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        OrderService sut)
    {
        // Arrange — 5 PM exactly (close of business hours)
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 17, 0, 0));

        // Act & Assert
        sut.CanPlaceOrder().Should().BeFalse();
    }
}

#endregion

#region Why Matching.DirectBaseType matters

/*
 * ============================================================
 * Matching.DirectBaseType — the explainer
 * ============================================================
 *
 * Problem:
 *   OrderService's constructor takes TimeProvider (the abstract base).
 *   The test wants to use FakeTimeProvider (the derived testing impl).
 *
 * Without Matching.DirectBaseType:
 *
 *     [Theory]
 *     [AutoDataWithCustomization]
 *     public void Test([Frozen] FakeTimeProvider provider, OrderService sut)
 *     {
 *         // FAILS — AutoFixture only freezes FakeTimeProvider.
 *         // When it builds OrderService it sees a TimeProvider parameter
 *         // and creates a different provider — so provider and sut don't
 *         // share state, and SetLocalNow has no effect.
 *     }
 *
 * With Matching.DirectBaseType:
 *
 *     [Theory]
 *     [AutoDataWithCustomization]
 *     public void Test([Frozen(Matching.DirectBaseType)] FakeTimeProvider provider, OrderService sut)
 *     {
 *         // PASSES — AutoFixture registers FakeTimeProvider as TimeProvider too.
 *         // Building OrderService now reuses the same FakeTimeProvider instance.
 *     }
 *
 * Resolution flow:
 *   1. AutoFixture needs to build OrderService.
 *   2. OrderService's constructor takes TimeProvider.
 *   3. AutoFixture checks for [Frozen] instances that satisfy this need.
 *   4. Finds [Frozen(Matching.DirectBaseType)] FakeTimeProvider.
 *   5. Confirms TimeProvider is the direct base of FakeTimeProvider.
 *   6. Injects the same FakeTimeProvider instance into OrderService.
 */

#endregion

#region Cache tests with AutoFixture

/// <summary>
/// Cache tests — AutoFixture auto-generates key/value pairs.
/// </summary>
public class TimedCacheAutoFixtureTests
{
    [Theory]
    [AutoDataWithCustomization]
    public void TimedCache_WithAutoFixture_ShouldHandleExpiration(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        string key,    // auto-generated
        string value)  // auto-generated
    {
        // Arrange
        var startTime = new DateTime(2024, 3, 15, 10, 0, 0);
        fakeTimeProvider.SetLocalNow(startTime);

        var cache = new TimedCache<string>(fakeTimeProvider, TimeSpan.FromMinutes(30));

        // Act & Assert — set + immediate fetch
        cache.Set(key, value);
        cache.Get(key).Should().Be(value);

        // Fast-forward past expiration
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(31));
        cache.Get(key).Should().BeNull();
    }

    [Theory]
    [AutoDataWithCustomization]
    public void TimedCache_MultipleItems_ShouldExpireIndependently(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        string key1, string value1,
        string key2, string value2,
        string key3, string value3)
    {
        // Arrange
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 10, 0, 0));
        var cache = new TimedCache<string>(fakeTimeProvider, TimeSpan.FromMinutes(10));

        // Act — set entries at different times
        cache.Set(key1, value1);
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(3));
        cache.Set(key2, value2);
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(3));
        cache.Set(key3, value3);

        // Advance another 5 minutes — key1 has been alive 11 min, key2 8 min, key3 5 min
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(5));

        cache.Get(key1).Should().BeNull();   // 11 > 10 — expired
        cache.Get(key2).Should().Be(value2); // 8 < 10 — still valid
        cache.Get(key3).Should().Be(value3); // 5 < 10 — still valid
    }
}

#endregion

#region InlineAutoData + parameterized tests

/// <summary>
/// InlineAutoDataWithCustomization combines InlineData rows with AutoFixture-produced
/// dependencies in the same theory.
/// </summary>
public class OrderServiceInlineAutoDataTests
{
    [Theory]
    [InlineAutoDataWithCustomization(8, false)]   // 8 AM — before open
    [InlineAutoDataWithCustomization(9, true)]    // 9 AM — open (boundary)
    [InlineAutoDataWithCustomization(12, true)]   // Noon
    [InlineAutoDataWithCustomization(16, true)]   // 4 PM
    [InlineAutoDataWithCustomization(17, false)]  // 5 PM — closed (boundary)
    [InlineAutoDataWithCustomization(18, false)]  // 6 PM — after hours
    public void CanPlaceOrder_AtVariousHours_AutoFixtureVersion(
        int hour,      // from InlineData
        bool expected, // from InlineData
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        OrderService sut) // from AutoFixture
    {
        // Arrange
        fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, hour, 0, 0));

        // Act
        var result = sut.CanPlaceOrder();

        // Assert
        result.Should().Be(expected);
    }
}

#endregion

#region Schedule service AutoFixture tests

/// <summary>
/// Schedule service tests using AutoFixture.
/// </summary>
public class ScheduleServiceAutoFixtureTests
{
    [Theory]
    [AutoDataWithCustomization]
    public void ShouldExecuteJob_WhenPastDue_ShouldReturnTrue(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        ScheduleService sut)
    {
        // Arrange
        var now = new DateTime(2024, 3, 15, 14, 30, 0);
        fakeTimeProvider.SetLocalNow(now);

        var schedule = new JobSchedule
        {
            NextExecutionTime = now.AddMinutes(-30), // 30 min ago — past due
            CronExpression = "0 0 * * *"
        };

        // Act
        var result = sut.ShouldExecuteJob(schedule);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [AutoDataWithCustomization]
    public void ShouldExecuteJob_WhenNotYetDue_ShouldReturnFalse(
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        ScheduleService sut)
    {
        // Arrange
        var now = new DateTime(2024, 3, 15, 14, 30, 0);
        fakeTimeProvider.SetLocalNow(now);

        var schedule = new JobSchedule
        {
            NextExecutionTime = now.AddMinutes(30), // 30 min in the future
            CronExpression = "0 0 * * *"
        };

        // Act
        var result = sut.ShouldExecuteJob(schedule);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineAutoDataWithCustomization("2024-03-15 14:30:00", "2024-03-15 14:00:00", true)]
    [InlineAutoDataWithCustomization("2024-03-15 13:30:00", "2024-03-15 14:00:00", false)]
    [InlineAutoDataWithCustomization("2024-03-15 14:00:00", "2024-03-15 14:00:00", true)]
    public void ShouldExecuteJob_Parameterized(
        string currentTimeStr,
        string scheduledTimeStr,
        bool expected,
        [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
        ScheduleService sut)
    {
        // Arrange
        fakeTimeProvider.SetLocalNow(DateTime.Parse(currentTimeStr));

        var schedule = new JobSchedule
        {
            NextExecutionTime = DateTime.Parse(scheduledTimeStr)
        };

        // Act
        var result = sut.ShouldExecuteJob(schedule);

        // Assert
        result.Should().Be(expected);
    }
}

#endregion
