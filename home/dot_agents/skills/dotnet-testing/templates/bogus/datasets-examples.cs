// =============================================================================
// Bogus DataSet usage examples
// Complete catalog of every built-in DataSet
// =============================================================================

using Bogus;
using AwesomeAssertions;
using Xunit;

namespace BogusDataSets.Templates;

public class DataSetExamples
{
    private readonly Faker _faker = new();

    #region Person DataSet

    /// <summary>
    /// Person — a coherent identity (FullName/Email/Phone all match one person).
    /// </summary>
    [Fact]
    public void PersonDataSet_PersonalInformation()
    {
        var person = _faker.Person;

        var fullName = person.FullName;
        var firstName = person.FirstName;
        var lastName = person.LastName;
        var userName = person.UserName;

        var email = person.Email;
        var phone = person.Phone;
        var website = person.Website;

        var gender = person.Gender;            // Male / Female
        var dateOfBirth = person.DateOfBirth;

        var company = person.Company;          // Company information
        var address = person.Address;          // Address object

        fullName.Should().NotBeNullOrEmpty();
        email.Should().Contain("@");
        dateOfBirth.Should().BeBefore(DateTime.Now);
    }

    /// <summary>
    /// Name — ad-hoc names (not a coherent identity, just strings).
    /// </summary>
    [Fact]
    public void NameDataSet_Names()
    {
        var firstName = _faker.Name.FirstName();
        var lastName = _faker.Name.LastName();
        var fullName = _faker.Name.FullName();
        var prefix = _faker.Name.Prefix();                // "Mr.", "Ms.", "Dr."
        var suffix = _faker.Name.Suffix();                // "Jr.", "Sr.", "III"
        var jobTitle = _faker.Name.JobTitle();
        var jobDescriptor = _faker.Name.JobDescriptor();
        var jobArea = _faker.Name.JobArea();

        firstName.Should().NotBeNullOrEmpty();
        jobTitle.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Address DataSet

    /// <summary>
    /// Address — composable street/city/state/country parts.
    /// </summary>
    [Fact]
    public void AddressDataSet_Addresses()
    {
        var fullAddress = _faker.Address.FullAddress();

        var streetAddress = _faker.Address.StreetAddress();
        var secondaryAddress = _faker.Address.SecondaryAddress();   // Apt/Suite
        var city = _faker.Address.City();
        var cityPrefix = _faker.Address.CityPrefix();
        var citySuffix = _faker.Address.CitySuffix();
        var state = _faker.Address.State();
        var stateAbbr = _faker.Address.StateAbbr();
        var zipCode = _faker.Address.ZipCode();
        var buildingNumber = _faker.Address.BuildingNumber();
        var streetName = _faker.Address.StreetName();
        var streetSuffix = _faker.Address.StreetSuffix();

        var country = _faker.Address.Country();
        var countryCode = _faker.Address.CountryCode();

        var latitude = _faker.Address.Latitude();
        var longitude = _faker.Address.Longitude();
        var direction = _faker.Address.Direction();                  // North, South...
        var cardinalDirection = _faker.Address.CardinalDirection();

        fullAddress.Should().NotBeNullOrEmpty();
        latitude.Should().BeInRange(-90, 90);
        longitude.Should().BeInRange(-180, 180);
    }

    #endregion

    #region Company DataSet

    /// <summary>
    /// Company — corporate names, catch phrases, and "bs" filler text.
    /// </summary>
    [Fact]
    public void CompanyDataSet_Companies()
    {
        var companyName = _faker.Company.CompanyName();
        var companySuffix = _faker.Company.CompanySuffix();          // Inc., LLC
        var catchPhrase = _faker.Company.CatchPhrase();
        var bs = _faker.Company.Bs();                                // Buzzword soup

        companyName.Should().NotBeNullOrEmpty();
        catchPhrase.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Commerce DataSet

    /// <summary>
    /// Commerce — products, departments, prices, barcodes, colours.
    /// </summary>
    [Fact]
    public void CommerceDataSet_CommerceData()
    {
        var productName = _faker.Commerce.ProductName();
        var productAdjective = _faker.Commerce.ProductAdjective();
        var productMaterial = _faker.Commerce.ProductMaterial();
        var product = _faker.Commerce.Product();

        var department = _faker.Commerce.Department();
        var categories = _faker.Commerce.Categories(3);

        var price = _faker.Commerce.Price(1, 1000, 2);
        var priceDecimal = _faker.Commerce.Price(1, 1000, 2, "$");

        var ean8 = _faker.Commerce.Ean8();
        var ean13 = _faker.Commerce.Ean13();

        var color = _faker.Commerce.Color();

        productName.Should().NotBeNullOrEmpty();
        department.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Internet DataSet

    /// <summary>
    /// Internet — emails, URLs, IPs, user agents, passwords, avatars.
    /// </summary>
    [Fact]
    public void InternetDataSet_InternetData()
    {
        var email = _faker.Internet.Email();
        var emailWithName = _faker.Internet.Email("john", "doe");
        var exampleEmail = _faker.Internet.ExampleEmail();           // example.com

        var userName = _faker.Internet.UserName();
        var userNameWithName = _faker.Internet.UserName("john", "doe");
        var password = _faker.Internet.Password();
        var passwordLength = _faker.Internet.Password(16, false, "", "!@#");

        var url = _faker.Internet.Url();
        var urlWithProtocol = _faker.Internet.UrlWithPath();
        var domainName = _faker.Internet.DomainName();
        var domainWord = _faker.Internet.DomainWord();
        var domainSuffix = _faker.Internet.DomainSuffix();

        var ip = _faker.Internet.Ip();
        var ipv6 = _faker.Internet.Ipv6();
        var mac = _faker.Internet.Mac();

        var userAgent = _faker.Internet.UserAgent();
        var protocol = _faker.Internet.Protocol();                   // http / https
        var port = _faker.Internet.Port();

        var avatar = _faker.Internet.Avatar();

        email.Should().Contain("@");
        ip.Should().MatchRegex(@"^\d+\.\d+\.\d+\.\d+$");
    }

    #endregion

    #region Finance DataSet

    /// <summary>
    /// Finance — credit cards, accounts, IBAN/BIC, amounts, cryptocurrency.
    /// </summary>
    [Fact]
    public void FinanceDataSet_FinanceData()
    {
        var creditCardNumber = _faker.Finance.CreditCardNumber();
        var creditCardCvv = _faker.Finance.CreditCardCvv();

        var account = _faker.Finance.Account();
        var accountName = _faker.Finance.AccountName();
        var routingNumber = _faker.Finance.RoutingNumber();

        var amount = _faker.Finance.Amount(100, 10000, 2);

        var currency = _faker.Finance.Currency();

        var iban = _faker.Finance.Iban();
        var bic = _faker.Finance.Bic();

        var bitcoinAddress = _faker.Finance.BitcoinAddress();
        var ethereumAddress = _faker.Finance.EthereumAddress();

        creditCardNumber.Should().NotBeNullOrEmpty();
        amount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Date DataSet

    /// <summary>
    /// Date — past/future, recent/soon, ranges, weekdays, months, DateTimeOffset.
    /// </summary>
    [Fact]
    public void DateDataSet_Dates()
    {
        var past = _faker.Date.Past();                               // within last year
        var pastYears = _faker.Date.Past(5);                         // within last 5 years
        var future = _faker.Date.Future();
        var futureYears = _faker.Date.Future(3);

        var recent = _faker.Date.Recent();                           // last few days
        var recentDays = _faker.Date.Recent(7);
        var soon = _faker.Date.Soon();
        var soonDays = _faker.Date.Soon(14);

        var between = _faker.Date.Between(DateTime.Now.AddYears(-1), DateTime.Now);

        // Birthday: 18-68 years old
        var birthday = _faker.Date.Past(50, DateTime.Now.AddYears(-18));

        var timespan = _faker.Date.Timespan();
        var weekday = _faker.Date.Weekday();
        var month = _faker.Date.Month();

        var pastOffset = _faker.Date.PastOffset();
        var futureOffset = _faker.Date.FutureOffset();

        past.Should().BeBefore(DateTime.Now);
        future.Should().BeAfter(DateTime.Now);
    }

    #endregion

    #region Lorem DataSet

    /// <summary>
    /// Lorem — placeholder text at every granularity (word, sentence, paragraph, text).
    /// </summary>
    [Fact]
    public void LoremDataSet_Text()
    {
        var word = _faker.Lorem.Word();
        var words = _faker.Lorem.Words(5);

        var sentence = _faker.Lorem.Sentence();
        var sentenceWords = _faker.Lorem.Sentence(10);
        var sentences = _faker.Lorem.Sentences(3);

        var paragraph = _faker.Lorem.Paragraph();
        var paragraphSentences = _faker.Lorem.Paragraph(5);
        var paragraphs = _faker.Lorem.Paragraphs(2);

        var text = _faker.Lorem.Text();
        var lines = _faker.Lorem.Lines();

        var slug = _faker.Lorem.Slug();                              // URL-friendly

        var letter = _faker.Lorem.Letter();
        var letters = _faker.Lorem.Letter(10);

        word.Should().NotBeNullOrEmpty();
        sentence.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Phone DataSet

    [Fact]
    public void PhoneDataSet_PhoneNumbers()
    {
        var phoneNumber = _faker.Phone.PhoneNumber();
        var phoneNumberFormat = _faker.Phone.PhoneNumberFormat();

        phoneNumber.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region System DataSet

    /// <summary>
    /// System — file names/extensions, MIME types, paths, versions, push tokens.
    /// </summary>
    [Fact]
    public void SystemDataSet_SystemData()
    {
        var fileName = _faker.System.FileName();
        var commonFileName = _faker.System.CommonFileName();
        var fileExt = _faker.System.FileExt();
        var commonFileExt = _faker.System.CommonFileExt();

        var mimeType = _faker.System.MimeType();
        var commonFileType = _faker.System.CommonFileType();

        var filePath = _faker.System.FilePath();
        var directoryPath = _faker.System.DirectoryPath();

        var version = _faker.System.Version();
        var semver = _faker.System.Semver();

        var androidId = _faker.System.AndroidId();
        var applePushToken = _faker.System.ApplePushToken();

        fileName.Should().NotBeNullOrEmpty();
        mimeType.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Random DataSet

    /// <summary>
    /// Random — primitives, character generators, collection operations, GUIDs, enums.
    /// </summary>
    [Fact]
    public void RandomDataSet_RandomPrimitives()
    {
        var randomInt = _faker.Random.Int(1, 100);
        var randomLong = _faker.Random.Long(1, 1000000);
        var randomDecimal = _faker.Random.Decimal(0, 1000);
        var randomDouble = _faker.Random.Double(0, 100);
        var randomFloat = _faker.Random.Float(0, 10);
        var randomByte = _faker.Random.Byte();
        var randomShort = _faker.Random.Short(0, 1000);

        var randomChar = _faker.Random.Char('a', 'z');
        var randomString = _faker.Random.String(10);
        var randomString2 = _faker.Random.String2(10, "abc123");
        var alphanumeric = _faker.Random.AlphaNumeric(8);

        var randomBool = _faker.Random.Bool();
        var weightedBool = _faker.Random.Bool(0.8f);                 // 80% true

        var randomGuid = _faker.Random.Guid();
        var randomUuid = _faker.Random.Uuid();
        var randomHash = _faker.Random.Hash();
        var randomHexadecimal = _faker.Random.Hexadecimal(16);

        var randomEnum = _faker.Random.Enum<DayOfWeek>();

        var array = new[] { "A", "B", "C", "D", "E" };
        var randomElement = _faker.Random.ArrayElement(array);
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var randomListItem = _faker.Random.ListItem(list);
        var shuffled = _faker.Random.Shuffle(array);

        var randomElements = _faker.Random.ArrayElements(array, 3);

        randomInt.Should().BeInRange(1, 100);
        randomGuid.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region Vehicle DataSet

    [Fact]
    public void VehicleDataSet_VehicleData()
    {
        var manufacturer = _faker.Vehicle.Manufacturer();
        var model = _faker.Vehicle.Model();
        var type = _faker.Vehicle.Type();
        var fuel = _faker.Vehicle.Fuel();
        var vin = _faker.Vehicle.Vin();

        manufacturer.Should().NotBeNullOrEmpty();
        vin.Should().HaveLength(17);
    }

    #endregion

    #region Image DataSet

    /// <summary>
    /// Image — placeholder image URLs (Picsum, LoremFlickr, generic placeholder).
    /// </summary>
    [Fact]
    public void ImageDataSet_ImageUrls()
    {
        var imageUrl = _faker.Image.PicsumUrl();
        var loremFlickr = _faker.Image.LoremFlickrUrl();
        var placeholder = _faker.Image.PlaceholderUrl();

        var abstract_ = _faker.Image.Abstract();
        var animals = _faker.Image.Animals();
        var business = _faker.Image.Business();
        var cats = _faker.Image.Cats();
        var city = _faker.Image.City();
        var food = _faker.Image.Food();
        var nightlife = _faker.Image.Nightlife();
        var fashion = _faker.Image.Fashion();
        var people = _faker.Image.People();
        var nature = _faker.Image.Nature();
        var sports = _faker.Image.Sports();
        var technics = _faker.Image.Technics();
        var transport = _faker.Image.Transport();

        imageUrl.Should().StartWith("http");
    }

    #endregion

    #region Rant DataSet

    [Fact]
    public void RantDataSet_ProductReviews()
    {
        var review = _faker.Rant.Review();
        var reviews = _faker.Rant.Reviews(3);

        review.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Hacker DataSet

    /// <summary>
    /// Hacker — technical sounding noun/verb/adjective tokens.
    /// </summary>
    [Fact]
    public void HackerDataSet_TechnicalJargon()
    {
        var abbreviation = _faker.Hacker.Abbreviation();             // TCP, HTTP...
        var adjective = _faker.Hacker.Adjective();
        var noun = _faker.Hacker.Noun();
        var verb = _faker.Hacker.Verb();
        var ingverb = _faker.Hacker.IngVerb();
        var phrase = _faker.Hacker.Phrase();

        phrase.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Database DataSet

    [Fact]
    public void DatabaseDataSet_DatabaseTokens()
    {
        var column = _faker.Database.Column();
        var type = _faker.Database.Type();
        var collation = _faker.Database.Collation();
        var engine = _faker.Database.Engine();

        column.Should().NotBeNullOrEmpty();
    }

    #endregion
}
