namespace Alos.Email.Validation.Tests;

using Configuration;

/// <summary>
///   Unit tests for <see cref="EmailValidationOptions"/>.
/// </summary>
public sealed class EmailValidationOptionsTests
{
  #region Tests - DefaultBlocklistDirectory

  [Fact]
  public void DefaultBlocklistDirectory_ReturnsCrossPlatformPath()
  {
    // Act
    var defaultDirectory = EmailValidationOptions.DefaultBlocklistDirectory;

    // Assert
    defaultDirectory.Should().NotBeNullOrEmpty();
    defaultDirectory.Should().Contain("alos");
    defaultDirectory.Should().Contain("email-blocklists");
  }


  [Fact]
  public void DefaultBlocklistDirectory_UsesLocalApplicationData()
  {
    // Arrange
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    // Act
    var defaultDirectory = EmailValidationOptions.DefaultBlocklistDirectory;

    // Assert
    defaultDirectory.Should().StartWith(localAppData);
  }


  [Fact]
  public void DefaultBlocklistDirectory_IsConsistentAcrossCalls()
  {
    // Act
    var first = EmailValidationOptions.DefaultBlocklistDirectory;
    var second = EmailValidationOptions.DefaultBlocklistDirectory;

    // Assert
    first.Should().Be(second);
  }

  #endregion


  #region Tests - Default Values

  [Fact]
  public void Options_HasCorrectDefaults()
  {
    // Arrange & Act
    var options = new EmailValidationOptions();

    // Assert
    options.BlocklistDirectory.Should().BeNull();
    options.EnableAutoUpdate.Should().BeFalse();
    options.UpdateInterval.Should().Be(TimeSpan.FromHours(24));
    options.CustomBlocklist.Should().BeEmpty();
    options.CustomAllowlist.Should().BeEmpty();
    options.CustomBlocklistUrls.Should().BeEmpty();
    options.CustomAllowlistUrls.Should().BeEmpty();
  }


  [Fact]
  public void Options_HasDefaultUrls()
  {
    // Arrange & Act
    var options = new EmailValidationOptions();

    // Assert
    options.BlocklistUrl.Should().Contain("disposable-email-domains");
    options.AllowlistUrl.Should().Contain("disposable-email-domains");
  }

  #endregion


  #region Tests - TimeSpan Edge Cases

  [Fact]
  public void Options_ZeroUpdateInterval_IsAllowed()
  {
    // Arrange & Act
    var options = new EmailValidationOptions
    {
      UpdateInterval = TimeSpan.Zero
    };

    // Assert: Zero is a valid value (though not practical).
    options.UpdateInterval.Should().Be(TimeSpan.Zero);
  }


  [Fact]
  public void Options_ZeroInitialUpdateDelay_IsAllowed()
  {
    // Arrange & Act
    var options = new EmailValidationOptions
    {
      InitialUpdateDelay = TimeSpan.Zero
    };

    // Assert: Zero is explicitly supported for immediate updates.
    options.InitialUpdateDelay.Should().Be(TimeSpan.Zero);
  }


  [Fact]
  public void Options_NegativeUpdateInterval_IsStoredAsIs()
  {
    // Arrange & Act: Negative values are technically storable in TimeSpan.
    var options = new EmailValidationOptions
    {
      UpdateInterval = TimeSpan.FromSeconds(-1)
    };

    // Assert: The property stores the value.
    // Note: The BlocklistUpdater will handle this by Task.Delay throwing
    // or behaving unexpectedly. This test documents that options don't validate.
    options.UpdateInterval.Should().Be(TimeSpan.FromSeconds(-1));
  }


  [Fact]
  public void Options_NegativeInitialUpdateDelay_IsStoredAsIs()
  {
    // Arrange & Act
    var options = new EmailValidationOptions
    {
      InitialUpdateDelay = TimeSpan.FromSeconds(-1)
    };

    // Assert: The property stores the value (no validation at options level).
    options.InitialUpdateDelay.Should().Be(TimeSpan.FromSeconds(-1));
  }


  [Fact]
  public void Options_MaxValueTimeSpan_IsAllowed()
  {
    // Arrange & Act
    var options = new EmailValidationOptions
    {
      UpdateInterval = TimeSpan.MaxValue,
      InitialUpdateDelay = TimeSpan.MaxValue
    };

    // Assert: Extreme values are storable.
    options.UpdateInterval.Should().Be(TimeSpan.MaxValue);
    options.InitialUpdateDelay.Should().Be(TimeSpan.MaxValue);
  }

  #endregion


  #region Tests - BlocklistDirectory Edge Cases

  [Fact]
  public void Options_NullBlocklistDirectory_IsAllowed()
  {
    // Arrange & Act
    var options = new EmailValidationOptions
    {
      BlocklistDirectory = null
    };

    // Assert: Null is the default and means "use embedded resources".
    options.BlocklistDirectory.Should().BeNull();
  }


  [Fact]
  public void Options_EmptyBlocklistDirectory_IsStoredAsIs()
  {
    // Arrange & Act
    var options = new EmailValidationOptions
    {
      BlocklistDirectory = ""
    };

    // Assert: Empty string is stored (treated as null in BlocklistUpdater).
    options.BlocklistDirectory.Should().BeEmpty();
  }


  [Fact]
  public void Options_WhitespaceBlocklistDirectory_IsStoredAsIs()
  {
    // Arrange & Act
    var options = new EmailValidationOptions
    {
      BlocklistDirectory = "   "
    };

    // Assert: Whitespace is stored (BlocklistUpdater uses string.IsNullOrEmpty).
    options.BlocklistDirectory.Should().Be("   ");
  }


  [Fact]
  public void Options_PathWithInvalidCharacters_IsStoredAsIs()
  {
    // Arrange: Path with characters that are invalid on Windows.
    // Note: These characters are valid on Linux, so this tests cross-platform differences.
    var invalidPath = "C:\\path\\with<invalid>chars:here|now?";

    // Act
    var options = new EmailValidationOptions
    {
      BlocklistDirectory = invalidPath
    };

    // Assert: Options don't validate paths (that's handled by the filesystem later).
    options.BlocklistDirectory.Should().Be(invalidPath);
  }


  [Fact]
  public void Options_RelativePath_IsStoredAsIs()
  {
    // Arrange
    var relativePath = "./relative/path";

    // Act
    var options = new EmailValidationOptions
    {
      BlocklistDirectory = relativePath
    };

    // Assert: Relative paths are stored and resolved by Directory.CreateDirectory.
    options.BlocklistDirectory.Should().Be(relativePath);
  }


  [Fact]
  public void Options_UncPath_IsStoredAsIs()
  {
    // Arrange: UNC path for network share.
    var uncPath = @"\\server\share\blocklists";

    // Act
    var options = new EmailValidationOptions
    {
      BlocklistDirectory = uncPath
    };

    // Assert: UNC paths are valid and stored.
    options.BlocklistDirectory.Should().Be(uncPath);
  }

  #endregion
}
