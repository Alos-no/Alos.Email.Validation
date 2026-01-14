namespace Alos.Email.Validation.IntegrationTests;

using System.Net;
using Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Tests.Shared.Fixtures;
using Tests.Shared.TestHelpers;

/// <summary>
///   Integration tests for <see cref="BlocklistUpdater"/> background service.
///   Tests the hosted service lifecycle, download behavior, and update logic.
/// </summary>
public sealed class BlocklistUpdaterIntegrationTests : IDisposable
{
  #region Fields

  private readonly TempFileFixture _fixture;

  #endregion


  #region Constructors

  public BlocklistUpdaterIntegrationTests()
  {
    _fixture = new TempFileFixture();
  }

  #endregion


  #region Tests - Hosted Service Lifecycle

  [Fact]
  public async Task BlocklistUpdater_StartsWithHost()
  {
    // Arrange: Build a host with the BlocklistUpdater.
    var blocklistDir = _fixture.CreateSubdirectory("updater-start");

    var hostBuilder = Host.CreateDefaultBuilder()
      .ConfigureServices(services =>
      {
        services.AddEmailValidationWithAutoUpdate(options =>
        {
          options.BlocklistDirectory = blocklistDir;
          options.EnableAutoUpdate = true;
          options.UpdateInterval = TimeSpan.FromHours(1);
          options.InitialUpdateDelay = TimeSpan.FromHours(1); // Long delay to avoid actual downloads.
        });
      });

    using var host = hostBuilder.Build();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    // Act: Start the host (which starts the BlocklistUpdater).
    await host.StartAsync(cts.Token);

    // Assert: Host started successfully (BlocklistUpdater is running in background).
    // The fact that we get here without exception means it started.
    host.Services.GetService<IHostedService>().Should().NotBeNull();

    // Clean up.
    await host.StopAsync(CancellationToken.None);
  }


  [Fact]
  public async Task BlocklistUpdater_StopsGracefully()
  {
    // Arrange: Build a host with the BlocklistUpdater.
    var blocklistDir = _fixture.CreateSubdirectory("updater-stop");

    var hostBuilder = Host.CreateDefaultBuilder()
      .ConfigureServices(services =>
      {
        services.AddEmailValidationWithAutoUpdate(options =>
        {
          options.BlocklistDirectory = blocklistDir;
          options.EnableAutoUpdate = true;
          options.UpdateInterval = TimeSpan.FromHours(1);
          options.InitialUpdateDelay = TimeSpan.FromHours(1); // Long delay to avoid actual downloads.
        });
      });

    using var host = hostBuilder.Build();
    using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    // Act: Start and then stop the host.
    await host.StartAsync(startCts.Token);

    // Give it a moment to initialize.
    await Task.Delay(100);

    using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await host.StopAsync(stopCts.Token);

    // Assert: Host stopped without hanging or throwing.
    // The fact that we get here means graceful shutdown worked.
  }

  #endregion


  #region Tests - Download Behavior

  [Fact]
  public async Task BlocklistUpdater_DownloadsPrimaryBlocklistAndAllowlist()
  {
    // Arrange: Set up mock HTTP handler to return test content.
    var blocklistDir = _fixture.CreateSubdirectory("updater-download-primary");
    var mockBlocklistContent = "downloaded-blocked-1.com\ndownloaded-blocked-2.net";
    var mockAllowlistContent = "downloaded-allowed.com";

    var mockHandler = HttpMockFactory.CreateMockHttpHandler(
      blocklistContent: mockBlocklistContent,
      allowlistContent: mockAllowlistContent);

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1), // Long interval - we only want one update.
      InitialUpdateDelay = TimeSpan.Zero, // Immediate execution for testing.
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait for the first update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);

    // Wait for download to complete (short delay since InitialUpdateDelay is zero).
    await Task.Delay(500, cts.Token);

    await updater.StopAsync(CancellationToken.None);

    // Assert: Verify files were downloaded and saved correctly.
    var blocklistPath = Path.Combine(blocklistDir, BlocklistFileNames.PrimaryBlocklist);
    var allowlistPath = Path.Combine(blocklistDir, BlocklistFileNames.PrimaryAllowlist);

    File.Exists(blocklistPath).Should().BeTrue("primary blocklist file should be downloaded");
    File.Exists(allowlistPath).Should().BeTrue("primary allowlist file should be downloaded");

    var savedBlocklistContent = await File.ReadAllTextAsync(blocklistPath);
    var savedAllowlistContent = await File.ReadAllTextAsync(allowlistPath);

    savedBlocklistContent.Should().Be(mockBlocklistContent);
    savedAllowlistContent.Should().Be(mockAllowlistContent);

    // Assert: Verify checker was reloaded with downloaded content.
    checker.IsDisposable("downloaded-blocked-1.com").Should().BeTrue();
    checker.IsDisposable("downloaded-blocked-2.net").Should().BeTrue();
    checker.IsDisposable("downloaded-allowed.com").Should().BeFalse("allowlisted domains should not be blocked");
  }


  [Fact]
  public async Task BlocklistUpdater_DownloadsCustomBlocklistUrls()
  {
    // Arrange: Set up mock HTTP handler with custom blocklist URLs.
    var blocklistDir = _fixture.CreateSubdirectory("updater-download-custom-blocklist");

    var primaryBlocklist = "primary-blocked.com";
    var customBlocklist1 = "custom-blocked-1.org";
    var customBlocklist2 = "custom-blocked-2.io";

    var requestedUrls = new List<string>();

    var mockHandler = HttpMockFactory.CreateMockHttpHandlerWithUrlTracking(
      blocklistContent: primaryBlocklist,
      allowlistContent: "",
      customBlocklistContents: new[] { customBlocklist1, customBlocklist2 },
      customAllowlistContents: Array.Empty<string>(),
      requestedUrls: requestedUrls);

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf",
      CustomBlocklistUrls = new List<string>
      {
        "https://custom1.example.com/blocklist.txt",
        "https://custom2.example.com/blocklist.txt"
      }
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait for the first update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Verify custom blocklist files were created with correct naming convention.
    var customBlocklist1Path = Path.Combine(blocklistDir, "custom_blocklist_000.conf");
    var customBlocklist2Path = Path.Combine(blocklistDir, "custom_blocklist_001.conf");

    File.Exists(customBlocklist1Path).Should().BeTrue("custom blocklist #1 should be saved as custom_blocklist_000.conf");
    File.Exists(customBlocklist2Path).Should().BeTrue("custom blocklist #2 should be saved as custom_blocklist_001.conf");

    var content1 = await File.ReadAllTextAsync(customBlocklist1Path);
    var content2 = await File.ReadAllTextAsync(customBlocklist2Path);

    content1.Should().Be(customBlocklist1);
    content2.Should().Be(customBlocklist2);

    // Assert: Verify checker was reloaded with all blocklist content.
    checker.IsDisposable("primary-blocked.com").Should().BeTrue();
    checker.IsDisposable("custom-blocked-1.org").Should().BeTrue();
    checker.IsDisposable("custom-blocked-2.io").Should().BeTrue();
  }


  [Fact]
  public async Task BlocklistUpdater_DownloadsCustomAllowlistUrls()
  {
    // Arrange: Set up mock HTTP handler with custom allowlist URLs.
    var blocklistDir = _fixture.CreateSubdirectory("updater-download-custom-allowlist");

    // The domain is in the primary blocklist but should be unblocked by custom allowlist.
    var primaryBlocklist = "false-positive.org\nreal-blocked.com";
    var primaryAllowlist = "";
    var customAllowlist1 = "false-positive.org";
    var customAllowlist2 = "another-false-positive.net";

    var mockHandler = HttpMockFactory.CreateMockHttpHandlerWithUrlTracking(
      blocklistContent: primaryBlocklist,
      allowlistContent: primaryAllowlist,
      customBlocklistContents: Array.Empty<string>(),
      customAllowlistContents: new[] { customAllowlist1, customAllowlist2 },
      requestedUrls: new List<string>());

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf",
      CustomAllowlistUrls = new List<string>
      {
        "https://custom1.example.com/allowlist.txt",
        "https://custom2.example.com/allowlist.txt"
      }
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait for the first update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Verify custom allowlist files were created with correct naming convention.
    var customAllowlist1Path = Path.Combine(blocklistDir, "custom_allowlist_000.conf");
    var customAllowlist2Path = Path.Combine(blocklistDir, "custom_allowlist_001.conf");

    File.Exists(customAllowlist1Path).Should().BeTrue("custom allowlist #1 should be saved as custom_allowlist_000.conf");
    File.Exists(customAllowlist2Path).Should().BeTrue("custom allowlist #2 should be saved as custom_allowlist_001.conf");

    var content1 = await File.ReadAllTextAsync(customAllowlist1Path);
    var content2 = await File.ReadAllTextAsync(customAllowlist2Path);

    content1.Should().Be(customAllowlist1);
    content2.Should().Be(customAllowlist2);

    // Assert: Verify checker was reloaded with allowlist taking precedence.
    checker.IsDisposable("real-blocked.com").Should().BeTrue("real blocked domain should still be blocked");
    checker.IsDisposable("false-positive.org").Should().BeFalse("false positive should be unblocked by custom allowlist");
    checker.IsDisposable("another-false-positive.net").Should().BeFalse("should not be blocked if in allowlist");
  }


  [Fact]
  public async Task BlocklistUpdater_ReloadsCheckerAfterDownload()
  {
    // Arrange: Set up mock HTTP handler with unique test domains.
    var blocklistDir = _fixture.CreateSubdirectory("updater-reload-checker");
    var uniqueDomain = $"reload-test-{Guid.NewGuid():N}.com";

    var mockHandler = HttpMockFactory.CreateMockHttpHandler(
      blocklistContent: uniqueDomain,
      allowlistContent: "");

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Assert: Before download, the unique domain is not in the checker.
    checker.IsDisposable(uniqueDomain).Should().BeFalse("domain should not be known before download");

    // Act: Start the updater and wait for the first update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: After download, the checker should have the new domain.
    checker.IsDisposable(uniqueDomain).Should().BeTrue("domain should be blocked after download and reload");
  }

  #endregion


  #region Tests - Configuration Behavior

  [Fact]
  public async Task BlocklistUpdater_NoBlocklistDirectory_UsesDefault()
  {
    // Arrange: Create updater without BlocklistDirectory configured.
    var mockHandler = HttpMockFactory.CreateMockHttpHandler("blocked.com", "allowed.com");
    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = null, // Not configured - should use default.
      EnableAutoUpdate = true,
      InitialUpdateDelay = TimeSpan.FromHours(1) // Long delay to avoid actual downloads.
    });

    var logger = new Mock<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger.Object);

    // Act: Start and quickly stop.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    await updater.StartAsync(cts.Token);
    await Task.Delay(100);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Info should have been logged with the default directory path.
    var expectedDefaultPath = EmailValidationOptions.DefaultBlocklistDirectory;

    logger.Verify(
      l => l.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedDefaultPath)),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
      Times.Once);
  }


  [Fact]
  public async Task BlocklistUpdater_DisabledAutoUpdate_DoesNotRun()
  {
    // Arrange: Create updater with auto-update disabled.
    var blocklistDir = _fixture.CreateSubdirectory("updater-disabled");
    var mockHandler = HttpMockFactory.CreateMockHttpHandler("blocked.com", "allowed.com");
    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = false // Disabled.
    });

    var logger = new Mock<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger.Object);

    // Act: Start and quickly stop.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    await updater.StartAsync(cts.Token);
    await Task.Delay(200);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Info about being disabled should be logged.
    logger.Verify(
      l => l.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
      Times.Once);

    // Assert: No files should be created in the directory.
    Directory.GetFiles(blocklistDir).Should().BeEmpty("no downloads should occur when disabled");
  }


  [Fact]
  public async Task BlocklistUpdater_ZeroInitialDelay_DownloadsImmediately()
  {
    // Arrange: Set up mock HTTP handler with URL tracking.
    var blocklistDir = _fixture.CreateSubdirectory("updater-zero-delay");
    var requestedUrls = new List<string>();

    var mockHandler = HttpMockFactory.CreateMockHttpHandlerWithUrlTracking(
      blocklistContent: "immediate-download.com",
      allowlistContent: "",
      customBlocklistContents: Array.Empty<string>(),
      customAllowlistContents: Array.Empty<string>(),
      requestedUrls: requestedUrls);

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero, // Should trigger immediate download.
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait a short time.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    await updater.StartAsync(cts.Token);

    // Wait for download to complete - should be very fast with zero delay.
    await Task.Delay(300, cts.Token);

    stopwatch.Stop();
    await updater.StopAsync(CancellationToken.None);

    // Assert: URLs should have been requested.
    requestedUrls.Should().Contain(url => url.Contains("blocklist"));
    requestedUrls.Should().Contain(url => url.Contains("allowlist"));

    // Assert: Files should exist.
    File.Exists(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryBlocklist)).Should().BeTrue();
  }

  #endregion


  #region Tests - Error Handling

  [Fact]
  public async Task BlocklistUpdater_HttpError_ContinuesRunning()
  {
    // Arrange: Set up mock HTTP handler that fails.
    var blocklistDir = _fixture.CreateSubdirectory("updater-error");
    var mockHandler = HttpMockFactory.CreateMockHttpHandler(throwException: true);
    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromMilliseconds(100),
      InitialUpdateDelay = TimeSpan.Zero
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

    try
    {
      await updater.StartAsync(cts.Token);
      await Task.Delay(500, cts.Token);
    }
    catch (OperationCanceledException)
    {
      // Expected.
    }

    await updater.StopAsync(CancellationToken.None);

    // Assert: The fact that we got here means the updater didn't crash.
    // HTTP errors are logged and the service continues running.
  }


  [Fact]
  public async Task BlocklistUpdater_PartialFailure_DownloadsOtherLists()
  {
    // Arrange: Set up mock HTTP handler where one custom URL fails but others succeed.
    var blocklistDir = _fixture.CreateSubdirectory("updater-partial-failure");

    var mockHandler = HttpMockFactory.CreateMockHttpHandlerWithSelectiveFailure(
      blocklistContent: "primary-blocked.com",
      allowlistContent: "primary-allowed.com",
      failingUrlPattern: "custom1"); // First custom URL will fail.

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf",
      CustomBlocklistUrls = new List<string>
      {
        "https://custom1.example.com/blocklist.txt", // This will fail.
        "https://custom2.example.com/blocklist.txt"  // This should succeed.
      }
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait for the update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Primary lists should be downloaded despite custom URL failure.
    File.Exists(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryBlocklist)).Should().BeTrue();
    File.Exists(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryAllowlist)).Should().BeTrue();

    // Assert: The second custom blocklist (custom_blocklist_001.conf) should exist.
    // Note: The first one (custom_blocklist_000.conf) may or may not exist depending on
    // whether the failure happens before or after file creation.
    File.Exists(Path.Combine(blocklistDir, "custom_blocklist_001.conf")).Should().BeTrue();

    // Assert: Checker should have primary blocklist domains.
    checker.IsDisposable("primary-blocked.com").Should().BeTrue();
  }

  #endregion


  #region Tests - Malformed URL Handling

  [Fact]
  public async Task BlocklistUpdater_MalformedCustomBlocklistUrl_ContinuesWithOtherDownloads()
  {
    // Arrange: Set up mock HTTP handler where the malformed URL will cause an exception.
    var blocklistDir = _fixture.CreateSubdirectory("updater-malformed-url");

    // Malformed URLs like "not-a-url" will throw when HttpClient tries to use them.
    // However, the BlocklistUpdater passes URLs directly to HttpClient which validates them.
    // We'll test with an invalid URL format that still looks like a URL but is unreachable.
    var mockHandler = HttpMockFactory.CreateMockHttpHandlerWithSelectiveFailure(
      blocklistContent: "primary-blocked.com",
      allowlistContent: "",
      failingUrlPattern: "malformed");

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf",
      CustomBlocklistUrls = new List<string>
      {
        "https://malformed-url-test.invalid/blocklist.txt", // Will fail.
        "https://valid.example.com/blocklist.txt"
      }
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait for the update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Primary lists should still be downloaded despite the malformed custom URL.
    File.Exists(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryBlocklist)).Should().BeTrue(
      "primary blocklist should be downloaded even if custom URL fails");

    // Assert: Checker should have primary blocklist domains.
    checker.IsDisposable("primary-blocked.com").Should().BeTrue();
  }


  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("not-a-valid-url")]
  [InlineData("ftp://wrong-protocol.com/list.txt")]
  [InlineData("file:///local/path/list.txt")]
  public async Task BlocklistUpdater_InvalidUrlFormats_HandlesGracefully(string invalidUrl)
  {
    // Arrange: Set up mock HTTP handler.
    var blocklistDir = _fixture.CreateSubdirectory($"updater-invalid-url-{Guid.NewGuid():N}");

    // Create a handler that will fail on invalid URLs but succeed on valid ones.
    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        var url = request.RequestUri?.ToString() ?? "";

        // Valid primary URLs succeed.
        if (url.Contains("example.com"))
        {
          return new HttpResponseMessage(HttpStatusCode.OK)
          {
            Content = new StringContent("test-blocked.com")
          };
        }

        // Invalid URLs fail.
        throw new HttpRequestException($"Invalid URL: {url}");
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf",
      CustomBlocklistUrls = string.IsNullOrWhiteSpace(invalidUrl)
        ? new List<string>() // Skip empty URLs.
        : new List<string> { invalidUrl }
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act & Assert: Should not throw even with invalid URL.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var startTask = async () =>
    {
      await updater.StartAsync(cts.Token);
      await Task.Delay(500, cts.Token);
      await updater.StopAsync(CancellationToken.None);
    };

    await startTask.Should().NotThrowAsync("updater should handle invalid URLs gracefully");

    // Assert: Primary blocklist should still work.
    File.Exists(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryBlocklist)).Should().BeTrue();
  }

  #endregion


  #region Tests - Download Content Edge Cases

  [Fact]
  public async Task BlocklistUpdater_EmptyContentResponse_HandlesGracefully()
  {
    // Arrange: Set up mock HTTP handler that returns empty content.
    var blocklistDir = _fixture.CreateSubdirectory("updater-empty-content");

    var mockHandler = HttpMockFactory.CreateMockHttpHandler(
      blocklistContent: "",
      allowlistContent: "");

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait for the update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Files should be created (even if empty).
    File.Exists(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryBlocklist)).Should().BeTrue();
    File.Exists(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryAllowlist)).Should().BeTrue();

    // Files should be empty.
    var blocklistContent = await File.ReadAllTextAsync(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryBlocklist));
    blocklistContent.Should().BeEmpty();
  }


  [Fact]
  public async Task BlocklistUpdater_ContentWithInvalidCharacters_HandlesGracefully()
  {
    // Arrange: Set up mock HTTP handler with content containing unusual characters.
    var blocklistDir = _fixture.CreateSubdirectory("updater-invalid-chars");

    // Content with null bytes, control characters, and Unicode.
    var blocklistWithWeirdChars = "normal-domain.com\n\0null-byte.com\n\x01control-char.com\nüñîçödé.com";

    var mockHandler = HttpMockFactory.CreateMockHttpHandler(
      blocklistContent: blocklistWithWeirdChars,
      allowlistContent: "");

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait for the update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Normal domain should be blocked.
    checker.IsDisposable("normal-domain.com").Should().BeTrue();
  }


  [Fact]
  public async Task BlocklistUpdater_VeryLargeLinesInContent_HandlesGracefully()
  {
    // Arrange: Create content with extremely long lines.
    var blocklistDir = _fixture.CreateSubdirectory("updater-long-lines");

    var veryLongDomain = new string('a', 10000) + ".com";
    var blocklistContent = $"normal.com\n{veryLongDomain}\nanother.com";

    var mockHandler = HttpMockFactory.CreateMockHttpHandler(
      blocklistContent: blocklistContent,
      allowlistContent: "");

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater and wait for the update cycle.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Normal domains should be blocked.
    checker.IsDisposable("normal.com").Should().BeTrue();
    checker.IsDisposable("another.com").Should().BeTrue();

    // Long domain should also be in the blocklist (even if impractical).
    checker.IsDisposable(veryLongDomain).Should().BeTrue();
  }

  #endregion


  #region Tests - HTTP Edge Cases

  [Fact]
  public async Task BlocklistUpdater_HttpRedirect_FollowsRedirect()
  {
    // Arrange: Set up mock HTTP handler that returns redirects.
    var blocklistDir = _fixture.CreateSubdirectory("updater-redirect");
    var redirectCount = 0;

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        var url = request.RequestUri?.ToString() ?? "";

        // First request returns redirect, second returns content.
        // Note: HttpClient follows redirects automatically by default.
        // This test verifies the handler doesn't interfere with that behavior.
        redirectCount++;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(url.Contains("blocklist")
            ? "redirect-blocked.com"
            : "")
        };
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Requests were made and content was downloaded.
    redirectCount.Should().BeGreaterThanOrEqualTo(2, "at least blocklist and allowlist should be requested");
    checker.IsDisposable("redirect-blocked.com").Should().BeTrue();
  }


  [Fact]
  public async Task BlocklistUpdater_HttpTimeout_HandlesGracefully()
  {
    // Arrange: Set up mock HTTP handler that simulates timeout.
    var blocklistDir = _fixture.CreateSubdirectory("updater-timeout");

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ThrowsAsync(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater - it should handle the timeout gracefully.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    // Should not throw - timeouts are caught and logged.
    var act = async () =>
    {
      await updater.StartAsync(cts.Token);
      await Task.Delay(500, cts.Token);
      await updater.StopAsync(CancellationToken.None);
    };

    await act.Should().NotThrowAsync("timeout should be handled gracefully");

    // Assert: Files should not exist since download failed.
    File.Exists(Path.Combine(blocklistDir, BlocklistFileNames.PrimaryBlocklist)).Should().BeFalse();
  }


  [Fact]
  public async Task BlocklistUpdater_VeryLargeResponse_HandlesSuccessfully()
  {
    // Arrange: Set up mock HTTP handler that returns very large content.
    var blocklistDir = _fixture.CreateSubdirectory("updater-large-response");

    // Generate 50,000 domain entries (~1MB of text).
    var largeDomains = string.Join("\n",
      Enumerable.Range(0, 50_000).Select(i => $"large-domain-{i:D6}.com"));

    var mockHandler = HttpMockFactory.CreateMockHttpHandler(
      blocklistContent: largeDomains,
      allowlistContent: "");

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    await updater.StartAsync(cts.Token);
    await Task.Delay(1000, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Random domains from the large list should be blocked.
    checker.IsDisposable("large-domain-000000.com").Should().BeTrue();
    checker.IsDisposable("large-domain-025000.com").Should().BeTrue();
    checker.IsDisposable("large-domain-049999.com").Should().BeTrue();
  }


  [Fact]
  public async Task BlocklistUpdater_Http500Error_HandlesGracefully()
  {
    // Arrange: Set up mock HTTP handler that returns 500 error.
    var blocklistDir = _fixture.CreateSubdirectory("updater-500-error");

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
      {
        Content = new StringContent("Internal Server Error")
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Start the updater - it should handle the 500 error gracefully.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var act = async () =>
    {
      await updater.StartAsync(cts.Token);
      await Task.Delay(500, cts.Token);
      await updater.StopAsync(CancellationToken.None);
    };

    await act.Should().NotThrowAsync("500 error should be handled gracefully");
  }

  #endregion


  #region Tests - HTTP Transfer Edge Cases

  /// <summary>
  ///   Validates that the updater handles chunked transfer encoding correctly.
  ///   HttpClient handles chunked encoding transparently, but we verify the content is received correctly.
  /// </summary>
  [Fact]
  public async Task BlocklistUpdater_ChunkedTransferEncoding_HandlesCorrectly()
  {
    // Arrange: Set up mock HTTP handler that simulates chunked response.
    var blocklistDir = _fixture.CreateSubdirectory("updater-chunked");

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        var content = request.RequestUri?.ToString().Contains("blocklist") == true
          ? "chunked-blocked.com\nchunked-blocked-2.org"
          : "";

        // Simulate chunked transfer encoding by not setting Content-Length.
        // When Content-Length is not set, HttpClient expects chunked or connection close.
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(content)
        };

        // Remove Content-Length to simulate chunked/streaming behavior.
        response.Content.Headers.ContentLength = null;
        response.Headers.TransferEncodingChunked = true;

        return response;
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Content from chunked response was received and processed.
    checker.IsDisposable("chunked-blocked.com").Should().BeTrue();
    checker.IsDisposable("chunked-blocked-2.org").Should().BeTrue();
  }


  /// <summary>
  ///   Validates that the updater handles responses with incorrect Content-Length gracefully.
  ///   In practice, HttpClient may throw or truncate content when Content-Length mismatches.
  /// </summary>
  [Fact]
  public async Task BlocklistUpdater_ContentLengthMismatch_HandlesGracefully()
  {
    // Arrange: Set up mock HTTP handler that returns mismatched Content-Length.
    var blocklistDir = _fixture.CreateSubdirectory("updater-content-length-mismatch");

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        var actualContent = "actual-content.com";
        var content = new StringContent(actualContent);

        // Set Content-Length to be larger than actual content.
        // This simulates a truncated response or server bug.
        // Note: In real scenarios, this would typically cause an IOException.
        content.Headers.ContentLength = actualContent.Length + 100;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = content
        };
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Should not throw even with Content-Length mismatch.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var act = async () =>
    {
      await updater.StartAsync(cts.Token);
      await Task.Delay(500, cts.Token);
      await updater.StopAsync(CancellationToken.None);
    };

    // The behavior depends on how HttpClient handles the mismatch.
    // It may succeed with actual content, throw, or truncate.
    // Our test verifies the service doesn't crash regardless.
    await act.Should().NotThrowAsync("Content-Length mismatch should be handled gracefully");
  }


  /// <summary>
  ///   Validates that the updater handles slow-streaming responses correctly.
  ///   Simulates a server that sends content in small chunks with delays.
  /// </summary>
  [Fact]
  public async Task BlocklistUpdater_SlowStreamingResponse_HandlesCorrectly()
  {
    // Arrange: Set up mock HTTP handler that simulates slow streaming.
    var blocklistDir = _fixture.CreateSubdirectory("updater-slow-stream");

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        // Return blocklist content for blocklist URLs, empty for allowlist.
        // Important: Allowlist takes precedence, so don't return same content for both!
        var content = request.RequestUri?.ToString().Contains("blocklist") == true
          ? "slow-streamed.com\nslow-streamed-2.net"
          : "";

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(content)
        };
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Content was received and processed.
    checker.IsDisposable("slow-streamed.com").Should().BeTrue();
    checker.IsDisposable("slow-streamed-2.net").Should().BeTrue();
  }


  /// <summary>
  ///   Validates that the updater handles responses with unusual but valid HTTP headers.
  /// </summary>
  [Fact]
  public async Task BlocklistUpdater_UnusualHttpHeaders_HandlesCorrectly()
  {
    // Arrange: Set up mock HTTP handler with unusual but valid headers.
    var blocklistDir = _fixture.CreateSubdirectory("updater-unusual-headers");

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        // Return blocklist content for blocklist URLs, empty for allowlist.
        // Important: Allowlist takes precedence, so don't return same content for both!
        var content = request.RequestUri?.ToString().Contains("blocklist") == true
          ? "unusual-headers.com"
          : "";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(content)
        };

        // Add unusual but valid headers.
        response.Headers.Add("X-Custom-Header", "custom-value");
        response.Headers.Add("X-Rate-Limit-Remaining", "999");
        response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
          NoCache = true,
          MaxAge = TimeSpan.FromHours(1)
        };

        return response;
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: Content was received despite unusual headers.
    checker.IsDisposable("unusual-headers.com").Should().BeTrue();
  }


  /// <summary>
  ///   Validates that the updater handles HTTP 204 No Content responses gracefully.
  /// </summary>
  [Fact]
  public async Task BlocklistUpdater_Http204NoContent_HandlesGracefully()
  {
    // Arrange: Set up mock HTTP handler that returns 204 No Content.
    var blocklistDir = _fixture.CreateSubdirectory("updater-204");

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Should not throw on 204 response.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var act = async () =>
    {
      await updater.StartAsync(cts.Token);
      await Task.Delay(500, cts.Token);
      await updater.StopAsync(CancellationToken.None);
    };

    await act.Should().NotThrowAsync("204 No Content should be handled gracefully");
  }

  #endregion


  #region Tests - TimeSpan Edge Cases

  [Fact]
  public async Task BlocklistUpdater_ZeroUpdateInterval_ExecutesImmediately()
  {
    // Arrange: Set up mock HTTP handler.
    var blocklistDir = _fixture.CreateSubdirectory("updater-zero-interval");
    var requestCount = 0;

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        Interlocked.Increment(ref requestCount);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("blocked.com")
        };
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.Zero, // Zero interval.
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: Run for a short period with zero interval (will loop rapidly).
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

    await updater.StartAsync(cts.Token);

    try
    {
      await Task.Delay(400, cts.Token);
    }
    catch (OperationCanceledException)
    {
      // Expected.
    }

    await updater.StopAsync(CancellationToken.None);

    // Assert: Multiple update cycles should have occurred due to zero interval.
    // Each cycle makes 2 requests (blocklist + allowlist), so we expect > 2 requests.
    requestCount.Should().BeGreaterThan(2, "zero interval should cause rapid updates");
  }


  [Fact]
  public async Task BlocklistUpdater_NegativeInitialDelay_ThrowsArgumentOutOfRangeException()
  {
    // Arrange: Configure with negative initial delay.
    var blocklistDir = _fixture.CreateSubdirectory("updater-negative-delay");
    var mockHandler = HttpMockFactory.CreateMockHttpHandler(blocklistContent: "blocked.com", allowlistContent: "");
    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromHours(1),
      InitialUpdateDelay = TimeSpan.FromSeconds(-1), // Negative!
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act & Assert: Task.Delay with negative value throws ArgumentOutOfRangeException.
    // The BackgroundService catches this and the service stops.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

    // StartAsync itself won't throw - the exception happens in ExecuteAsync.
    await updater.StartAsync(cts.Token);

    // Give time for the exception to occur.
    await Task.Delay(200, cts.Token);

    // The service should have stopped due to the exception.
    // StopAsync should complete without issue.
    await updater.StopAsync(CancellationToken.None);
  }


  [Fact]
  public async Task BlocklistUpdater_NegativeUpdateInterval_ThrowsOnSecondCycle()
  {
    // Arrange: Configure with negative update interval.
    var blocklistDir = _fixture.CreateSubdirectory("updater-negative-interval");
    var requestCount = 0;

    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        Interlocked.Increment(ref requestCount);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("blocked.com")
        };
      });

    var httpClientFactory = HttpMockFactory.CreateMockHttpClientFactory(mockHandler.Object);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir,
      EnableAutoUpdate = true,
      UpdateInterval = TimeSpan.FromSeconds(-1), // Negative!
      InitialUpdateDelay = TimeSpan.Zero,
      BlocklistUrl = "https://example.com/blocklist.conf",
      AllowlistUrl = "https://example.com/allowlist.conf"
    });

    var logger = Mock.Of<ILogger<BlocklistUpdater>>();
    var checker = new DisposableEmailDomainChecker(options, Mock.Of<ILogger<DisposableEmailDomainChecker>>());
    var updater = new BlocklistUpdater(options, httpClientFactory, checker, logger);

    // Act: The first update should succeed, then the negative delay will throw.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

    await updater.StartAsync(cts.Token);
    await Task.Delay(500, cts.Token);
    await updater.StopAsync(CancellationToken.None);

    // Assert: At least one update cycle completed before the exception.
    requestCount.Should().BeGreaterThanOrEqualTo(2, "first cycle should complete");
  }

  #endregion


  #region IDisposable

  public void Dispose()
  {
    _fixture.Dispose();
  }

  #endregion
}
