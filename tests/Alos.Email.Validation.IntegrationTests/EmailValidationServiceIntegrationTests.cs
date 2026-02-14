namespace Alos.Email.Validation.IntegrationTests;

using Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Fixtures;

/// <summary>
///   End-to-end integration tests for <see cref="EmailValidationService"/>.
///   Tests the complete validation pipeline with real dependencies (no mocks).
/// </summary>
public sealed class EmailValidationServiceIntegrationTests : IDisposable
{
  #region Fields

  private readonly TempFileFixture _fixture;

  #endregion


  #region Constructors

  public EmailValidationServiceIntegrationTests()
  {
    _fixture = new TempFileFixture();
  }

  #endregion


  #region Tests - End-to-End Validation

  [Fact]
  public async Task ValidateEmailAsync_EndToEnd_WithRealDependencies()
  {
    // Arrange: Build full service container with real implementations.
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    using var provider = services.BuildServiceProvider();
    var validationService = provider.GetRequiredService<IEmailValidationService>();

    // Act & Assert: Valid email with real domain.
    // Note: This test uses gmail.com which should have real MX records.
    // However, in CI environments, DNS might be restricted, so we verify
    // the service works without exceptions rather than specific results.
    var result = await validationService.ValidateEmailAsync("test@gmail.com");

    // The result should be one of the valid outcomes (not throw).
    result.Should().NotBeNull();

    // Either valid, or InvalidDomain if DNS fails in CI environment.
    if (!result.IsValid)
    {
      result.Error.Should().Be(EmailValidationError.InvalidDomain);
    }
  }


  [Fact]
  public async Task ValidateEmailAsync_EndToEnd_DisposableEmail_Rejected()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    using var provider = services.BuildServiceProvider();
    var validationService = provider.GetRequiredService<IEmailValidationService>();

    // Act
    var result = await validationService.ValidateEmailAsync("test@mailinator.com");

    // Assert: Should be rejected as disposable (before MX check).
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.Disposable);
  }


  [Fact]
  public async Task ValidateEmailAsync_EndToEnd_RelayService_Rejected()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    using var provider = services.BuildServiceProvider();
    var validationService = provider.GetRequiredService<IEmailValidationService>();

    // Act
    var result = await validationService.ValidateEmailAsync("test@duck.com");

    // Assert: Should be rejected as relay service (before disposable check).
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.RelayService);
  }


  [Fact]
  public async Task ValidateEmailAsync_EndToEnd_InvalidFormat_Rejected()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    using var provider = services.BuildServiceProvider();
    var validationService = provider.GetRequiredService<IEmailValidationService>();

    // Act
    var result = await validationService.ValidateEmailAsync("invalid-email-format");

    // Assert: Should be rejected for invalid format.
    result.IsValid.Should().BeFalse();
    result.Error.Should().Be(EmailValidationError.InvalidFormat);
  }

  #endregion


  #region Tests - End-to-End with Custom Lists

  [Fact]
  public async Task ValidateEmailAsync_EndToEnd_InlineCustomLists_Applied()
  {
    // Arrange: Configure with inline custom blocklist and allowlist.
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation(options =>
    {
      options.CustomBlocklist = ["custom-blocked-for-test.com"];
      options.CustomAllowlist = ["mailinator.com"]; // Override built-in blocklist.
    });

    using var provider = services.BuildServiceProvider();
    var validationService = provider.GetRequiredService<IEmailValidationService>();

    // Act & Assert: Custom blocked domain.
    var blockedResult = await validationService.ValidateEmailAsync("test@custom-blocked-for-test.com");
    blockedResult.IsValid.Should().BeFalse();
    blockedResult.Error.Should().Be(EmailValidationError.Disposable);

    // Act & Assert: Custom allowlisted domain (normally blocked).
    var allowedResult = await validationService.ValidateEmailAsync("test@mailinator.com");

    // Note: mailinator.com is on the allowlist, so it won't be rejected as disposable.
    // However, it might still fail MX validation in some environments.
    allowedResult.Error.Should().NotBe(EmailValidationError.Disposable);
  }


  [Fact]
  public async Task ValidateEmailAsync_EndToEnd_CustomBlocklistFilesFromDirectory_Applied()
  {
    // Arrange: Create custom list files in the BlocklistDirectory using naming conventions.
    var blocklistDir = _fixture.CreateSubdirectory("e2e-custom-files");

    // Create primary blocklist.
    _fixture.CreateBlocklistFile(blocklistDir, "primary-blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    // Create custom blocklist file with naming convention.
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_blocklist_001.conf"),
      ["e2e-blocked-domain.com"]);

    // Create custom allowlist file with naming convention (override built-in).
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_allowlist_001.conf"),
      ["primary-blocked.com"]);

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation(options =>
    {
      options.BlocklistDirectory = blocklistDir;
    });

    using var provider = services.BuildServiceProvider();
    var validationService = provider.GetRequiredService<IEmailValidationService>();

    // Act & Assert: Domain from custom blocklist file should be blocked.
    var blockedResult = await validationService.ValidateEmailAsync("test@e2e-blocked-domain.com");
    blockedResult.IsValid.Should().BeFalse();
    blockedResult.Error.Should().Be(EmailValidationError.Disposable);

    // Act & Assert: Domain from custom allowlist file (override primary blocklist).
    var allowedResult = await validationService.ValidateEmailAsync("test@primary-blocked.com");
    allowedResult.Error.Should().NotBe(EmailValidationError.Disposable);
  }

  #endregion


  #region Tests - Normalization

  [Fact]
  public void Normalize_EndToEnd_GmailNormalization()
  {
    // Act: Normalize a Gmail address with dots and plus tag.
    // Normalize is a static method on EmailNormalizer — no DI needed.
    var normalized = EmailNormalizer.Normalize("Test.User+tag@GMAIL.COM");

    // Assert: The normalized email should be lowercase with dots and plus removed.
    normalized.Should().Be("testuser@gmail.com");
  }

  #endregion


  #region IDisposable

  public void Dispose()
  {
    _fixture.Dispose();
  }

  #endregion
}
