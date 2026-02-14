namespace Alos.Email.Validation.Tests;

using Configuration;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
///   Tests for <see cref="EmailValidationService"/>.
/// </summary>
public class EmailValidationServiceTests
{
  #region Properties & Fields - Non-Public

  private readonly Mock<IDisposableEmailDomainChecker> _disposableChecker;
  private readonly Mock<IMxRecordValidator> _mxValidator;
  private readonly EmailValidationOptions _options;
  private readonly IEmailValidationService _service;

  #endregion


  #region Constructors

  public EmailValidationServiceTests()
  {
    _disposableChecker = new Mock<IDisposableEmailDomainChecker>();
    _mxValidator = new Mock<IMxRecordValidator>();
    _options = new EmailValidationOptions();

    // Default: not disposable, has MX records
    _disposableChecker.Setup(c => c.IsDisposable(It.IsAny<string>())).Returns(false);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    _service = CreateService();
  }


  /// <summary>
  ///   Creates a service instance with the current options.
  /// </summary>
  private EmailValidationService CreateService()
  {
    return new EmailValidationService(
      _disposableChecker.Object,
      _mxValidator.Object,
      Options.Create(_options));
  }


  /// <summary>
  ///   Creates a service instance with custom options.
  /// </summary>
  private EmailValidationService CreateServiceWithOptions(EmailValidationOptions options)
  {
    return new EmailValidationService(
      _disposableChecker.Object,
      _mxValidator.Object,
      Options.Create(options));
  }

  #endregion


  #region ValidateFormat Tests

  /// <summary>
  ///   Tests valid email formats per HTML5 Living Standard specification.
  ///   Source: https://html.spec.whatwg.org/multipage/input.html#valid-e-mail-address
  /// </summary>
  [Theory]
  // Basic valid formats
  [InlineData("john@gmail.com")]
  [InlineData("john.doe@example.com")]
  [InlineData("john+tag@gmail.com")]
  [InlineData("john.doe+tag@sub.example.com")]
  [InlineData("user@localhost")]
  [InlineData("a@b.c")]
  [InlineData("user123@domain123.com")]
  [InlineData("user.name@domain.co.uk")]
  // Numeric domains
  [InlineData("user@123.com")]
  [InlineData("user@domain1.domain2.com")]
  // Special characters allowed in local part per HTML5 spec (atext chars)
  [InlineData("user!test@example.com")]
  [InlineData("user#test@example.com")]
  [InlineData("user$test@example.com")]
  [InlineData("user%test@example.com")]
  [InlineData("user&test@example.com")]
  [InlineData("user'test@example.com")]
  [InlineData("user*test@example.com")]
  [InlineData("user+test@example.com")]
  [InlineData("user/test@example.com")]
  [InlineData("user=test@example.com")]
  [InlineData("user?test@example.com")]
  [InlineData("user^test@example.com")]
  [InlineData("user_test@example.com")]
  [InlineData("user`test@example.com")]
  [InlineData("user{test@example.com")]
  [InlineData("user|test@example.com")]
  [InlineData("user}test@example.com")]
  [InlineData("user~test@example.com")]
  [InlineData("user-test@example.com")]
  // Combined special characters
  [InlineData("user!#$%&'*+/=?^_`{|}~-test@example.com")]
  // Dots in local part
  [InlineData("first.last@example.com")]
  [InlineData("first.middle.last@example.com")]
  // Single character local part
  [InlineData("a@example.com")]
  // Long but valid local part (up to 64 chars is valid per RFC)
  [InlineData("abcdefghijklmnopqrstuvwxyz1234567890@example.com")]
  // Subdomains
  [InlineData("user@sub.domain.example.com")]
  [InlineData("user@a.b.c.d.e.f.example.com")]
  // TLD variations
  [InlineData("user@example.co")]
  [InlineData("user@example.io")]
  [InlineData("user@example.museum")]
  // HTML5 spec allows these (willful violation of RFC 5322) - dots at start/end and consecutive dots
  // Most email providers reject these, but the HTML5 browser pattern accepts them.
  // We use HTML5 spec for broad compatibility; MX validation will catch truly invalid domains.
  [InlineData(".user@domain.com")]
  [InlineData("user.@domain.com")]
  [InlineData(".user.@domain.com")]
  [InlineData("user..name@domain.com")]
  [InlineData("user...name@domain.com")]
  public void ValidateFormat_ValidEmails_ReturnsTrue(string email)
  {
    var result = _service.ValidateFormat(email);

    result.Should().BeTrue($"'{email}' should be a valid email format per HTML5 spec");
  }


  /// <summary>
  ///   Tests invalid email formats per HTML5 Living Standard specification.
  /// </summary>
  [Theory]
  // Empty/whitespace
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\t")]
  [InlineData("\n")]
  // Missing @ symbol
  [InlineData("notanemail")]
  [InlineData("plainaddress")]
  [InlineData("missing.at.sign.com")]
  // Missing parts
  [InlineData("@missing.local")]
  [InlineData("missing@")]
  [InlineData("@")]
  [InlineData("@@")]
  // Multiple @ symbols
  [InlineData("user@@domain.com")]
  [InlineData("user@domain@domain.com")]
  [InlineData("a@b@c@d.com")]
  // Invalid domain formats
  [InlineData("missing@.com")]
  [InlineData("user@domain..com")]
  [InlineData("user@.domain.com")]
  [InlineData("user@domain.com.")]
  [InlineData("user@-domain.com")]
  [InlineData("user@domain-.com")]
  [InlineData("user@-domain-.com")]
  // Domain label starting/ending with hyphen
  [InlineData("user@sub.-domain.com")]
  [InlineData("user@sub.domain-.com")]
  // Spaces (not allowed)
  [InlineData("user @domain.com")]
  [InlineData("user@ domain.com")]
  [InlineData("user@domain .com")]
  [InlineData(" user@domain.com")]
  [InlineData("user@domain.com ")]
  [InlineData("user name@domain.com")]
  // Characters not allowed in local part per HTML5 (outside atext)
  [InlineData("user(comment)@domain.com")]
  [InlineData("user)test@domain.com")]
  [InlineData("user<test@domain.com")]
  [InlineData("user>test@domain.com")]
  [InlineData("user[test@domain.com")]
  [InlineData("user]test@domain.com")]
  [InlineData("user:test@domain.com")]
  [InlineData("user;test@domain.com")]
  [InlineData("user,test@domain.com")]
  [InlineData("user\\test@domain.com")]
  [InlineData("\"user\"@domain.com")]
  public void ValidateFormat_InvalidEmails_ReturnsFalse(string email)
  {
    var result = _service.ValidateFormat(email);

    result.Should().BeFalse($"'{email}' should be an invalid email format");
  }


  /// <summary>
  ///   Tests null input handling.
  /// </summary>
  [Fact]
  public void ValidateFormat_NullEmail_ReturnsFalse()
  {
    var result = _service.ValidateFormat(null!);

    result.Should().BeFalse();
  }


  /// <summary>
  ///   Tests edge cases for email format validation.
  /// </summary>
  [Theory]
  // Maximum local part length edge cases (64 chars max per RFC 5321)
  [InlineData("a@b.c", true)] // Minimum valid
  [InlineData("1@2.3", true)] // Numeric minimum
  // International-looking but ASCII domains
  [InlineData("user@xn--n3h.com", true)] // Punycode domain (valid ASCII)
  // Numbers in all positions
  [InlineData("123@456.789", true)]
  [InlineData("1user@domain.com", true)]
  [InlineData("user1@1domain.com", true)]
  public void ValidateFormat_EdgeCases_ReturnsExpected(string email, bool expected)
  {
    var result = _service.ValidateFormat(email);

    result.Should().Be(expected, $"'{email}' validation result should be {expected}");
  }

  #endregion


  #region ValidateEmailAsync Tests

  [Fact]
  public async Task ValidateEmailAsync_ValidEmail_ReturnsSuccess()
  {
    var result = await _service.ValidateEmailAsync("john@gmail.com");

    result.IsValid.Should().BeTrue();
    result.Error.Should().BeNull();
  }


  [Theory]
  [InlineData("notanemail")]
  [InlineData("@missing.local")]
  [InlineData("")]
  public async Task ValidateEmailAsync_InvalidFormat_ReturnsInvalidFormat(string email)
  {
    var result = await _service.ValidateEmailAsync(email);

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidFormat);
  }


  [Theory]
  [InlineData("alias@duck.com")]
  [InlineData("alias@mozmail.com")]
  [InlineData("alias@privaterelay.appleid.com")]
  public async Task ValidateEmailAsync_RelayService_ReturnsRelayServiceError(string email)
  {
    var result = await _service.ValidateEmailAsync(email);

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.RelayService);
  }


  [Fact]
  public async Task ValidateEmailAsync_DisposableDomain_ReturnsDisposableError()
  {
    _disposableChecker.Setup(c => c.IsDisposable("mailinator.com")).Returns(true);

    var result = await _service.ValidateEmailAsync("test@mailinator.com");

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.Disposable);
  }


  [Fact]
  public async Task ValidateEmailAsync_NoMxRecords_ReturnsInvalidDomainError()
  {
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("invalid.invalid", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var result = await _service.ValidateEmailAsync("test@invalid.invalid");

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidDomain);
  }


  [Fact]
  public async Task ValidateEmailAsync_WhitelistedMxDomain_SkipsMxCheck()
  {
    // Arrange: Create service with whitelisted domain.
    var options = new EmailValidationOptions
    {
      WhitelistedMxDomains = ["test.local"]
    };
    var service = CreateServiceWithOptions(options);

    // Act: Validate an email with the whitelisted domain.
    var result = await service.ValidateEmailAsync("user@test.local");

    // Assert: Should succeed without MX check.
    result.IsValid.Should().BeTrue();

    // MX validator should NOT be called for whitelisted domain.
    _mxValidator.Verify(
      v => v.HasValidMxRecordsAsync("test.local", It.IsAny<CancellationToken>()),
      Times.Never,
      "MX check should be skipped for whitelisted domains");
  }


  [Fact]
  public async Task ValidateEmailAsync_WhitelistedMxDomain_StillChecksOtherValidation()
  {
    // Arrange: Create service with whitelisted domain that is also disposable.
    var options = new EmailValidationOptions
    {
      WhitelistedMxDomains = ["tempmail.com"]
    };
    var service = CreateServiceWithOptions(options);
    _disposableChecker.Setup(c => c.IsDisposable("tempmail.com")).Returns(true);

    // Act: Validate an email with the whitelisted (but disposable) domain.
    var result = await service.ValidateEmailAsync("user@tempmail.com");

    // Assert: Should fail with Disposable error - MX whitelist doesn't bypass other checks.
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.Disposable);
  }

  #endregion


  #region ValidateMxAsync Tests

  [Fact]
  public async Task ValidateMxAsync_ValidMxRecords_ReturnsSuccess()
  {
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("example.com", It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var result = await _service.ValidateMxAsync("user@example.com");

    result.IsValid.Should().BeTrue();
    result.Error.Should().BeNull();
  }


  [Fact]
  public async Task ValidateMxAsync_NoMxRecords_ReturnsInvalidDomain()
  {
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("invalid.invalid", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var result = await _service.ValidateMxAsync("user@invalid.invalid");

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidDomain);
  }


  [Fact]
  public async Task ValidateMxAsync_NullDomain_ReturnsInvalidFormat()
  {
    // When domain extraction fails (null/empty email), should return InvalidFormat.
    var result = await _service.ValidateMxAsync("notanemail");

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidFormat);
  }


  [Fact]
  public async Task ValidateMxAsync_WhitelistedDomain_SkipsMxCheck()
  {
    // Arrange: Create service with whitelisted domain.
    var options = new EmailValidationOptions
    {
      WhitelistedMxDomains = ["test.local", "itest.alos.local"]
    };
    var service = CreateServiceWithOptions(options);

    // Act: Validate an email with the whitelisted domain.
    var result = await service.ValidateMxAsync("user@itest.alos.local");

    // Assert: Should succeed without MX check.
    result.IsValid.Should().BeTrue();

    // MX validator should NOT be called for whitelisted domain.
    _mxValidator.Verify(
      v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never,
      "MX check should be skipped for whitelisted domains");
  }


  [Fact]
  public async Task ValidateMxAsync_WhitelistIsCaseInsensitive()
  {
    // Arrange: Create service with whitelisted domain in mixed case.
    var options = new EmailValidationOptions
    {
      WhitelistedMxDomains = ["TEST.Local"]
    };
    var service = CreateServiceWithOptions(options);

    // Act: Validate an email with lowercase domain.
    var result = await service.ValidateMxAsync("user@test.local");

    // Assert: Should succeed - whitelist should be case-insensitive.
    result.IsValid.Should().BeTrue();
    _mxValidator.Verify(
      v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }


  [Fact]
  public async Task ValidateMxAsync_DoesNotCheckRelayOrDisposable()
  {
    // ValidateMxAsync should ONLY check MX records, not relay or disposable.
    _disposableChecker.Setup(c => c.IsDisposable("mailinator.com")).Returns(true);

    var result = await _service.ValidateMxAsync("user@mailinator.com");

    // Should succeed (MX check passes) even though domain is disposable.
    result.IsValid.Should().BeTrue();

    // Disposable checker should NOT be called by ValidateMxAsync.
    _disposableChecker.Verify(
      c => c.IsDisposable(It.IsAny<string>()),
      Times.Never,
      "ValidateMxAsync should not check disposable domains");
  }


  [Fact]
  public async Task ValidateMxAsync_CancellationToken_PassedToMxValidator()
  {
    using var cts = new CancellationTokenSource();
    var capturedToken = CancellationToken.None;

    _mxValidator
      .Setup(v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((_, ct) => capturedToken = ct)
      .ReturnsAsync(true);

    await _service.ValidateMxAsync("test@example.com", cts.Token);

    capturedToken.Should().Be(cts.Token);
  }

  #endregion


  #region IsRelayService Tests

  [Theory]
  [InlineData("alias@duck.com", true)]
  [InlineData("alias@mozmail.com", true)]
  [InlineData("john@gmail.com", false)]
  [InlineData("john@company.com", false)]
  public void IsRelayService_ReturnsExpected(string email, bool expected)
  {
    var result = _service.IsRelayService(email);

    result.Should().Be(expected);
  }

  #endregion


  #region IsDisposable Tests

  [Fact]
  public void IsDisposable_DelegatesToChecker()
  {
    _disposableChecker.Setup(c => c.IsDisposable("mailinator.com")).Returns(true);

    var result = _service.IsDisposable("test@mailinator.com");

    result.Should().BeTrue();
    _disposableChecker.Verify(c => c.IsDisposable("mailinator.com"), Times.Once);
  }

  #endregion


  #region Normalize Tests

  [Theory]
  [InlineData("john.doe@gmail.com", "johndoe@gmail.com")]
  [InlineData("john+tag@outlook.com", "john@outlook.com")]
  [InlineData("JOHN@EXAMPLE.COM", "john@example.com")]
  public void Normalize_ReturnsNormalizedEmail(string input, string expected)
  {
    // Normalize is a static method on EmailNormalizer, not an instance method on the service.
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Null Input Tests

  [Fact]
  public async Task ValidateEmailAsync_NullEmail_ReturnsInvalidFormat()
  {
    var result = await _service.ValidateEmailAsync(null!);

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidFormat);
  }


  [Fact]
  public void IsRelayService_NullEmail_ReturnsFalse()
  {
    var result = _service.IsRelayService(null!);

    result.Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_NullEmail_ReturnsFalse()
  {
    var result = _service.IsDisposable(null!);

    result.Should().BeFalse();
  }


  [Fact]
  public void Normalize_NullEmail_ReturnsNull()
  {
    // Normalize is a static method on EmailNormalizer, not an instance method on the service.
    var result = EmailNormalizer.Normalize(null!);

    result.Should().BeNull();
  }

  #endregion


  #region CancellationToken Tests

  [Fact]
  public async Task ValidateEmailAsync_CancellationToken_PassedToMxValidator()
  {
    using var cts = new CancellationTokenSource();
    var capturedToken = CancellationToken.None;

    _mxValidator
      .Setup(v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((_, ct) => capturedToken = ct)
      .ReturnsAsync(true);

    await _service.ValidateEmailAsync("test@example.com", cts.Token);

    capturedToken.Should().Be(cts.Token);
  }


  [Fact]
  public async Task ValidateEmailAsync_CancellationRequested_PropagatesException()
  {
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    _mxValidator
      .Setup(v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    await FluentActions
      .Invoking(() => _service.ValidateEmailAsync("test@example.com", cts.Token))
      .Should().ThrowAsync<OperationCanceledException>();
  }

  #endregion


  #region Validation Order and Precedence Tests

  /// <summary>
  ///   Validates that the service checks format FIRST, before any other checks.
  ///   When format is invalid, relay/disposable/MX checks should NOT be invoked.
  /// </summary>
  [Theory]
  [InlineData("notanemail")]
  [InlineData("@missing.local")]
  [InlineData("")]
  [InlineData("   ")]
  public async Task ValidateEmailAsync_InvalidFormat_DoesNotCheckRelayOrDisposableOrMx(string email)
  {
    var result = await _service.ValidateEmailAsync(email);

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidFormat);

    // Disposable checker should NOT be called (format check short-circuits).
    _disposableChecker.Verify(
      c => c.IsDisposable(It.IsAny<string>()),
      Times.Never,
      "disposable check should not be called when format is invalid");

    // MX validator should NOT be called (format check short-circuits).
    _mxValidator.Verify(
      v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never,
      "MX check should not be called when format is invalid");
  }


  /// <summary>
  ///   Validates that relay service check happens BEFORE disposable check.
  ///   When email is a relay service, disposable/MX checks should NOT be invoked.
  /// </summary>
  [Theory]
  [InlineData("alias@duck.com")]
  [InlineData("alias@mozmail.com")]
  [InlineData("alias@privaterelay.appleid.com")]
  public async Task ValidateEmailAsync_RelayService_DoesNotCheckDisposableOrMx(string email)
  {
    // Even if the domain were disposable, we should never check.
    _disposableChecker.Setup(c => c.IsDisposable(It.IsAny<string>())).Returns(true);

    var result = await _service.ValidateEmailAsync(email);

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.RelayService);

    // Disposable checker should NOT be called (relay check short-circuits).
    _disposableChecker.Verify(
      c => c.IsDisposable(It.IsAny<string>()),
      Times.Never,
      "disposable check should not be called for relay services");

    // MX validator should NOT be called (relay check short-circuits).
    _mxValidator.Verify(
      v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never,
      "MX check should not be called for relay services");
  }


  /// <summary>
  ///   Validates that disposable check happens BEFORE MX check.
  ///   When email is disposable, MX check should NOT be invoked (saves DNS query).
  /// </summary>
  [Fact]
  public async Task ValidateEmailAsync_DisposableDomain_DoesNotCheckMx()
  {
    _disposableChecker.Setup(c => c.IsDisposable("tempmail.com")).Returns(true);

    var result = await _service.ValidateEmailAsync("user@tempmail.com");

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.Disposable);

    // MX validator should NOT be called (disposable check short-circuits).
    _mxValidator.Verify(
      v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never,
      "MX check should not be called for disposable domains");
  }


  /// <summary>
  ///   Validates that MX check is the LAST validation step.
  ///   If email passes format, relay, and disposable checks, MX is checked.
  /// </summary>
  [Fact]
  public async Task ValidateEmailAsync_ValidEmailPassesAllChecks_ChecksMxLast()
  {
    _disposableChecker.Setup(c => c.IsDisposable("example.com")).Returns(false);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("example.com", It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var result = await _service.ValidateEmailAsync("user@example.com");

    result.IsValid.Should().BeTrue();

    // All checks were invoked in order.
    _disposableChecker.Verify(c => c.IsDisposable("example.com"), Times.Once);
    _mxValidator.Verify(v => v.HasValidMxRecordsAsync("example.com", It.IsAny<CancellationToken>()), Times.Once);
  }


  /// <summary>
  ///   Validates that when an email fails multiple checks, the FIRST error is returned.
  ///   Order: InvalidFormat > RelayService > Disposable > InvalidDomain
  /// </summary>
  [Fact]
  public async Task ValidateEmailAsync_EmailFailsMultipleChecks_ReturnsFirstError()
  {
    // Domain would fail both disposable and MX checks.
    _disposableChecker.Setup(c => c.IsDisposable("bad-domain.invalid")).Returns(true);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("bad-domain.invalid", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var result = await _service.ValidateEmailAsync("user@bad-domain.invalid");

    // Should return Disposable error (checked before MX).
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.Disposable);

    // MX validator should NOT be called due to short-circuiting.
    _mxValidator.Verify(
      v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }


  /// <summary>
  ///   Validates the complete validation order with a domain that would fail all checks.
  /// </summary>
  [Fact]
  public async Task ValidateEmailAsync_ValidationOrderIsFormatThenRelayThenDisposableThenMx()
  {
    // Test 1: Invalid format stops immediately.
    var formatResult = await _service.ValidateEmailAsync("invalid");
    formatResult.Error.Should().Be(EmailValidationError.InvalidFormat);

    // Test 2: Valid format but relay service stops at relay check.
    var relayResult = await _service.ValidateEmailAsync("test@duck.com");
    relayResult.Error.Should().Be(EmailValidationError.RelayService);

    // Test 3: Valid format, not relay, but disposable stops at disposable check.
    _disposableChecker.Setup(c => c.IsDisposable("mailinator.com")).Returns(true);
    var disposableResult = await _service.ValidateEmailAsync("test@mailinator.com");
    disposableResult.Error.Should().Be(EmailValidationError.Disposable);

    // Test 4: Valid format, not relay, not disposable, but no MX stops at MX check.
    _disposableChecker.Setup(c => c.IsDisposable("no-mx.invalid")).Returns(false);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("no-mx.invalid", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);
    var mxResult = await _service.ValidateEmailAsync("test@no-mx.invalid");
    mxResult.Error.Should().Be(EmailValidationError.InvalidDomain);

    // Test 5: All checks pass.
    _disposableChecker.Setup(c => c.IsDisposable("valid.com")).Returns(false);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("valid.com", It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);
    var validResult = await _service.ValidateEmailAsync("test@valid.com");
    validResult.IsValid.Should().BeTrue();
  }


  /// <summary>
  ///   Validates that the domain extraction is done once and passed to all checks.
  /// </summary>
  [Fact]
  public async Task ValidateEmailAsync_ExtractsDomainOnce_PassesToAllChecks()
  {
    string? domainPassedToDisposable = null;
    string? domainPassedToMx = null;

    _disposableChecker
      .Setup(c => c.IsDisposable(It.IsAny<string>()))
      .Callback<string?>(d => domainPassedToDisposable = d)
      .Returns(false);

    _mxValidator
      .Setup(v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((d, _) => domainPassedToMx = d)
      .ReturnsAsync(true);

    await _service.ValidateEmailAsync("User.Name+Tag@EXAMPLE.COM");

    // Both checks received the same extracted domain (lowercase).
    domainPassedToDisposable.Should().Be("example.com");
    domainPassedToMx.Should().Be("example.com");
  }

  #endregion


  #region ObjectDisposedException Propagation Tests

  [Fact]
  public async Task ValidateEmailAsync_DisposedChecker_PropagatesObjectDisposedException()
  {
    _disposableChecker
      .Setup(c => c.IsDisposable(It.IsAny<string>()))
      .Throws(new ObjectDisposedException(nameof(DisposableEmailDomainChecker)));

    await FluentActions
      .Invoking(() => _service.ValidateEmailAsync("test@example.com"))
      .Should().ThrowAsync<ObjectDisposedException>()
      .WithMessage("*DisposableEmailDomainChecker*");
  }


  [Fact]
  public void IsDisposable_DisposedChecker_PropagatesObjectDisposedException()
  {
    _disposableChecker
      .Setup(c => c.IsDisposable(It.IsAny<string>()))
      .Throws(new ObjectDisposedException(nameof(DisposableEmailDomainChecker)));

    FluentActions
      .Invoking(() => _service.IsDisposable("test@example.com"))
      .Should().Throw<ObjectDisposedException>()
      .WithMessage("*DisposableEmailDomainChecker*");
  }

  #endregion
}
