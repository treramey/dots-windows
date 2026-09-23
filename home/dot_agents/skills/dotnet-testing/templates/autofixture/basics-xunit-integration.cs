// =============================================================================
// AutoFixture + xUnit integration
// Shared fixture, Theory integration, and practical scenarios.
// =============================================================================

using AutoFixture;
using AwesomeAssertions;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net.Mail;

namespace TestProject.AutoFixtureBasics;

/// <summary>
/// Demonstrates AutoFixture integration with xUnit.
/// </summary>
public class XunitIntegrationTests
{
    #region Fixture sharing pattern

    /// <summary>
    /// Class-level fixture sharing.
    /// </summary>
    public class ProductServiceTests
    {
        private readonly Fixture _fixture;

        public ProductServiceTests()
        {
            _fixture = new Fixture();

            // Common customization in the constructor.
            _fixture.Customize<ProductCreateRequest>(c => c
                .With(x => x.Price, () => Math.Round((decimal)Random.Shared.NextDouble() * 10000, 2))
                .With(x => x.Name, () => $"Product-{_fixture.Create<string>()[..8]}")
            );
        }

        [Fact]
        public void CreateProduct_SharedFixture_AppliesCustomization()
        {
            var productData = _fixture.Create<ProductCreateRequest>();

            productData.Price.Should().BeLessThan(10000);
            productData.Name.Should().StartWith("Product-");
        }

        [Fact]
        public void CreateProducts_SharedFixture_ConsistentValues()
        {
            var products = _fixture.CreateMany<ProductCreateRequest>(5);

            products.Should().AllSatisfy(p =>
            {
                p.Name.Should().StartWith("Product-");
                p.Price.Should().BeLessThan(10000);
            });
        }
    }

    #endregion

    #region Theory integration

    [Theory]
    [InlineData(CustomerType.Regular)]
    [InlineData(CustomerType.Premium)]
    [InlineData(CustomerType.VIP)]
    public void CalculateDiscount_ByCustomerType_AppliesCorrectDiscount(CustomerType customerType)
    {
        var fixture = new Fixture();

        var customer = fixture.Build<Customer>()
            .With(x => x.Type, customerType)
            .Create();

        var calculator = new DiscountCalculator();

        var discount = calculator.Calculate(customer);

        switch (customerType)
        {
            case CustomerType.Regular:
                discount.Should().Be(0m);
                break;
            case CustomerType.Premium:
                discount.Should().Be(0.10m);
                break;
            case CustomerType.VIP:
                discount.Should().Be(0.20m);
                break;
        }
    }

    [Theory]
    [InlineData(0, CustomerLevel.Bronze)]
    [InlineData(5000, CustomerLevel.Bronze)]
    [InlineData(15000, CustomerLevel.Silver)]
    [InlineData(60000, CustomerLevel.Gold)]
    [InlineData(120000, CustomerLevel.Diamond)]
    public void GetLevel_BySpend_ReturnsExpectedLevel(decimal totalSpent, CustomerLevel expected)
    {
        var fixture = new Fixture();
        var customer = fixture.Build<Customer>()
            .With(x => x.TotalSpent, totalSpent)
            .Create();

        var level = customer.GetLevel();

        level.Should().Be(expected);
    }

    #endregion

    #region MemberData + AutoFixture

    public static IEnumerable<object[]> GetPricingTestData()
    {
        var fixture = new Fixture();
        var quantities = new[] { 1, 3, 5, 10, 20 };

        foreach (var quantity in quantities)
        {
            var product = fixture.Create<Product>();
            var expectedTotal = product.Price * quantity;
            yield return new object[] { product, quantity, expectedTotal };
        }
    }

    [Theory]
    [MemberData(nameof(GetPricingTestData))]
    public void CalculateTotal_VariousProductsAndQuantities_CalculatesExpectedTotal(
        Product product, int quantity, decimal expectedTotal)
    {
        var calculator = new PriceCalculator();

        var total = calculator.Calculate(product, quantity);

        total.Should().Be(expectedTotal);
    }

    #endregion

    #region Anonymous-testing principle

    [Fact]
    public void AnonymousTest_FocusesOnBehaviorNotData()
    {
        var fixture = new Fixture();
        var customer = fixture.Create<Customer>();
        var repository = new CustomerRepository();

        var result = repository.Add(customer);

        result.Should().BeTrue();
    }

    [Fact]
    public void ExplicitlyPinKeyValues_KeepsTestStable()
    {
        var fixture = new Fixture();
        var customer = fixture.Build<Customer>()
            .With(x => x.Age, 25)
            .Create();

        var validator = new CustomerValidator();

        var isAdult = validator.IsAdult(customer);

        isAdult.Should().BeTrue();
    }

    #endregion

    #region DTO validation

    [Fact]
    public void ValidateRequest_ValidData_Passes()
    {
        var fixture = new Fixture();

        var request = fixture.Build<CreateCustomerRequest>()
            .With(x => x.Name, fixture.Create<string>()[..50])
            .With(x => x.Email, fixture.Create<MailAddress>().Address)
            .With(x => x.Age, Random.Shared.Next(18, 78))
            .Create();

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(request, context, results, true);

        isValid.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRequest_NameTooLong_Fails()
    {
        var fixture = new Fixture();
        var request = fixture.Build<CreateCustomerRequest>()
            .With(x => x.Name, new string('A', 101))
            .With(x => x.Email, fixture.Create<MailAddress>().Address)
            .With(x => x.Age, 25)
            .Create();

        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(request, context, results, true);

        isValid.Should().BeFalse();
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(request.Name)));
    }

    #endregion

    #region Bulk data scenarios

    [Fact]
    public void ProcessBatch_LargeDataset_HandlesCorrectly()
    {
        var fixture = new Fixture();
        var records = fixture.CreateMany<DataRecord>(1000).ToList();
        var processor = new DataProcessor();

        var stopwatch = Stopwatch.StartNew();
        var result = processor.ProcessBatch(records);
        stopwatch.Stop();

        result.ProcessedCount.Should().Be(1000);
        result.ErrorCount.Should().Be(0);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000);
    }

    [Fact]
    public void SerializeDeserialize_ArbitraryObject_RemainsEquivalent()
    {
        var fixture = new Fixture();
        var original = fixture.Create<Customer>();

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Customer>(json);

        deserialized.Should().BeEquivalentTo(original);
    }

    #endregion

    #region Sample models and services

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public CustomerType Type { get; set; }
        public decimal TotalSpent { get; set; }

        public CustomerLevel GetLevel() => TotalSpent switch
        {
            >= 100000 => CustomerLevel.Diamond,
            >= 50000 => CustomerLevel.Gold,
            >= 10000 => CustomerLevel.Silver,
            _ => CustomerLevel.Bronze
        };
    }

    public enum CustomerType { Regular, Premium, VIP }
    public enum CustomerLevel { Bronze, Silver, Gold, Diamond }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class ProductCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    public class CreateCustomerRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Range(18, 120)]
        public int Age { get; set; }
    }

    public class DataRecord
    {
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class ProcessingResult
    {
        public int ProcessedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class DiscountCalculator
    {
        public decimal Calculate(Customer customer) => customer.Type switch
        {
            CustomerType.VIP => 0.20m,
            CustomerType.Premium => 0.10m,
            _ => 0m
        };
    }

    public class PriceCalculator
    {
        public decimal Calculate(Product product, int quantity) => product.Price * quantity;
    }

    public class CustomerRepository
    {
        public bool Add(Customer customer) => customer != null && !string.IsNullOrEmpty(customer.Name);
    }

    public class CustomerValidator
    {
        public bool IsAdult(Customer customer) => customer.Age >= 18;
    }

    public class DataProcessor
    {
        public ProcessingResult ProcessBatch(IEnumerable<DataRecord> records)
        {
            var processed = 0;
            var errors = new List<string>();

            foreach (var record in records)
            {
                try
                {
                    processed++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Record {record.Id}: {ex.Message}");
                }
            }

            return new ProcessingResult
            {
                ProcessedCount = processed,
                ErrorCount = errors.Count,
                Errors = errors
            };
        }
    }

    #endregion
}
