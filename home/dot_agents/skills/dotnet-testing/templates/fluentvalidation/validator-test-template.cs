// FluentValidation testing template.
// Shows basic validator-test patterns covering every rule kind.

using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FluentValidationTestingExample;

// ==================== Test model ====================

public class UserRegistrationRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool AgreeToTerms { get; set; }
}

// ==================== Validator under test ====================

public class UserRegistrationValidator : AbstractValidator<UserRegistrationRequest>
{
    private readonly TimeProvider _timeProvider;

    public UserRegistrationValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        SetupValidationRules();
    }

    private void SetupValidationRules()
    {
        // Username
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username must not be null or empty")
            .Length(3, 20).WithMessage("Username length must be between 3 and 20")
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("Username may only contain letters, digits, and underscore");

        // Email
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email must not be null or empty")
            .EmailAddress().WithMessage("Email is not in a valid format")
            .MaximumLength(100).WithMessage("Email length must not exceed 100 characters");

        // Password
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password must not be null or empty")
            .Length(8, 50).WithMessage("Password length must be between 8 and 50")
            .Must(BeComplexPassword).WithMessage("Password must contain upper, lower, and digit");

        // Confirm password
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords must match");

        // Age
        RuleFor(x => x.Age)
            .GreaterThanOrEqualTo(18).WithMessage("Age must be >= 18")
            .LessThanOrEqualTo(120).WithMessage("Age must be <= 120");

        // Birth date / age consistency
        RuleFor(x => x.BirthDate)
            .Must((request, birthDate) => IsAgeConsistentWithBirthDate(birthDate, request.Age))
            .WithMessage("Birth date and age do not match");

        // Optional phone number
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^09\d{8}$").WithMessage("Phone number format is invalid")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        // Roles
        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("Roles must not be empty")
            .Must(roles => roles == null || roles.All(IsValidRole))
            .WithMessage("Contains an invalid role");

        // Terms
        RuleFor(x => x.AgreeToTerms)
            .Equal(true).WithMessage("You must agree to the terms");
    }

    private bool BeComplexPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        return Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$");
    }

    private bool IsAgeConsistentWithBirthDate(DateTime birthDate, int age)
    {
        var currentDate = _timeProvider.GetLocalNow().Date;
        var calculatedAge = currentDate.Year - birthDate.Year;

        if (birthDate.Date > currentDate.AddYears(-calculatedAge))
            calculatedAge--;

        return calculatedAge == age;
    }

    private bool IsValidRole(string role)
    {
        var validRoles = new[] { "User", "Admin", "Manager", "Support" };
        return validRoles.Contains(role);
    }
}

// ==================== Tests ====================

public class UserRegistrationValidatorTests
{
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly UserRegistrationValidator _sut;

    public UserRegistrationValidatorTests()
    {
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTime(2024, 1, 1));

        _sut = new UserRegistrationValidator(_fakeTimeProvider);
    }

    // ==================== Username ====================

    [Fact]
    public void Validate_ValidUsername_ShouldPass()
    {
        var request = CreateValidRequest();
        request.Username = "valid_user123";

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyUsername_ShouldFail(string username)
    {
        var request = CreateValidRequest();
        request.Username = username;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username must not be null or empty");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public void Validate_TooShortUsername_ShouldFail(string username)
    {
        var request = CreateValidRequest();
        request.Username = username;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username length must be between 3 and 20");
    }

    [Fact]
    public void Validate_TooLongUsername_ShouldFail()
    {
        var request = CreateValidRequest();
        request.Username = "a_very_long_username_that_exceeds_limit";

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username length must be between 3 and 20");
    }

    [Theory]
    [InlineData("user@name")]
    [InlineData("user-name")]
    [InlineData("user name")]
    [InlineData("user#123")]
    public void Validate_UsernameWithSpecialCharacters_ShouldFail(string username)
    {
        var request = CreateValidRequest();
        request.Username = username;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username may only contain letters, digits, and underscore");
    }

    // ==================== Email ====================

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyEmail_ShouldFail(string email)
    {
        var request = CreateValidRequest();
        request.Email = email;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email must not be null or empty");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user name@example.com")]
    public void Validate_InvalidEmailFormat_ShouldFail(string email)
    {
        var request = CreateValidRequest();
        request.Email = email;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email is not in a valid format");
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.user@example.com")]
    [InlineData("user+tag@example.co.uk")]
    public void Validate_ValidEmail_ShouldPass(string email)
    {
        var request = CreateValidRequest();
        request.Email = email;

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // ==================== Password ====================

    [Theory]
    [InlineData("weak")]
    [InlineData("weakpass")]
    [InlineData("WEAKPASS123")]
    [InlineData("weakpass123")]
    [InlineData("WeakPass")]
    public void Validate_WeakPassword_ShouldFail(string password)
    {
        var request = CreateValidRequest();
        request.Password = password;
        request.ConfirmPassword = password;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("StrongPass123")]
    [InlineData("MyP@ssw0rd")]
    [InlineData("Test1234Aa")]
    public void Validate_StrongPassword_ShouldPass(string password)
    {
        var request = CreateValidRequest();
        request.Password = password;
        request.ConfirmPassword = password;

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_MismatchedConfirmPassword_ShouldFail()
    {
        var request = CreateValidRequest();
        request.Password = "Password123";
        request.ConfirmPassword = "DifferentPass456";

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
              .WithErrorMessage("Passwords must match");
    }

    // ==================== Age ====================

    [Theory]
    [InlineData(17)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_AgeBelowMinimum_ShouldFail(int age)
    {
        var request = CreateValidRequest();
        request.Age = age;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Age)
              .WithErrorMessage("Age must be >= 18");
    }

    [Theory]
    [InlineData(121)]
    [InlineData(150)]
    public void Validate_AgeAboveMaximum_ShouldFail(int age)
    {
        var request = CreateValidRequest();
        request.Age = age;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Age)
              .WithErrorMessage("Age must be <= 120");
    }

    [Theory]
    [InlineData(18)]
    [InlineData(30)]
    [InlineData(120)]
    public void Validate_ValidAge_ShouldPass(int age)
    {
        var request = CreateValidRequest();
        request.Age = age;

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Age);
    }

    // ==================== Birth date / age consistency ====================

    [Fact]
    public void Validate_BirthDateMatchesAge_ShouldPass()
    {
        var request = CreateValidRequest();
        request.BirthDate = new DateTime(1990, 1, 1);
        request.Age = 34;

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.BirthDate);
    }

    [Fact]
    public void Validate_BirthDateMismatchesAge_ShouldFail()
    {
        var request = CreateValidRequest();
        request.BirthDate = new DateTime(1990, 1, 1);
        request.Age = 25;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.BirthDate)
              .WithErrorMessage("Birth date and age do not match");
    }

    [Fact]
    public void Validate_BirthdayLaterInYear_AgeDecrements()
    {
        _fakeTimeProvider.SetUtcNow(new DateTime(2024, 2, 1));
        var sut = new UserRegistrationValidator(_fakeTimeProvider);

        var request = CreateValidRequest();
        request.BirthDate = new DateTime(1990, 6, 15);
        request.Age = 33;

        var result = sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.BirthDate);
    }

    // ==================== Phone (conditional rule) ====================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyPhoneNumber_ShouldSkipValidation(string phoneNumber)
    {
        var request = CreateValidRequest();
        request.PhoneNumber = phoneNumber;

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("0812345678")]
    [InlineData("091234567")]
    [InlineData("09123456789")]
    public void Validate_InvalidPhoneNumber_ShouldFail(string phoneNumber)
    {
        var request = CreateValidRequest();
        request.PhoneNumber = phoneNumber;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber)
              .WithErrorMessage("Phone number format is invalid");
    }

    [Theory]
    [InlineData("0912345678")]
    [InlineData("0987654321")]
    [InlineData("0900000000")]
    public void Validate_ValidPhoneNumber_ShouldPass(string phoneNumber)
    {
        var request = CreateValidRequest();
        request.PhoneNumber = phoneNumber;

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    // ==================== Roles ====================

    [Fact]
    public void Validate_EmptyRoleList_ShouldFail()
    {
        var request = CreateValidRequest();
        request.Roles = new List<string>();

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Roles)
              .WithErrorMessage("Roles must not be empty");
    }

    [Theory]
    [InlineData("InvalidRole")]
    [InlineData("SuperUser")]
    [InlineData("Guest")]
    public void Validate_InvalidRole_ShouldFail(string invalidRole)
    {
        var request = CreateValidRequest();
        request.Roles = new List<string> { "User", invalidRole };

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Roles)
              .WithErrorMessage("Contains an invalid role");
    }

    [Fact]
    public void Validate_ValidRoleCombination_ShouldPass()
    {
        var request = CreateValidRequest();
        request.Roles = new List<string> { "User", "Admin", "Manager" };

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Roles);
    }

    // ==================== AgreeToTerms ====================

    [Fact]
    public void Validate_DidNotAgreeToTerms_ShouldFail()
    {
        var request = CreateValidRequest();
        request.AgreeToTerms = false;

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AgreeToTerms)
              .WithErrorMessage("You must agree to the terms");
    }

    [Fact]
    public void Validate_AgreedToTerms_ShouldPass()
    {
        var request = CreateValidRequest();
        request.AgreeToTerms = true;

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.AgreeToTerms);
    }

    // ==================== Whole-object validation ====================

    [Fact]
    public void Validate_FullyValidRequest_ShouldPass()
    {
        var request = CreateValidRequest();

        var result = _sut.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ==================== Helpers ====================

    /// <summary>
    /// Builds a known-valid request as a baseline; each test mutates only the
    /// field under exercise so failures localise cleanly.
    /// </summary>
    private UserRegistrationRequest CreateValidRequest() => new()
    {
        Username = "testuser123",
        Email = "test@example.com",
        Password = "TestPass123",
        ConfirmPassword = "TestPass123",
        BirthDate = new DateTime(1990, 1, 1),
        Age = 34,
        PhoneNumber = "0912345678",
        Roles = new List<string> { "User" },
        AgreeToTerms = true
    };
}
