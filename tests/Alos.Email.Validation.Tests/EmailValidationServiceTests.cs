namespace Alos.Email.Validation.Tests;

using Moq;

/// <summary>
///   Tests for <see cref="EmailValidationService"/>.
/// </summary>
public class EmailValidationServiceTests
{
  private readonly Mock<IDisposableEmailDomainChecker> _disposableChecker;
  private readonly Mock<IMxRecordValidator> _mxValidator;
  private readonly IEmailValidationService _service;


  public EmailValidationServiceTests()
  {
    _disposableChecker = new Mock<IDisposableEmailDomainChecker>();
    _mxValidator = new Mock<IMxRecordValidator>();

    // Default: not disposable, has MX records
    _disposableChecker.Setup(c => c.IsDisposable(It.IsAny<string>())).Returns(false);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    _service = new EmailValidationService(_disposableChecker.Object, _mxValidator.Object);
  }


  #region ValidateAsync Tests

  [Fact]
  public async Task ValidateAsync_ValidEmail_ReturnsSuccess()
  {
    var result = await _service.ValidateAsync("john@gmail.com");

    result.IsValid.Should().BeTrue();
    result.Error.Should().BeNull();
  }


  [Theory]
  [InlineData("notanemail")]
  [InlineData("@missing.local")]
  [InlineData("")]
  public async Task ValidateAsync_InvalidFormat_ReturnsInvalidFormat(string email)
  {
    var result = await _service.ValidateAsync(email);

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidFormat);
  }


  [Theory]
  [InlineData("alias@duck.com")]
  [InlineData("alias@mozmail.com")]
  [InlineData("alias@privaterelay.appleid.com")]
  public async Task ValidateAsync_RelayService_ReturnsRelayServiceError(string email)
  {
    var result = await _service.ValidateAsync(email);

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.RelayService);
  }


  [Fact]
  public async Task ValidateAsync_DisposableDomain_ReturnsDisposableError()
  {
    _disposableChecker.Setup(c => c.IsDisposable("mailinator.com")).Returns(true);

    var result = await _service.ValidateAsync("test@mailinator.com");

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.Disposable);
  }


  [Fact]
  public async Task ValidateAsync_NoMxRecords_ReturnsInvalidDomainError()
  {
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("invalid.invalid", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var result = await _service.ValidateAsync("test@invalid.invalid");

    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidDomain);
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
    var result = _service.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Null Input Tests

  [Fact]
  public async Task ValidateAsync_NullEmail_ReturnsInvalidFormat()
  {
    // Act
    var result = await _service.ValidateAsync(null!);

    // Assert: Should return InvalidFormat, not throw NullReferenceException.
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidFormat);
  }


  [Fact]
  public void IsRelayService_NullEmail_ReturnsFalse()
  {
    // Act
    var result = _service.IsRelayService(null!);

    // Assert: Should return false, not throw.
    result.Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_NullEmail_ReturnsFalse()
  {
    // Act
    var result = _service.IsDisposable(null!);

    // Assert: Should return false, not throw.
    result.Should().BeFalse();
  }


  [Fact]
  public void Normalize_NullEmail_ReturnsNull()
  {
    // Act
    var result = _service.Normalize(null!);

    // Assert: Should return null, not throw.
    result.Should().BeNull();
  }

  #endregion


  #region CancellationToken Tests

  [Fact]
  public async Task ValidateAsync_CancellationToken_PassedToMxValidator()
  {
    // Arrange
    using var cts = new CancellationTokenSource();
    var capturedToken = CancellationToken.None;

    _mxValidator
      .Setup(v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((_, ct) => capturedToken = ct)
      .ReturnsAsync(true);

    // Act
    await _service.ValidateAsync("test@example.com", cts.Token);

    // Assert: The token should have been forwarded to the MX validator.
    capturedToken.Should().Be(cts.Token);
  }


  [Fact]
  public async Task ValidateAsync_CancellationRequested_PropagatesException()
  {
    // Arrange
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    _mxValidator
      .Setup(v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    // Act & Assert: Should propagate OperationCanceledException.
    await FluentActions
      .Invoking(() => _service.ValidateAsync("test@example.com", cts.Token))
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
  public async Task ValidateAsync_InvalidFormat_DoesNotCheckRelayOrDisposableOrMx(string email)
  {
    // Act
    var result = await _service.ValidateAsync(email);

    // Assert: Should return InvalidFormat error.
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidFormat);

    // Assert: Disposable checker should NOT be called (format check short-circuits).
    _disposableChecker.Verify(
      c => c.IsDisposable(It.IsAny<string>()),
      Times.Never,
      "disposable check should not be called when format is invalid");

    // Assert: MX validator should NOT be called (format check short-circuits).
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
  public async Task ValidateAsync_RelayService_DoesNotCheckDisposableOrMx(string email)
  {
    // Arrange: Set up disposable checker to track if it's called.
    // Even if the domain were disposable, we should never check.
    _disposableChecker.Setup(c => c.IsDisposable(It.IsAny<string>())).Returns(true);

    // Act
    var result = await _service.ValidateAsync(email);

    // Assert: Should return RelayService error.
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.RelayService);

    // Assert: Disposable checker should NOT be called (relay check short-circuits).
    _disposableChecker.Verify(
      c => c.IsDisposable(It.IsAny<string>()),
      Times.Never,
      "disposable check should not be called for relay services");

    // Assert: MX validator should NOT be called (relay check short-circuits).
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
  public async Task ValidateAsync_DisposableDomain_DoesNotCheckMx()
  {
    // Arrange: Set up domain as disposable.
    _disposableChecker.Setup(c => c.IsDisposable("tempmail.com")).Returns(true);

    // Act
    var result = await _service.ValidateAsync("user@tempmail.com");

    // Assert: Should return Disposable error.
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.Disposable);

    // Assert: MX validator should NOT be called (disposable check short-circuits).
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
  public async Task ValidateAsync_ValidEmailPassesAllChecks_ChecksMxLast()
  {
    // Arrange: All checks pass.
    _disposableChecker.Setup(c => c.IsDisposable("example.com")).Returns(false);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("example.com", It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    // Act
    var result = await _service.ValidateAsync("user@example.com");

    // Assert: Should succeed.
    result.IsValid.Should().BeTrue();

    // Assert: All checks were invoked in order.
    _disposableChecker.Verify(c => c.IsDisposable("example.com"), Times.Once);
    _mxValidator.Verify(v => v.HasValidMxRecordsAsync("example.com", It.IsAny<CancellationToken>()), Times.Once);
  }


  /// <summary>
  ///   Validates that when an email fails multiple checks, the FIRST error is returned.
  ///   Order: InvalidFormat > RelayService > Disposable > InvalidDomain
  /// </summary>
  [Fact]
  public async Task ValidateAsync_EmailFailsMultipleChecks_ReturnsFirstError()
  {
    // Arrange: Domain would fail both disposable and MX checks.
    _disposableChecker.Setup(c => c.IsDisposable("bad-domain.invalid")).Returns(true);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("bad-domain.invalid", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    // Act
    var result = await _service.ValidateAsync("user@bad-domain.invalid");

    // Assert: Should return Disposable error (checked before MX).
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.Disposable);

    // Assert: MX validator should NOT be called due to short-circuiting.
    _mxValidator.Verify(
      v => v.HasValidMxRecordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }


  /// <summary>
  ///   Validates the complete validation order with a domain that would fail all checks.
  /// </summary>
  [Fact]
  public async Task ValidateAsync_ValidationOrderIsFormatThenRelayThenDisposableThenMx()
  {
    // Test 1: Invalid format stops immediately.
    var formatResult = await _service.ValidateAsync("invalid");
    formatResult.Error.Should().Be(EmailValidationError.InvalidFormat);

    // Test 2: Valid format but relay service stops at relay check.
    var relayResult = await _service.ValidateAsync("test@duck.com");
    relayResult.Error.Should().Be(EmailValidationError.RelayService);

    // Test 3: Valid format, not relay, but disposable stops at disposable check.
    _disposableChecker.Setup(c => c.IsDisposable("mailinator.com")).Returns(true);
    var disposableResult = await _service.ValidateAsync("test@mailinator.com");
    disposableResult.Error.Should().Be(EmailValidationError.Disposable);

    // Test 4: Valid format, not relay, not disposable, but no MX stops at MX check.
    _disposableChecker.Setup(c => c.IsDisposable("no-mx.invalid")).Returns(false);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("no-mx.invalid", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);
    var mxResult = await _service.ValidateAsync("test@no-mx.invalid");
    mxResult.Error.Should().Be(EmailValidationError.InvalidDomain);

    // Test 5: All checks pass.
    _disposableChecker.Setup(c => c.IsDisposable("valid.com")).Returns(false);
    _mxValidator.Setup(v => v.HasValidMxRecordsAsync("valid.com", It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);
    var validResult = await _service.ValidateAsync("test@valid.com");
    validResult.IsValid.Should().BeTrue();
  }


  /// <summary>
  ///   Validates that the domain extraction is done once and passed to all checks.
  /// </summary>
  [Fact]
  public async Task ValidateAsync_ExtractsDomainOnce_PassesToAllChecks()
  {
    // Arrange: Track the domain passed to each check.
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

    // Act
    await _service.ValidateAsync("User.Name+Tag@EXAMPLE.COM");

    // Assert: Both checks received the same extracted domain (lowercase).
    domainPassedToDisposable.Should().Be("example.com");
    domainPassedToMx.Should().Be("example.com");
  }

  #endregion


  #region ObjectDisposedException Propagation Tests

  [Fact]
  public async Task ValidateAsync_DisposedChecker_PropagatesObjectDisposedException()
  {
    // Arrange: Simulate a disposed checker.
    _disposableChecker
      .Setup(c => c.IsDisposable(It.IsAny<string>()))
      .Throws(new ObjectDisposedException(nameof(DisposableEmailDomainChecker)));

    // Act & Assert: ObjectDisposedException should propagate up.
    await FluentActions
      .Invoking(() => _service.ValidateAsync("test@example.com"))
      .Should().ThrowAsync<ObjectDisposedException>()
      .WithMessage("*DisposableEmailDomainChecker*");
  }


  [Fact]
  public void IsDisposable_DisposedChecker_PropagatesObjectDisposedException()
  {
    // Arrange: Simulate a disposed checker.
    _disposableChecker
      .Setup(c => c.IsDisposable(It.IsAny<string>()))
      .Throws(new ObjectDisposedException(nameof(DisposableEmailDomainChecker)));

    // Act & Assert: ObjectDisposedException should propagate up.
    FluentActions
      .Invoking(() => _service.IsDisposable("test@example.com"))
      .Should().Throw<ObjectDisposedException>()
      .WithMessage("*DisposableEmailDomainChecker*");
  }

  #endregion
}
