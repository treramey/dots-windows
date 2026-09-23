using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TestDataBuilderPattern.TheoryExamples
{
    // ===== Domain Models =====

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public CustomerType Type { get; set; }
        public decimal CreditLimit { get; set; }
        public bool IsVerified { get; set; }
    }

    public enum CustomerType
    {
        Regular,
        Premium,
        VIP
    }

    // ===== Customer Builder =====

    public class CustomerBuilder
    {
        private int _id = 1;
        private string _name = "Default Customer";
        private string _email = "customer@example.com";
        private CustomerType _type = CustomerType.Regular;
        private decimal _creditLimit = 1000m;
        private bool _isVerified = true;

        public CustomerBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public CustomerBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public CustomerBuilder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public CustomerBuilder OfType(CustomerType type)
        {
            _type = type;
            return this;
        }

        public CustomerBuilder WithCreditLimit(decimal limit)
        {
            _creditLimit = limit;
            return this;
        }

        public CustomerBuilder Unverified()
        {
            _isVerified = false;
            return this;
        }

        // Semantic factories
        public static CustomerBuilder ACustomer() => new();

        public static CustomerBuilder ARegularCustomer() => new CustomerBuilder()
            .OfType(CustomerType.Regular)
            .WithCreditLimit(1000m);

        public static CustomerBuilder APremiumCustomer() => new CustomerBuilder()
            .OfType(CustomerType.Premium)
            .WithCreditLimit(5000m);

        public static CustomerBuilder AVIPCustomer() => new CustomerBuilder()
            .OfType(CustomerType.VIP)
            .WithCreditLimit(10000m);

        public Customer Build()
        {
            return new Customer
            {
                Id = _id,
                Name = _name,
                Email = _email,
                Type = _type,
                CreditLimit = _creditLimit,
                IsVerified = _isVerified
            };
        }
    }

    // ===== Builder + xUnit Theory tests =====

    public class CustomerServiceTests
    {
        // Example 1: MemberData paired with the builder for different customer types
        [Theory]
        [MemberData(nameof(GetCustomerTypeScenarios))]
        public void CalculateDiscount_ForCustomerTier_ShouldReturnTierDiscount(Customer customer, decimal expectedDiscount)
        {
            // Arrange
            var service = new CustomerService();

            // Act
            var discount = service.CalculateDiscount(customer);

            // Assert
            Assert.Equal(expectedDiscount, discount);
        }

        public static IEnumerable<object[]> GetCustomerTypeScenarios()
        {
            // Regular customer — no discount
            yield return new object[]
            {
                CustomerBuilder.ARegularCustomer()
                    .WithName("Regular John")
                    .Build(),
                0m
            };

            // Premium customer — 5% discount
            yield return new object[]
            {
                CustomerBuilder.APremiumCustomer()
                    .WithName("Premium Jane")
                    .Build(),
                0.05m
            };

            // VIP customer — 10% discount
            yield return new object[]
            {
                CustomerBuilder.AVIPCustomer()
                    .WithName("VIP Alice")
                    .Build(),
                0.10m
            };
        }

        // Example 2: customer validation
        [Theory]
        [MemberData(nameof(GetCustomerValidationScenarios))]
        public void Validate_WithVariousCustomers_ShouldReturnExpected(Customer customer, bool expectedValid, string description)
        {
            // Arrange
            var validator = new CustomerValidator();

            // Act
            var isValid = validator.IsValid(customer);

            // Assert
            Assert.Equal(expectedValid, isValid);
        }

        public static IEnumerable<object[]> GetCustomerValidationScenarios()
        {
            // Valid customer
            yield return new object[]
            {
                CustomerBuilder.ACustomer()
                    .WithName("Valid Customer")
                    .WithEmail("valid@example.com")
                    .Build(),
                true,
                "Valid generic customer"
            };

            // Invalid — empty name
            yield return new object[]
            {
                CustomerBuilder.ACustomer()
                    .WithName("")
                    .WithEmail("test@example.com")
                    .Build(),
                false,
                "Name is empty"
            };

            // Invalid — malformed email
            yield return new object[]
            {
                CustomerBuilder.ACustomer()
                    .WithName("Test User")
                    .WithEmail("invalid-email")
                    .Build(),
                false,
                "Email format invalid"
            };

            // Invalid — unverified
            yield return new object[]
            {
                CustomerBuilder.ACustomer()
                    .WithName("Unverified User")
                    .WithEmail("unverified@example.com")
                    .Unverified()
                    .Build(),
                false,
                "Customer not verified"
            };

            // Invalid — negative credit limit
            yield return new object[]
            {
                CustomerBuilder.ACustomer()
                    .WithName("Negative Credit")
                    .WithCreditLimit(-100m)
                    .Build(),
                false,
                "Credit limit is negative"
            };
        }

        // Example 3: credit approval logic
        [Theory]
        [MemberData(nameof(GetCreditApprovalScenarios))]
        public void ApproveCredit_WithLimitAndCustomerType_ShouldReturnExpected(
            Customer customer,
            decimal requestAmount,
            bool expectedApproved)
        {
            // Arrange
            var service = new CustomerService();

            // Act
            var approved = service.ApproveCreditRequest(customer, requestAmount);

            // Assert
            Assert.Equal(expectedApproved, approved);
        }

        public static IEnumerable<object[]> GetCreditApprovalScenarios()
        {
            // Regular customer — within limit
            yield return new object[]
            {
                CustomerBuilder.ARegularCustomer().Build(),
                500m,
                true
            };

            // Regular customer — exceeds limit
            yield return new object[]
            {
                CustomerBuilder.ARegularCustomer().Build(),
                1500m,
                false
            };

            // Premium customer — higher allowance
            yield return new object[]
            {
                CustomerBuilder.APremiumCustomer().Build(),
                4000m,
                true
            };

            // Premium customer — over the cap
            yield return new object[]
            {
                CustomerBuilder.APremiumCustomer().Build(),
                6000m,
                false
            };

            // VIP — large amount
            yield return new object[]
            {
                CustomerBuilder.AVIPCustomer().Build(),
                9000m,
                true
            };

            // Unverified — any request rejected
            yield return new object[]
            {
                CustomerBuilder.ACustomer()
                    .Unverified()
                    .Build(),
                100m,
                false
            };
        }

        // Example 4: ClassData with TheoryData<T1, T2>
        [Theory]
        [ClassData(typeof(CustomerUpgradeTestData))]
        public void Upgrade_WhenEligible_ShouldRaiseTier(
            Customer customer,
            CustomerType expectedType)
        {
            // Arrange
            var service = new CustomerService();

            // Act
            var upgradedCustomer = service.UpgradeCustomer(customer);

            // Assert
            Assert.Equal(expectedType, upgradedCustomer.Type);
        }
    }

    // ===== ClassData implementation using TheoryData<T1, T2> =====

    public class CustomerUpgradeTestData : TheoryData<Customer, CustomerType>
    {
        public CustomerUpgradeTestData()
        {
            // Regular -> Premium when credit limit >= 2000
            Add(
                CustomerBuilder.ARegularCustomer()
                    .WithCreditLimit(2000m)
                    .Build(),
                CustomerType.Premium
            );

            // Premium -> VIP when credit limit >= 7000
            Add(
                CustomerBuilder.APremiumCustomer()
                    .WithCreditLimit(7000m)
                    .Build(),
                CustomerType.VIP
            );

            // Doesn't qualify — stays at the same tier
            Add(
                CustomerBuilder.ARegularCustomer()
                    .WithCreditLimit(1000m)
                    .Build(),
                CustomerType.Regular
            );
        }
    }

    // ===== Mock services (for demonstration) =====

    public class CustomerService
    {
        public decimal CalculateDiscount(Customer customer)
        {
            return customer.Type switch
            {
                CustomerType.Regular => 0m,
                CustomerType.Premium => 0.05m,
                CustomerType.VIP => 0.10m,
                _ => 0m
            };
        }

        public bool ApproveCreditRequest(Customer customer, decimal amount)
        {
            if (!customer.IsVerified)
                return false;

            return amount <= customer.CreditLimit;
        }

        public Customer UpgradeCustomer(Customer customer)
        {
            var newType = customer.Type;

            if (customer.Type == CustomerType.Regular && customer.CreditLimit >= 2000m)
                newType = CustomerType.Premium;
            else if (customer.Type == CustomerType.Premium && customer.CreditLimit >= 7000m)
                newType = CustomerType.VIP;

            return new Customer
            {
                Id = customer.Id,
                Name = customer.Name,
                Email = customer.Email,
                Type = newType,
                CreditLimit = customer.CreditLimit,
                IsVerified = customer.IsVerified
            };
        }
    }

    public class CustomerValidator
    {
        public bool IsValid(Customer customer)
        {
            if (string.IsNullOrWhiteSpace(customer.Name))
                return false;

            if (!IsValidEmail(customer.Email))
                return false;

            if (!customer.IsVerified)
                return false;

            if (customer.CreditLimit < 0)
                return false;

            return true;
        }

        private bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && email.Contains("@");
        }
    }
}
