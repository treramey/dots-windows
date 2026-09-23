// =============================================================================
// Bogus advanced patterns and custom extensions
// Cascading business rules, custom datasets, performance tuning, test integration
// =============================================================================

using Bogus;
using AwesomeAssertions;
using Xunit;

namespace BogusAdvanced.Templates;

#region Test model classes

public class Employee
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Level { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public string Department { get; set; } = string.Empty;
    public bool IsManager { get; set; }
}

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string> Technologies { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}

public class TaiwanPerson
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IdCard { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string University { get; set; } = string.Empty;
}

public class GlobalUser
{
    public Guid Id { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class TestBoundaryData
{
    public string? NullableString { get; set; }
    public string ShortString { get; set; } = string.Empty;
    public string LongString { get; set; } = string.Empty;
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public int ZeroValue { get; set; }
    public int NegativeValue { get; set; }
    public int PositiveValue { get; set; }
    public string SpecialChars { get; set; } = string.Empty;
    public DateTime MinDate { get; set; }
    public DateTime MaxDate { get; set; }
}

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

#endregion

#region Cascading business rules

public class ComplexBusinessLogicExamples
{
    /// <summary>
    /// Each subsequent rule observes the partly-built entity and chooses values consistent with it.
    /// </summary>
    [Fact]
    public void Employee_CascadingRulesProduceConsistentObject()
    {
        var employeeFaker = new Faker<Employee>()
            .RuleFor(e => e.Id, f => f.Random.Guid())
            .RuleFor(e => e.FirstName, f => f.Person.FirstName)
            .RuleFor(e => e.LastName, f => f.Person.LastName)
            .RuleFor(e => e.Email, (f, e) =>
                f.Internet.Email(e.FirstName, e.LastName, "company.com"))
            .RuleFor(e => e.Age, f => f.Random.Int(22, 65))
            // Level depends on age
            .RuleFor(e => e.Level, (f, e) => e.Age switch
            {
                < 25 => "Junior",
                < 35 => "Senior",
                < 45 => "Lead",
                _    => "Principal"
            })
            // Salary depends on level
            .RuleFor(e => e.Salary, (f, e) => e.Level switch
            {
                "Junior"    => f.Random.Decimal(35000, 50000),
                "Senior"    => f.Random.Decimal(50000, 80000),
                "Lead"      => f.Random.Decimal(80000, 120000),
                "Principal" => f.Random.Decimal(120000, 200000),
                _           => f.Random.Decimal(35000, 50000)
            })
            // Hire date can't precede age 22
            .RuleFor(e => e.HireDate, (f, e) =>
            {
                var maxYearsAgo = Math.Max(1, e.Age - 22);
                return f.Date.Past(maxYearsAgo);
            })
            // Skills list
            .RuleFor(e => e.Skills, f =>
            {
                var allSkills = new[]
                {
                    "C#", ".NET", "JavaScript", "TypeScript", "React", "Angular", "Vue",
                    "SQL Server", "PostgreSQL", "MongoDB", "Redis",
                    "Azure", "AWS", "Docker", "Kubernetes", "Git"
                };
                return f.PickRandom(allSkills, f.Random.Int(2, 6)).ToList();
            })
            .RuleFor(e => e.Department, f =>
                f.PickRandom("Engineering", "Product", "Design", "QA", "DevOps"))
            // 30% of Leads/Principals are managers
            .RuleFor(e => e.IsManager, (f, e) =>
                (e.Level == "Lead" || e.Level == "Principal") && f.Random.Bool(0.3f))
            // Project history scaled by tenure
            .RuleFor(e => e.Projects, (f, e) =>
            {
                var projectFaker = new Faker<Project>()
                    .RuleFor(p => p.Id, f => f.Random.Guid())
                    .RuleFor(p => p.Name, f => f.Company.CatchPhrase())
                    .RuleFor(p => p.Description, f => f.Lorem.Sentence())
                    .RuleFor(p => p.StartDate, f => f.Date.Between(e.HireDate, DateTime.Now.AddMonths(-1)))
                    .RuleFor(p => p.EndDate, (f, p) =>
                        f.Random.Bool(0.8f) ? f.Date.Between(p.StartDate, DateTime.Now) : null)
                    .RuleFor(p => p.Status, (f, p) =>
                        p.EndDate.HasValue ? "Completed" : f.PickRandom("In Progress", "On Hold"))
                    .RuleFor(p => p.Technologies, f =>
                        f.PickRandom(e.Skills, f.Random.Int(1, Math.Min(3, e.Skills.Count))).ToList());

                var yearsOfExperience = (DateTime.Now - e.HireDate).Days / 365;
                var projectCount = Math.Max(1, yearsOfExperience / 2);
                return projectFaker.Generate(f.Random.Int(1, projectCount));
            });

        var employee = employeeFaker.Generate();

        employee.Age.Should().BeInRange(22, 65);
        employee.Email.Should().EndWith("@company.com");
        employee.HireDate.Should().BeBefore(DateTime.Now);
        employee.Skills.Should().HaveCountGreaterOrEqualTo(2);

        if (employee.Level == "Junior")
        {
            employee.Salary.Should().BeInRange(35000, 50000);
        }
    }

    /// <summary>
    /// Build a small organisation: one manager per department + 3-8 ICs.
    /// </summary>
    [Fact]
    public void Organisation_BuildsHierarchy()
    {
        var departments = new[] { "Engineering", "Product", "Design", "QA", "DevOps" };

        var managerFaker = new Faker<Employee>()
            .RuleFor(e => e.Id, f => f.Random.Guid())
            .RuleFor(e => e.FirstName, f => f.Person.FirstName)
            .RuleFor(e => e.LastName, f => f.Person.LastName)
            .RuleFor(e => e.Age, f => f.Random.Int(35, 55))
            .RuleFor(e => e.Level, _ => "Lead")
            .RuleFor(e => e.IsManager, _ => true)
            .RuleFor(e => e.Salary, f => f.Random.Decimal(100000, 150000));

        var employeeFaker = new Faker<Employee>()
            .RuleFor(e => e.Id, f => f.Random.Guid())
            .RuleFor(e => e.FirstName, f => f.Person.FirstName)
            .RuleFor(e => e.LastName, f => f.Person.LastName)
            .RuleFor(e => e.Age, f => f.Random.Int(22, 40))
            .RuleFor(e => e.Level, f => f.PickRandom("Junior", "Senior"))
            .RuleFor(e => e.IsManager, _ => false)
            .RuleFor(e => e.Salary, (f, e) => e.Level == "Junior"
                ? f.Random.Decimal(35000, 50000)
                : f.Random.Decimal(50000, 80000));

        var organization = departments.Select(dept =>
        {
            var manager = managerFaker.Generate();
            manager.Department = dept;

            var teamSize = new Faker().Random.Int(3, 8);
            var team = employeeFaker.Generate(teamSize);
            team.ForEach(e => e.Department = dept);

            return new { Department = dept, Manager = manager, Team = team };
        }).ToList();

        organization.Should().HaveCount(5);
        organization.All(o => o.Manager.IsManager).Should().BeTrue();
        organization.All(o => o.Team.All(e => !e.IsManager)).Should().BeTrue();
    }
}

#endregion

#region Custom DataSet extensions

/// <summary>
/// Domain-specific data generators for Taiwan. Extend Faker via extension methods.
/// </summary>
public static class TaiwanDataSetExtensions
{
    private static readonly string[] TaiwanCities =
    {
        "Taipei", "New Taipei", "Taoyuan", "Taichung", "Tainan", "Kaohsiung",
        "Keelung", "Hsinchu City", "Chiayi City", "Yilan", "Hsinchu County", "Miaoli",
        "Changhua", "Nantou", "Yunlin", "Chiayi County", "Pingtung", "Taitung",
        "Hualien", "Penghu", "Kinmen", "Lienchiang"
    };

    private static readonly string[] TaiwanDistricts =
    {
        "Zhongzheng", "Datong", "Zhongshan", "Songshan", "Daan", "Wanhua",
        "Xinyi", "Shilin", "Beitou", "Neihu", "Nangang", "Wenshan"
    };

    private static readonly string[] TaiwanUniversities =
    {
        "National Taiwan University", "National Tsing Hua University", "National Chiao Tung University",
        "National Cheng Kung University", "National Sun Yat-sen University",
        "National Chengchi University", "National Central University", "National Chung Cheng University",
        "National Chung Hsing University", "National Taiwan Normal University",
        "National Taipei University of Technology", "National Taiwan University of Science and Technology",
        "National Kaohsiung University of Science and Technology"
    };

    private static readonly string[] TaiwanCompanies =
    {
        "TSMC", "Foxconn", "MediaTek", "Chunghwa Telecom", "Formosa Plastics", "Uni-President",
        "Fubon", "CTBC", "Cathay", "FarEasTone", "ASUS", "Acer", "Quanta"
    };

    public static string TaiwanCity(this Faker faker)
        => faker.PickRandom(TaiwanCities);

    public static string TaiwanDistrict(this Faker faker)
        => faker.PickRandom(TaiwanDistricts);

    public static string TaiwanUniversity(this Faker faker)
        => faker.PickRandom(TaiwanUniversities);

    public static string TaiwanCompany(this Faker faker)
        => faker.PickRandom(TaiwanCompanies);

    /// <summary>
    /// Format-correct Taiwan ID card number — not a real valid ID.
    /// </summary>
    public static string TaiwanIdCard(this Faker faker)
    {
        var firstChar = faker.PickRandom("ABCDEFGHJKLMNPQRSTUVXYWZIO".ToCharArray());
        var genderDigit = faker.Random.Int(1, 2);
        var digits = faker.Random.String2(8, "0123456789");
        return $"{firstChar}{genderDigit}{digits}";
    }

    /// <summary>
    /// Taiwan mobile number — 09XX-XXX-XXX.
    /// </summary>
    public static string TaiwanMobilePhone(this Faker faker)
    {
        var thirdDigit = faker.Random.Int(0, 9);
        var fourthDigit = faker.Random.Int(0, 9);
        var middle = faker.Random.String2(3, "0123456789");
        var suffix = faker.Random.String2(3, "0123456789");
        return $"09{thirdDigit}{fourthDigit}-{middle}-{suffix}";
    }

    /// <summary>
    /// Landline — (02) XXXX-XXXX style.
    /// </summary>
    public static string TaiwanLandlinePhone(this Faker faker)
    {
        var areaCodes = new[] { "02", "03", "04", "05", "06", "07", "08" };
        var areaCode = faker.PickRandom(areaCodes);

        var part1 = areaCode == "02"
            ? faker.Random.String2(4, "0123456789")
            : faker.Random.String2(3, "0123456789");
        var part2 = faker.Random.String2(4, "0123456789");

        return $"({areaCode}) {part1}-{part2}";
    }

    /// <summary>
    /// Composite Taiwan address.
    /// </summary>
    public static string TaiwanFullAddress(this Faker faker)
    {
        var city = faker.TaiwanCity();
        var district = faker.TaiwanDistrict();
        var road = faker.PickRandom("Zhongzheng Rd", "Zhongshan Rd", "Minsheng Rd", "Zhongxiao Rd", "Fuxing Rd", "Jianguo Rd");
        var number = faker.Random.Int(1, 500);
        var floor = faker.Random.Int(1, 20);

        return $"{city} {district} {road} {number}, Floor {floor}";
    }
}

public class TaiwanDataSetTests
{
    [Fact]
    public void TaiwanDataSet_GeneratesExpectedShape()
    {
        var taiwanPersonFaker = new Faker<TaiwanPerson>("zh_TW")
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.Name, f => f.Person.FullName)
            .RuleFor(p => p.IdCard, f => f.TaiwanIdCard())
            .RuleFor(p => p.City, f => f.TaiwanCity())
            .RuleFor(p => p.District, f => f.TaiwanDistrict())
            .RuleFor(p => p.Address, f => f.TaiwanFullAddress())
            .RuleFor(p => p.Mobile, f => f.TaiwanMobilePhone())
            .RuleFor(p => p.Company, f => f.TaiwanCompany())
            .RuleFor(p => p.University, f => f.TaiwanUniversity());

        var person = taiwanPersonFaker.Generate();

        person.IdCard.Should().HaveLength(10);
        person.Mobile.Should().StartWith("09");
        person.Address.Should().NotBeNullOrEmpty();
    }
}

#endregion

#region Multi-locale per-row generation

public class MultiLanguageAdvancedExamples
{
    [Fact]
    public void GlobalUser_PerRowLocaleSwitch()
    {
        var locales = new[] { "en_US", "zh_TW", "ja", "ko", "fr", "de" };

        var globalUserFaker = new Faker<GlobalUser>()
            .RuleFor(u => u.Id, f => f.Random.Guid())
            .RuleFor(u => u.Locale, f => f.PickRandom(locales))
            .RuleFor(u => u.Name, (f, u) =>
            {
                var localFaker = new Faker(u.Locale);
                return localFaker.Person.FullName;
            })
            .RuleFor(u => u.Address, (f, u) =>
            {
                var localFaker = new Faker(u.Locale);
                return localFaker.Address.FullAddress();
            })
            .RuleFor(u => u.Phone, (f, u) =>
            {
                var localFaker = new Faker(u.Locale);
                return localFaker.Phone.PhoneNumber();
            });

        var users = globalUserFaker.Generate(10);

        users.Should().HaveCount(10);
        users.All(u => !string.IsNullOrEmpty(u.Name)).Should().BeTrue();
    }
}

#endregion

#region Boundary-value data

public class BoundaryTestExamples
{
    /// <summary>
    /// Generate deliberately exotic data for property-based / fuzz-style tests.
    /// </summary>
    [Fact]
    public void Boundary_GeneratesEdgeCases()
    {
        var boundaryFaker = new Faker<TestBoundaryData>()
            .RuleFor(t => t.NullableString, f => f.PickRandom<string?>(null, "", " ", "valid"))
            .RuleFor(t => t.ShortString, f => f.Random.String2(1, 10))
            .RuleFor(t => t.LongString, f => f.Random.String2(255, 1000))
            .RuleFor(t => t.MinValue, _ => int.MinValue)
            .RuleFor(t => t.MaxValue, _ => int.MaxValue)
            .RuleFor(t => t.ZeroValue, _ => 0)
            .RuleFor(t => t.NegativeValue, f => f.Random.Int(int.MinValue, -1))
            .RuleFor(t => t.PositiveValue, f => f.Random.Int(1, int.MaxValue))
            .RuleFor(t => t.SpecialChars, f => f.PickRandom(
                "!@#$%^&*()",
                "<script>alert('xss')</script>",
                "Chinese characters",
                "Japanese: テスト",
                "Korean: 테스트",
                "emoji: 🎉🔥"))
            .RuleFor(t => t.MinDate, _ => DateTime.MinValue)
            .RuleFor(t => t.MaxDate, _ => DateTime.MaxValue);

        var boundaryData = boundaryFaker.Generate();

        boundaryData.MinValue.Should().Be(int.MinValue);
        boundaryData.MaxValue.Should().Be(int.MaxValue);
    }
}

#endregion

#region Performance — reuse Faker<T>

/// <summary>
/// Static, pre-compiled Faker<T> instances avoid repeated rule compilation.
/// </summary>
public static class OptimizedDataGenerator
{
    private static readonly Faker _faker = new();

    private static readonly Faker<User> _userFaker = CreateUserFaker();
    private static readonly Faker<Product> _productFaker = CreateProductFaker();

    private static Faker<User> CreateUserFaker()
    {
        return new Faker<User>()
            .RuleFor(u => u.Id, f => f.Random.Guid())
            .RuleFor(u => u.Name, f => f.Person.FullName)
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.Age, f => f.Random.Int(18, 80));
    }

    private static Faker<Product> CreateProductFaker()
    {
        return new Faker<Product>()
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000))
            .RuleFor(p => p.Category, f => f.Commerce.Department());
    }

    public static List<User> GenerateUsers(int count)
        => _userFaker.Generate(count);

    public static List<Product> GenerateProducts(int count)
        => _productFaker.Generate(count);

    /// <summary>
    /// Streaming batch generation — yields per item, lets the consumer short-circuit.
    /// </summary>
    public static IEnumerable<User> GenerateUsersBatch(int totalCount, int batchSize = 1000)
    {
        var generated = 0;
        while (generated < totalCount)
        {
            var currentBatchSize = Math.Min(batchSize, totalCount - generated);
            var batch = _userFaker.Generate(currentBatchSize);

            foreach (var user in batch)
            {
                yield return user;
            }

            generated += currentBatchSize;
        }
    }
}

/// <summary>
/// Lazy<T> defers rule compilation until first use.
/// </summary>
public static class LazyFakerProvider
{
    private static readonly Lazy<Faker<Employee>> _employeeFaker =
        new(() => CreateEmployeeFaker(), LazyThreadSafetyMode.ExecutionAndPublication);

    private static Faker<Employee> CreateEmployeeFaker()
    {
        return new Faker<Employee>()
            .RuleFor(e => e.Id, f => f.Random.Guid())
            .RuleFor(e => e.FirstName, f => f.Person.FirstName)
            .RuleFor(e => e.LastName, f => f.Person.LastName)
            .RuleFor(e => e.Email, f => f.Internet.Email())
            .RuleFor(e => e.Age, f => f.Random.Int(22, 65))
            .RuleFor(e => e.Level, f => f.PickRandom("Junior", "Senior", "Lead", "Principal"))
            .RuleFor(e => e.Salary, f => f.Random.Decimal(35000, 200000));
    }

    public static Employee GenerateEmployee()
        => _employeeFaker.Value.Generate();

    public static List<Employee> GenerateEmployees(int count)
        => _employeeFaker.Value.Generate(count);
}

public class PerformanceOptimizationTests
{
    [Fact]
    public void OptimizedGenerator_LargeBatch()
    {
        var users = OptimizedDataGenerator.GenerateUsers(1000);
        var products = OptimizedDataGenerator.GenerateProducts(500);

        users.Should().HaveCount(1000);
        products.Should().HaveCount(500);
    }

    [Fact]
    public void BatchGeneration_ConsumerCanShortCircuit()
    {
        var users = OptimizedDataGenerator.GenerateUsersBatch(10000, 500);

        // Only 100 generated, not 10000
        var sample = users.Take(100).ToList();

        sample.Should().HaveCount(100);
    }

    [Fact]
    public void LazyInitialization_FirstCallTriggersBuild()
    {
        var employees = LazyFakerProvider.GenerateEmployees(50);

        employees.Should().HaveCount(50);
        employees.All(e => e.Age >= 22 && e.Age <= 65).Should().BeTrue();
    }
}

#endregion

#region Test-integration examples

// Mock service interface
public interface IEmailService
{
    string GenerateWelcomeEmail(User user);
}

// Mock service implementation
public class EmailService : IEmailService
{
    public string GenerateWelcomeEmail(User user)
    {
        return $"Dear {user.Name},\n\nWelcome to our service!\n\nYour registered email: {user.Email}";
    }
}

public class EmailServiceTests
{
    /// <summary>
    /// Use Bogus when the SUT echoes data back and the assertion needs realistic shape.
    /// </summary>
    [Fact]
    public void GenerateWelcomeEmail_EmbedsNameAndEmail()
    {
        var userFaker = new Faker<User>()
            .RuleFor(u => u.Id, f => f.Random.Guid())
            .RuleFor(u => u.Name, f => f.Person.FullName)
            .RuleFor(u => u.Email, f => f.Internet.Email());

        var user = userFaker.Generate();
        var emailService = new EmailService();

        var emailContent = emailService.GenerateWelcomeEmail(user);

        emailContent.Should().Contain(user.Name);
        emailContent.Should().Contain(user.Email);
        emailContent.Should().Contain("Welcome");
    }

    /// <summary>
    /// Seed in the Arrange step makes a Bogus-driven test reproducible.
    /// </summary>
    [Fact]
    public void GenerateWelcomeEmail_ReproducibleWithSeed()
    {
        Randomizer.Seed = new Random(42);

        var userFaker = new Faker<User>()
            .RuleFor(u => u.Name, f => f.Person.FullName)
            .RuleFor(u => u.Email, f => f.Internet.Email());

        var user = userFaker.Generate();
        var expectedName = user.Name;

        Randomizer.Seed = new Random(42);
        var user2 = userFaker.Generate();

        user2.Name.Should().Be(expectedName);

        Randomizer.Seed = new Random();
    }
}

/// <summary>
/// Deterministic seed routine — useful for dev/test database seeders.
/// </summary>
public static class DatabaseSeeder
{
    public static List<User> GenerateSeedUsers(int count = 100)
    {
        Randomizer.Seed = new Random(42);

        var userFaker = new Faker<User>("zh_TW")
            .RuleFor(u => u.Id, f => f.Random.Guid())
            .RuleFor(u => u.Name, f => f.Person.FullName)
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.Age, f => f.Random.Int(18, 70));

        var users = userFaker.Generate(count);

        Randomizer.Seed = new Random();

        return users;
    }

    public static List<Product> GenerateSeedProducts(int count = 50)
    {
        Randomizer.Seed = new Random(42);

        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => f.Random.Decimal(100, 10000))
            .RuleFor(p => p.Category, f => f.Commerce.Department());

        var products = productFaker.Generate(count);

        Randomizer.Seed = new Random();

        return products;
    }
}

#endregion
