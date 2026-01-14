namespace Alos.Email.Validation.IntegrationTests;

using Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Tests.Shared.Fixtures;

/// <summary>
///   Integration tests for loading custom blocklist and allowlist files from disk.
///   Tests file loading using the naming conventions (custom_blocklist_*.conf, custom_allowlist_*.conf).
/// </summary>
public sealed class CustomListFileIntegrationTests : IDisposable
{
  #region Fields

  private readonly TempFileFixture _fixture;

  #endregion


  #region Constructors

  public CustomListFileIntegrationTests()
  {
    _fixture = new TempFileFixture();
  }

  #endregion


  #region Tests - Custom Blocklist Files

  [Fact]
  public void IsDisposable_CustomBlocklistFile_LoadedAndMerged()
  {
    // Arrange: Create a custom blocklist file with the naming convention in BlocklistDirectory.
    var blocklistDir = _fixture.CreateSubdirectory("custom-blocklist-test");

    // Create primary blocklist.
    _fixture.CreateBlocklistFile(blocklistDir, "primary-blocked.com");

    // Create custom blocklist with naming convention (custom_blocklist_*.conf).
    var customBlocklistPath = Path.Combine(blocklistDir, "custom_blocklist_001.conf");
    File.WriteAllLines(customBlocklistPath, ["file-blocked-domain-1.com", "file-blocked-domain-2.net"]);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Custom blocked domains from file should be detected.
    checker.IsDisposable("file-blocked-domain-1.com").Should().BeTrue();
    checker.IsDisposable("file-blocked-domain-2.net").Should().BeTrue();

    // Primary blocklist should also work.
    checker.IsDisposable("primary-blocked.com").Should().BeTrue();
  }


  [Fact]
  public void IsDisposable_CustomAllowlistFile_LoadedAndMerged()
  {
    // Arrange: Create a custom allowlist file that allows a blocked domain.
    var blocklistDir = _fixture.CreateSubdirectory("custom-allowlist-test");

    // Create primary blocklist with some blocked domains.
    _fixture.CreateBlocklistFile(blocklistDir, "to-be-allowed.com", "still-blocked.com");

    // Create custom allowlist with naming convention (custom_allowlist_*.conf).
    var customAllowlistPath = Path.Combine(blocklistDir, "custom_allowlist_001.conf");
    File.WriteAllLines(customAllowlistPath, ["to-be-allowed.com"]);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Allowlisted domain should NOT be detected as disposable.
    checker.IsDisposable("to-be-allowed.com").Should().BeFalse();

    // Non-allowlisted blocked domain should still be blocked.
    checker.IsDisposable("still-blocked.com").Should().BeTrue();
  }


  [Fact]
  public void IsDisposable_MultipleCustomFiles_AllMerged()
  {
    // Arrange: Create multiple custom blocklist and allowlist files.
    var blocklistDir = _fixture.CreateSubdirectory("multi-file-test");

    // Create primary blocklist.
    _fixture.CreateBlocklistFile(blocklistDir, "primary-blocked.com");

    // Create multiple custom blocklists.
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_blocklist_000.conf"),
      ["custom-blocked-a.com"]);
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_blocklist_001.conf"),
      ["custom-blocked-b.com"]);
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_blocklist_002.conf"),
      ["custom-blocked-c.com"]);

    // Create multiple custom allowlists.
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_allowlist_000.conf"),
      ["primary-blocked.com"]); // Allow primary blocked domain.

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: All custom blocked domains should be detected.
    checker.IsDisposable("custom-blocked-a.com").Should().BeTrue();
    checker.IsDisposable("custom-blocked-b.com").Should().BeTrue();
    checker.IsDisposable("custom-blocked-c.com").Should().BeTrue();

    // Primary blocked domain should be allowed (overridden by allowlist).
    checker.IsDisposable("primary-blocked.com").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_NoCustomFiles_StillWorks()
  {
    // Arrange: Create directory with only primary files.
    var blocklistDir = _fixture.CreateSubdirectory("no-custom-test");
    _fixture.CreateBlocklistFile(blocklistDir, "blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir, "allowed.com");

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Primary lists should work normally.
    checker.IsDisposable("blocked.com").Should().BeTrue();
    checker.IsDisposable("allowed.com").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_CustomFile_CommentsIgnored()
  {
    // Arrange: Create a blocklist file with comments.
    var blocklistDir = _fixture.CreateSubdirectory("comments-test");

    var customBlocklistPath = Path.Combine(blocklistDir, "custom_blocklist_001.conf");
    File.WriteAllLines(customBlocklistPath, [
      "# This is a comment",
      "real-blocked-domain.com",
      "  # Another comment with leading whitespace",
      "",
      "another-blocked.net",
      "# Final comment"
    ]);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Real domains should be blocked.
    checker.IsDisposable("real-blocked-domain.com").Should().BeTrue();
    checker.IsDisposable("another-blocked.net").Should().BeTrue();

    // Comments should not be treated as domains.
    checker.IsDisposable("# This is a comment").Should().BeFalse();
  }

  #endregion


  #region IDisposable

  public void Dispose()
  {
    _fixture.Dispose();
  }

  #endregion
}
