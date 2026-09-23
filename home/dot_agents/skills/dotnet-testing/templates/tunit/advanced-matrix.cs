// TUnit MatrixDataSource — auto-generated combinations of parameter
// values. Watch case-count growth: 4 x 4 = 16 is fine, 5 x 4 x 3 x 6 =
// 360 is not. Enums must be passed as numeric values due to C# attribute
// constant rules.

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace TUnit.Advanced.Matrix.Examples;

public enum CustomerLevel
{
    Regular = 0,
    Vip = 1,
    Platinum = 2,
    Diamond = 3
}

public class Order
{
    public CustomerLevel CustomerLevel { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public decimal SubTotal => Items.Sum(i => i.UnitPrice * i.Quantity);
}

public class OrderItem
{
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Basic matrix combinations.</summary>
public class MatrixTestsBasicExamples
{
    /// <summary>
    /// 4 levels x 4 amounts = 16 cases. Enum is passed as int and TUnit
    /// converts it back to the enum type on the parameter.
    /// </summary>
    [Test]
    [MatrixDataSource]
    public async Task CalculateShipping_PerCustomerLevelAndAmount_ShouldFollowRules(
        [Matrix(0, 1, 2, 3)] CustomerLevel customerLevel,
        [Matrix(100, 500, 1000, 2000)] decimal orderAmount)
    {
        var order = new Order
        {
            CustomerLevel = customerLevel,
            Items = [new OrderItem { UnitPrice = orderAmount, Quantity = 1 }]
        };

        var shippingFee = CalculateShippingFee(order);
        var isFreeShipping = IsEligibleForFreeShipping(order);

        if (isFreeShipping)
        {
            await Assert.That(shippingFee).IsEqualTo(0m);
        }
        else
        {
            await Assert.That(shippingFee).IsGreaterThan(0m);
        }

        switch (customerLevel)
        {
            case CustomerLevel.Diamond:
                await Assert.That(shippingFee).IsEqualTo(0m); // diamond always free
                break;
            case CustomerLevel.Vip or CustomerLevel.Platinum:
                if (orderAmount < 1000m)
                {
                    await Assert.That(shippingFee).IsEqualTo(40m); // VIP+ half shipping
                }
                break;
            case CustomerLevel.Regular:
                if (orderAmount < 1000m)
                {
                    await Assert.That(shippingFee).IsEqualTo(80m);
                }
                break;
        }
    }

    /// <summary>2 x 4 = 8 cases. Focuses on the key boundaries.</summary>
    [Test]
    [MatrixDataSource]
    public async Task DiscountLogic_PerMembershipAndAmount_ShouldFollowRules(
        [Matrix(true, false)] bool isMember,
        [Matrix(0, 1, 100, 1000)] int amount)
    {
        var discount = CalculateMemberDiscount(isMember, amount);

        if (!isMember)
        {
            await Assert.That(discount).IsEqualTo(0m);
        }
        else if (amount >= 1000)
        {
            await Assert.That(discount).IsGreaterThan(0m);
        }
    }

    /// <summary>2 x 2 = 4 cases for a boolean cross.</summary>
    [Test]
    [MatrixDataSource]
    public async Task ValidateInput_AtLeastOneContactMethod_ShouldPass(
        [Matrix(true, false)] bool hasEmail,
        [Matrix(true, false)] bool hasPhone)
    {
        var isValid = hasEmail || hasPhone;

        if (hasEmail || hasPhone)
        {
            await Assert.That(isValid).IsTrue();
        }
        else
        {
            await Assert.That(isValid).IsFalse();
        }
    }

    /// <summary>3 x 4 = 12 cases mixing strings and numbers.</summary>
    [Test]
    [MatrixDataSource]
    public async Task ProcessPayment_PerMethodAndAmount_ShouldCalculateFee(
        [Matrix("CreditCard", "DebitCard", "BankTransfer")] string paymentMethod,
        [Matrix(100, 500, 1000, 5000)] decimal amount)
    {
        var fee = CalculatePaymentFee(paymentMethod, amount);

        await Assert.That(fee).IsGreaterThanOrEqualTo(0);

        if (paymentMethod == "BankTransfer" && amount >= 1000)
        {
            await Assert.That(fee).IsEqualTo(0);
        }
    }

    // ===== helpers =====

    private static decimal CalculateShippingFee(Order order)
    {
        if (order.CustomerLevel == CustomerLevel.Diamond) return 0m;
        if (order.SubTotal >= 1000m) return 0m;
        if (order.CustomerLevel is CustomerLevel.Vip or CustomerLevel.Platinum) return 40m;
        return 80m;
    }

    private static bool IsEligibleForFreeShipping(Order order) =>
        order.CustomerLevel == CustomerLevel.Diamond || order.SubTotal >= 1000m;

    private static decimal CalculateMemberDiscount(bool isMember, int amount)
    {
        if (!isMember) return 0m;
        if (amount >= 1000) return amount * 0.1m;
        if (amount >= 100) return amount * 0.05m;
        return 0m;
    }

    private static decimal CalculatePaymentFee(string paymentMethod, decimal amount) =>
        paymentMethod switch
        {
            "CreditCard" => amount * 0.03m,
            "DebitCard" => amount * 0.01m,
            "BankTransfer" when amount >= 1000 => 0m,
            "BankTransfer" => 30m,
            _ => 0m
        };
}

/*
 * Anti-patterns to avoid:
 *
 * - Too many dimensions. 5 x 4 x 3 x 6 = 360 cases. The test run slows
 *   to a crawl and individual failures stop being diagnosable.
 *
 * - Enums as named constants in [Matrix(...)]. C# attribute literal
 *   rules disallow them. Use the underlying integer values; TUnit
 *   converts them back to the enum on the parameter.
 *
 * Guidelines:
 *
 * 1. Limit total combinations to ~50.
 * 2. Pick boundary values (0, 1, threshold, threshold + 1, max).
 * 3. Use [Arguments] for the small set of representative combinations
 *    when matrix growth would be unmanageable.
 */
