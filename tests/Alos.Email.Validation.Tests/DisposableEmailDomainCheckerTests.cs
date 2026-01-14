namespace Alos.Email.Validation.Tests;

using Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
///   Tests for <see cref="DisposableEmailDomainChecker"/>.
/// </summary>
public class DisposableEmailDomainCheckerTests
{
  private readonly IDisposableEmailDomainChecker _checker;


  public DisposableEmailDomainCheckerTests()
  {
    var options = Options.Create(new EmailValidationOptions());
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    _checker = new DisposableEmailDomainChecker(options, logger);
  }


  #region Known Disposable Domains

  [Theory]
  [InlineData("mailinator.com")]
  [InlineData("guerrillamail.com")]
  [InlineData("tempmail.com")]
  [InlineData("10minutemail.com")]
  [InlineData("throwaway.email")]
  [InlineData("yopmail.com")]
  [InlineData("maildrop.cc")]
  public void IsDisposable_KnownDisposable_ReturnsTrue(string domain)
  {
    var result = _checker.IsDisposable(domain);

    result.Should().BeTrue($"'{domain}' should be detected as disposable");
  }


  [Theory]
  [InlineData("MAILINATOR.COM")]
  [InlineData("Guerrillamail.Com")]
  [InlineData("TEMPMAIL.COM")]
  public void IsDisposable_CaseInsensitive(string domain)
  {
    var result = _checker.IsDisposable(domain);

    result.Should().BeTrue("comparison should be case-insensitive");
  }

  #endregion


  #region Legitimate Domains

  [Theory]
  [InlineData("gmail.com")]
  [InlineData("outlook.com")]
  [InlineData("yahoo.com")]
  [InlineData("protonmail.com")]
  [InlineData("icloud.com")]
  [InlineData("hotmail.com")]
  [InlineData("company.com")]
  public void IsDisposable_LegitimateProvider_ReturnsFalse(string domain)
  {
    var result = _checker.IsDisposable(domain);

    result.Should().BeFalse($"'{domain}' should not be blocked");
  }

  #endregion


  #region Edge Cases

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void IsDisposable_NullOrEmpty_ReturnsFalse(string? domain)
  {
    var result = _checker.IsDisposable(domain);

    result.Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_UnknownDomain_ReturnsFalse()
  {
    var result = _checker.IsDisposable("not-in-any-list-12345.xyz");

    result.Should().BeFalse();
  }

  #endregion


  #region Custom Lists

  [Fact]
  public void IsDisposable_CustomBlocklist_MergedWithBuiltIn()
  {
    // Arrange: Add a custom domain to the blocklist.
    var options = Options.Create(new EmailValidationOptions
    {
      CustomBlocklist = ["my-custom-blocked-domain.com", "another-blocked.net"]
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Custom blocked domain should be detected.
    checker.IsDisposable("my-custom-blocked-domain.com").Should().BeTrue();
    checker.IsDisposable("another-blocked.net").Should().BeTrue();

    // Built-in blocklist should still work.
    checker.IsDisposable("mailinator.com").Should().BeTrue();
  }


  [Fact]
  public void IsDisposable_CustomAllowlist_OverridesBlocklist()
  {
    // Arrange: Allow a domain that's on the built-in blocklist.
    var options = Options.Create(new EmailValidationOptions
    {
      CustomAllowlist = ["mailinator.com"] // Normally blocked, but we're allowing it.
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Allowlisted domain should NOT be detected as disposable.
    checker.IsDisposable("mailinator.com").Should().BeFalse();

    // Other blocked domains should still be blocked.
    checker.IsDisposable("guerrillamail.com").Should().BeTrue();
  }


  [Fact]
  public void IsDisposable_CustomAllowlistTakesPrecedence_OverCustomBlocklist()
  {
    // Arrange: Same domain in both custom blocklist and allowlist.
    var options = Options.Create(new EmailValidationOptions
    {
      CustomBlocklist = ["contested-domain.com"],
      CustomAllowlist = ["contested-domain.com"] // Allowlist wins.
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Allowlist takes precedence.
    checker.IsDisposable("contested-domain.com").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_CustomLists_CaseInsensitive()
  {
    // Arrange: Add domains with specific casing.
    var options = Options.Create(new EmailValidationOptions
    {
      CustomBlocklist = ["MyBlockedDomain.COM"],
      CustomAllowlist = ["MyAllowedDomain.COM"]
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Lookup should be case-insensitive.
    checker.IsDisposable("myblockedDOMAIN.com").Should().BeTrue();
    checker.IsDisposable("MYALLOWEDDOMAIN.com").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_CustomLists_TrimsWhitespace()
  {
    // Arrange: Add domains with whitespace.
    var options = Options.Create(new EmailValidationOptions
    {
      CustomBlocklist = ["  spaced-domain.com  ", ""],
      CustomAllowlist = ["  allowed-spaced.com  "]
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Whitespace should be trimmed.
    checker.IsDisposable("spaced-domain.com").Should().BeTrue();
    checker.IsDisposable("allowed-spaced.com").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_EmptyCustomLists_StillUsesBuiltInLists()
  {
    // Arrange: Empty custom lists should not affect built-in behavior.
    var options = Options.Create(new EmailValidationOptions
    {
      CustomBlocklist = [],
      CustomAllowlist = []
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Built-in blocklist should still work.
    checker.IsDisposable("mailinator.com").Should().BeTrue();
    checker.IsDisposable("gmail.com").Should().BeFalse();
  }


  [Fact]
  public void IsDisposable_NonExistentBlocklistDirectory_FallsBackToEmbeddedResources()
  {
    // Arrange: Configure a directory that doesn't exist.
    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = "/nonexistent/path/that/does/not/exist"
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act & Assert: Should fallback to embedded resources and still work.
    checker.IsDisposable("mailinator.com").Should().BeTrue();
    checker.IsDisposable("gmail.com").Should().BeFalse();
  }

  #endregion


  #region Disposal Tests

  [Fact]
  public void Dispose_CalledTwice_DoesNotThrow()
  {
    // Arrange: Create a fresh instance specifically for disposal testing.
    var options = Options.Create(new EmailValidationOptions());
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    // Act: Dispose twice.
    var disposeOnce = () => checker.Dispose();
    var disposeTwice = () => checker.Dispose();

    // Assert: Neither should throw.
    disposeOnce.Should().NotThrow("first dispose should succeed");
    disposeTwice.Should().NotThrow("second dispose should be a no-op");
  }


  [Fact]
  public void IsDisposable_AfterDispose_ThrowsObjectDisposedException()
  {
    // Arrange: Create a fresh instance and dispose it.
    var options = Options.Create(new EmailValidationOptions());
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);
    checker.Dispose();

    // Act & Assert: Using the disposed instance should throw.
    var act = () => checker.IsDisposable("test.com");

    act.Should().Throw<ObjectDisposedException>()
      .WithMessage("*DisposableEmailDomainChecker*");
  }


  [Fact]
  public void ReloadFromDisk_AfterDispose_ThrowsObjectDisposedException()
  {
    // Arrange: Create a fresh instance and dispose it.
    var options = Options.Create(new EmailValidationOptions());
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);
    checker.Dispose();

    // Act & Assert: Using the disposed instance should throw.
    var act = () => checker.ReloadFromDisk("/some/path");

    act.Should().Throw<ObjectDisposedException>()
      .WithMessage("*DisposableEmailDomainChecker*");
  }


  [Fact]
  public async Task IsDisposable_DisposeDuringActiveRead_HandlesGracefully()
  {
    // Arrange: Create a checker with a large blocklist to extend read time.
    var options = Options.Create(new EmailValidationOptions());
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    var readStarted = new ManualResetEventSlim(false);
    var proceedWithDispose = new ManualResetEventSlim(false);
    Exception? caughtException = null;

    // Start a read operation.
    var readTask = Task.Run(() =>
    {
      try
      {
        readStarted.Set();

        // Perform multiple reads to increase chance of overlap with dispose.
        for (var i = 0; i < 100; i++)
        {
          if (proceedWithDispose.IsSet)
            break;

          _ = checker.IsDisposable("mailinator.com");
        }
      }
      catch (ObjectDisposedException ex)
      {
        // This is acceptable - dispose happened during read.
        caughtException = ex;
      }
    });

    // Wait for read to start, then dispose.
    readStarted.Wait(TimeSpan.FromSeconds(1));
    proceedWithDispose.Set();
    checker.Dispose();

    // Wait for read task to complete.
    await readTask;

    // Assert: Either succeeded or got ObjectDisposedException (both are valid).
    // The key is that we didn't deadlock and the program didn't crash.
    if (caughtException is not null)
    {
      caughtException.Should().BeOfType<ObjectDisposedException>();
    }
  }

  #endregion
}
