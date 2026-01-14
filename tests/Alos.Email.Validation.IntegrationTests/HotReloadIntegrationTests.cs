namespace Alos.Email.Validation.IntegrationTests;

using Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Tests.Shared.Fixtures;

/// <summary>
///   Integration tests for hot-reload functionality of blocklists.
///   Tests the ReloadFromDisk method behavior.
/// </summary>
public sealed class HotReloadIntegrationTests : IDisposable
{
  #region Fields

  private readonly TempFileFixture _fixture;

  #endregion


  #region Constructors

  public HotReloadIntegrationTests()
  {
    _fixture = new TempFileFixture();
  }

  #endregion


  #region Tests - Hot Reload

  [Fact]
  public void ReloadFromDisk_UpdatesBlocklist()
  {
    // Arrange: Create initial blocklist.
    var blocklistDir = _fixture.CreateSubdirectory("hot-reload-blocklists");
    _fixture.CreateBlocklistFile(blocklistDir, "initial-blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir); // Empty allowlist.

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Verify initial state.
    checker.IsDisposable("initial-blocked.com").Should().BeTrue();
    checker.IsDisposable("new-blocked.com").Should().BeFalse();

    // Act: Update the blocklist file and reload.
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
    File.WriteAllLines(blocklistPath, ["initial-blocked.com", "new-blocked.com"]);
    checker.ReloadFromDisk(blocklistDir);

    // Assert: New domain should now be blocked.
    checker.IsDisposable("initial-blocked.com").Should().BeTrue();
    checker.IsDisposable("new-blocked.com").Should().BeTrue();
  }


  [Fact]
  public void ReloadFromDisk_MergesCustomLists()
  {
    // Arrange: Create blocklist directory and custom inline lists.
    var blocklistDir = _fixture.CreateSubdirectory("hot-reload-with-custom");
    _fixture.CreateBlocklistFile(blocklistDir, "disk-blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir); // Empty allowlist.

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      CustomBlocklist = ["inline-blocked.com"],
      CustomAllowlist = ["disk-blocked.com"] // Override the disk blocklist entry.
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Verify initial state with custom lists merged.
    checker.IsDisposable("inline-blocked.com").Should().BeTrue();
    checker.IsDisposable("disk-blocked.com").Should().BeFalse(); // Overridden by allowlist.

    // Act: Update the disk blocklist and reload.
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
    File.WriteAllLines(blocklistPath, ["disk-blocked.com", "another-disk-blocked.com"]);
    checker.ReloadFromDisk(blocklistDir);

    // Assert: Custom lists should be re-merged.
    checker.IsDisposable("inline-blocked.com").Should().BeTrue(); // Custom still applied.
    checker.IsDisposable("disk-blocked.com").Should().BeFalse(); // Still overridden by allowlist.
    checker.IsDisposable("another-disk-blocked.com").Should().BeTrue();
  }


  [Fact]
  public async Task ReloadFromDisk_ThreadSafe()
  {
    // Arrange: Create blocklist directory with initial content.
    var blocklistDir = _fixture.CreateSubdirectory("thread-safe-reload");
    _fixture.CreateBlocklistFile(blocklistDir, "concurrent-blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    var errors = new List<Exception>();
    using var cts = new CancellationTokenSource();

    // Act: Start concurrent reads while reloading.
    var readTask = Task.Run(
      async () =>
      {
        try
        {
          while (!cts.Token.IsCancellationRequested)
          {
            // Perform reads continuously.
            checker.IsDisposable("concurrent-blocked.com");
            checker.IsDisposable("mailinator.com");
            checker.IsDisposable("gmail.com");
            await Task.Delay(1, cts.Token);
          }
        }
        catch (OperationCanceledException)
        {
          // Expected when cancellation is requested.
        }
        catch (Exception ex)
        {
          errors.Add(ex);
        }
      },
      cts.Token);

    // Perform multiple reloads.
    for (var i = 0; i < 10; i++)
    {
      // Update the file with different content.
      var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
      File.WriteAllLines(blocklistPath, [$"concurrent-blocked.com", $"iteration-{i}.com"]);

      checker.ReloadFromDisk(blocklistDir);
      await Task.Delay(10);
    }

    // Stop the read task.
    await cts.CancelAsync();

    try
    {
      await readTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
    catch (OperationCanceledException)
    {
      // Task cancellation is expected.
    }
    catch (TimeoutException)
    {
      // Timeout is acceptable.
    }

    // Assert: No exceptions during concurrent reads.
    errors.Should().BeEmpty("concurrent reads during reload should be thread-safe");
  }


  [Fact]
  public void ReloadFromDisk_LoadsCustomFileNamingConventions()
  {
    // Arrange: Create blocklist directory with custom file naming conventions.
    var blocklistDir = _fixture.CreateSubdirectory("custom-naming-reload");
    _fixture.CreateBlocklistFile(blocklistDir, "primary-blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Verify initial state (only primary blocklist).
    checker.IsDisposable("primary-blocked.com").Should().BeTrue();
    checker.IsDisposable("custom-blocked-a.com").Should().BeFalse();
    checker.IsDisposable("custom-blocked-b.com").Should().BeFalse();

    // Act: Add custom blocklist files with naming convention and reload.
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_blocklist_000.conf"),
      ["custom-blocked-a.com"]);
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_blocklist_001.conf"),
      ["custom-blocked-b.com"]);

    // Add custom allowlist file to override primary blocked domain.
    File.WriteAllLines(
      Path.Combine(blocklistDir, "custom_allowlist_000.conf"),
      ["primary-blocked.com"]);

    checker.ReloadFromDisk(blocklistDir);

    // Assert: Custom files should be loaded after reload.
    checker.IsDisposable("custom-blocked-a.com").Should().BeTrue();
    checker.IsDisposable("custom-blocked-b.com").Should().BeTrue();
    checker.IsDisposable("primary-blocked.com").Should().BeFalse(); // Now allowlisted.
  }


  [Fact]
  public void ReloadFromDisk_EmptyDirectory_LoadsInlineCustomLists()
  {
    // Arrange: Create an empty blocklist directory.
    var blocklistDir = _fixture.CreateSubdirectory("empty-reload");

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      CustomBlocklist = ["inline-blocked.com"],
      CustomAllowlist = ["inline-allowed.com"]
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Verify initial state with inline custom lists.
    checker.IsDisposable("inline-blocked.com").Should().BeTrue();
    checker.IsDisposable("inline-allowed.com").Should().BeFalse();

    // Act: Reload from empty directory.
    checker.ReloadFromDisk(blocklistDir);

    // Assert: Inline custom lists should still be applied after reload.
    checker.IsDisposable("inline-blocked.com").Should().BeTrue();
    checker.IsDisposable("inline-allowed.com").Should().BeFalse();
  }

  #endregion


  #region Tests - File I/O Edge Cases

  [Fact]
  public void ReloadFromDisk_NonExistentDirectory_ThrowsDirectoryNotFoundException()
  {
    // Arrange: Create checker with a valid directory initially.
    var blocklistDir = _fixture.CreateSubdirectory("io-edge-case");
    _fixture.CreateBlocklistFile(blocklistDir, "blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Reload from non-existent directory should throw.
    var nonExistentDir = Path.Combine(_fixture.TempDirectory, "does-not-exist");

    FluentActions
      .Invoking(() => checker.ReloadFromDisk(nonExistentDir))
      .Should().Throw<DirectoryNotFoundException>();
  }


  [Fact]
  public void ReloadFromDisk_FileDeletedDuringReload_HandlesGracefully()
  {
    // Arrange: Create blocklist directory with content.
    var blocklistDir = _fixture.CreateSubdirectory("file-deleted");
    _fixture.CreateBlocklistFile(blocklistDir, "blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Verify initial state.
    checker.IsDisposable("blocked.com").Should().BeTrue();

    // Act: Delete the blocklist file and reload.
    // The custom_blocklist_*.conf pattern won't match if file is deleted,
    // so this tests that missing primary files are handled gracefully.
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
    File.Delete(blocklistPath);

    // Should not throw - missing files are ignored.
    var act = () => checker.ReloadFromDisk(blocklistDir);
    act.Should().NotThrow();

    // Assert: After reload with missing file, the domain should no longer be blocked.
    checker.IsDisposable("blocked.com").Should().BeFalse();
  }


  [Fact]
  public void ReloadFromDisk_FileLockedByAnotherProcess_ThrowsIOException()
  {
    // Arrange: Create blocklist directory with content.
    var blocklistDir = _fixture.CreateSubdirectory("file-locked");
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
    File.WriteAllLines(blocklistPath, ["blocked.com"]);
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act: Lock the file and try to reload.
    using var lockedStream = new FileStream(
      blocklistPath,
      FileMode.Open,
      FileAccess.Read,
      FileShare.None); // No sharing - locks the file.

    // Assert: Should throw IOException due to file lock.
    FluentActions
      .Invoking(() => checker.ReloadFromDisk(blocklistDir))
      .Should().Throw<IOException>();
  }


  [Fact]
  public void ReloadFromDisk_CorruptedFileContent_HandlesGracefully()
  {
    // Arrange: Create blocklist directory with corrupted content.
    var blocklistDir = _fixture.CreateSubdirectory("corrupted-content");

    // Write binary garbage to the blocklist file.
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
    var binaryGarbage = new byte[] { 0x00, 0xFF, 0xFE, 0x01, 0x02 };
    File.WriteAllBytes(blocklistPath, binaryGarbage);
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();

    // Act & Assert: Constructor should not throw on corrupted content.
    // The file reading should handle binary data gracefully.
    var act = () => new DisposableEmailDomainChecker(options, logger);
    act.Should().NotThrow();
  }


  [Fact]
  public void ReloadFromDisk_VeryLargeFile_LoadsSuccessfully()
  {
    // Arrange: Create blocklist directory with a large file.
    var blocklistDir = _fixture.CreateSubdirectory("large-file");

    // Generate 100,000 domain entries.
    var domains = Enumerable.Range(0, 100_000)
      .Select(i => $"domain-{i:D6}.com")
      .ToList();

    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
    File.WriteAllLines(blocklistPath, domains);
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act: Verify various domains are blocked.
    checker.IsDisposable("domain-000000.com").Should().BeTrue();
    checker.IsDisposable("domain-050000.com").Should().BeTrue();
    checker.IsDisposable("domain-099999.com").Should().BeTrue();
    checker.IsDisposable("domain-100000.com").Should().BeFalse(); // Beyond range.
  }

  #endregion


  #region Tests - File Encoding Edge Cases

  [Fact]
  public void Constructor_Utf8WithBom_LoadsCorrectly()
  {
    // Arrange: Create blocklist file with UTF-8 BOM encoding.
    var blocklistDir = _fixture.CreateSubdirectory("encoding-utf8-bom");
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");

    // UTF-8 with BOM: EF BB BF prefix.
    var domains = new[] { "bom-blocked.com", "another-bom.org" };
    File.WriteAllLines(blocklistPath, domains, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();

    // Act
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Assert: Domains should be loaded correctly despite BOM.
    checker.IsDisposable("bom-blocked.com").Should().BeTrue();
    checker.IsDisposable("another-bom.org").Should().BeTrue();
  }


  [Fact]
  public void Constructor_Utf16LittleEndian_HandlesGracefully()
  {
    // Arrange: Create blocklist file with UTF-16 LE encoding (Windows default for some apps).
    var blocklistDir = _fixture.CreateSubdirectory("encoding-utf16-le");
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");

    var domains = new[] { "utf16-blocked.com", "another-utf16.org" };
    File.WriteAllLines(blocklistPath, domains, System.Text.Encoding.Unicode); // UTF-16 LE
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();

    // Act: Constructor should not throw.
    var act = () => new DisposableEmailDomainChecker(options, logger);
    act.Should().NotThrow();

    // Assert: File.ReadLines with default encoding may not read UTF-16 correctly,
    // but the checker should handle it gracefully (may not find domains).
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Note: The behavior depends on how File.ReadLines handles UTF-16.
    // On .NET, File.ReadLines tries to auto-detect encoding via BOM.
    // UTF-16 has a BOM (FF FE for LE), so it should work.
    checker.IsDisposable("utf16-blocked.com").Should().BeTrue("UTF-16 with BOM should be auto-detected");
  }


  [Fact]
  public void Constructor_Utf16BigEndian_HandlesGracefully()
  {
    // Arrange: Create blocklist file with UTF-16 BE encoding.
    var blocklistDir = _fixture.CreateSubdirectory("encoding-utf16-be");
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");

    var domains = new[] { "utf16be-blocked.com", "another-utf16be.org" };
    File.WriteAllLines(blocklistPath, domains, System.Text.Encoding.BigEndianUnicode); // UTF-16 BE
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();

    // Act & Assert: Should not throw and should handle gracefully.
    var act = () => new DisposableEmailDomainChecker(options, logger);
    act.Should().NotThrow();

    var checker = new DisposableEmailDomainChecker(options, logger);

    // UTF-16 BE has BOM (FE FF), so it should be auto-detected.
    checker.IsDisposable("utf16be-blocked.com").Should().BeTrue("UTF-16 BE with BOM should be auto-detected");
  }


  [Fact]
  public void Constructor_Ascii_LoadsCorrectly()
  {
    // Arrange: Create blocklist file with pure ASCII (subset of UTF-8).
    var blocklistDir = _fixture.CreateSubdirectory("encoding-ascii");
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");

    var domains = new[] { "ascii-blocked.com", "simple.org" };
    File.WriteAllLines(blocklistPath, domains, System.Text.Encoding.ASCII);
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();

    // Act
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Assert
    checker.IsDisposable("ascii-blocked.com").Should().BeTrue();
    checker.IsDisposable("simple.org").Should().BeTrue();
  }


  [Fact]
  public void Constructor_Latin1_Iso88591_HandlesGracefully()
  {
    // Arrange: Create blocklist file with Latin1/ISO-8859-1 encoding.
    // Latin1 is similar to Windows-1252 for ASCII characters and is available in .NET Core.
    var blocklistDir = _fixture.CreateSubdirectory("encoding-latin1");
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");

    // Latin1 specific characters won't be in domain names, but test graceful handling.
    var domains = new[] { "latin1-blocked.com", "iso8859.org" };

    // ISO-8859-1 (Latin1) encoding - available by default in .NET Core.
    var latin1 = System.Text.Encoding.Latin1;
    File.WriteAllLines(blocklistPath, domains, latin1);
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();

    // Act
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Assert: ASCII-compatible domains should work even with Latin1 encoding.
    checker.IsDisposable("latin1-blocked.com").Should().BeTrue();
    checker.IsDisposable("iso8859.org").Should().BeTrue();
  }


  [Fact]
  public void Constructor_Utf8WithUnicodeDomains_LoadsCorrectly()
  {
    // Arrange: Create blocklist file with UTF-8 encoded Unicode domain names.
    var blocklistDir = _fixture.CreateSubdirectory("encoding-utf8-unicode");
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");

    // Unicode domain names (IDN) - should be stored as-is.
    var domains = new[] { "münchen.de", "日本.jp", "пример.рф", "normal.com" };
    File.WriteAllLines(blocklistPath, domains, System.Text.Encoding.UTF8);
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();

    // Act
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Assert: Unicode domains should be loaded correctly.
    checker.IsDisposable("münchen.de").Should().BeTrue();
    checker.IsDisposable("日本.jp").Should().BeTrue();
    checker.IsDisposable("пример.рф").Should().BeTrue();
    checker.IsDisposable("normal.com").Should().BeTrue();
  }


  [Fact]
  public void ReloadFromDisk_EncodingChanges_HandlesGracefully()
  {
    // Arrange: Start with UTF-8, then reload with UTF-16.
    var blocklistDir = _fixture.CreateSubdirectory("encoding-change");
    var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");

    // Initial: UTF-8 without BOM.
    File.WriteAllLines(blocklistPath, ["initial-utf8.com"], System.Text.Encoding.UTF8);
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Verify initial state.
    checker.IsDisposable("initial-utf8.com").Should().BeTrue();

    // Act: Change to UTF-16 and reload.
    File.WriteAllLines(blocklistPath, ["changed-utf16.com"], System.Text.Encoding.Unicode);
    checker.ReloadFromDisk(blocklistDir);

    // Assert: New content should be loaded (UTF-16 has BOM, so auto-detected).
    checker.IsDisposable("initial-utf8.com").Should().BeFalse();
    checker.IsDisposable("changed-utf16.com").Should().BeTrue();
  }

  #endregion


  #region IDisposable

  public void Dispose()
  {
    _fixture.Dispose();
  }

  #endregion
}
