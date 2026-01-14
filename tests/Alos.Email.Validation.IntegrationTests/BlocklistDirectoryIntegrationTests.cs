namespace Alos.Email.Validation.IntegrationTests;

using Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Tests.Shared.Fixtures;

/// <summary>
///   Integration tests for disk-based blocklist directory loading.
///   Tests the BlocklistDirectory option which loads lists from a specific directory.
/// </summary>
public sealed class BlocklistDirectoryIntegrationTests : IDisposable
{
  #region Fields

  private readonly TempFileFixture _fixture;

  #endregion


  #region Constructors

  public BlocklistDirectoryIntegrationTests()
  {
    _fixture = new TempFileFixture();
  }

  #endregion


  #region Tests - Blocklist Directory

  [Fact]
  public void IsDisposable_BlocklistDirectory_LoadsFromDisk()
  {
    // Arrange: Create blocklist and allowlist files in a directory.
    var blocklistDir = _fixture.CreateSubdirectory("blocklists");
    _fixture.CreateBlocklistFile(
      blocklistDir,
      "disk-blocked-1.com",
      "disk-blocked-2.net",
      "disk-blocked-3.org");
    _fixture.CreateAllowlistFile(
      blocklistDir,
      "disk-allowed-1.com");

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Domains from disk blocklist should be detected.
    checker.IsDisposable("disk-blocked-1.com").Should().BeTrue();
    checker.IsDisposable("disk-blocked-2.net").Should().BeTrue();
    checker.IsDisposable("disk-blocked-3.org").Should().BeTrue();

    // Allowlisted domain should NOT be detected.
    checker.IsDisposable("disk-allowed-1.com").Should().BeFalse();

    // Non-listed domain should NOT be detected
    // (note: when using disk-based lists, embedded resources are not loaded).
    checker.IsDisposable("random-unknown.xyz").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_BlocklistDirectory_MissingFiles_UsesEmbedded()
  {
    // Arrange: Create an empty directory (no blocklist files).
    var emptyDir = _fixture.CreateSubdirectory("empty-blocklists");

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = emptyDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Should fall back gracefully - empty sets loaded.
    // Since no files exist, blocklist is empty, so nothing is blocked.
    checker.IsDisposable("mailinator.com").Should().BeFalse();
    checker.IsDisposable("random-domain.com").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_BlocklistDirectory_PartialFiles_MergesAvailable()
  {
    // Arrange: Create only blocklist file, no allowlist.
    var partialDir = _fixture.CreateSubdirectory("partial-blocklists");
    _fixture.CreateBlocklistFile(
      partialDir,
      "partial-blocked.com");
    // Note: No allowlist file created.

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = partialDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Blocklist should work.
    checker.IsDisposable("partial-blocked.com").Should().BeTrue();

    // Non-blocked domains should pass.
    checker.IsDisposable("gmail.com").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_BlocklistDirectory_NonExistent_UsesEmbedded()
  {
    // Arrange: Reference a directory that does not exist.
    var nonExistentDir = _fixture.GetFilePath("non-existent-directory");

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = nonExistentDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Should fall back to embedded resources.
    checker.IsDisposable("mailinator.com").Should().BeTrue();
    checker.IsDisposable("gmail.com").Should().BeFalse();
  }

  #endregion


  #region IDisposable

  public void Dispose()
  {
    _fixture.Dispose();
  }

  #endregion
}
