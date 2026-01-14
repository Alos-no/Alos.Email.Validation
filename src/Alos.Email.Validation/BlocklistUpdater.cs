namespace Alos.Email.Validation;

using Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///   Background service that periodically updates blocklists from remote URLs.
/// </summary>
/// <remarks>
///   <para>
///     Downloads the primary blocklist and allowlist from the configured URLs,
///     as well as any custom blocklist/allowlist URLs. All downloaded files are
///     saved to the configured directory and then merged by the
///     <see cref="IDisposableEmailDomainChecker"/> on reload.
///   </para>
/// </remarks>
public sealed class BlocklistUpdater : BackgroundService
{
  #region Fields

  private readonly EmailValidationOptions _options;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IDisposableEmailDomainChecker _checker;
  private readonly ILogger<BlocklistUpdater> _logger;

  /// <summary>
  ///   The resolved blocklist directory (configured or default).
  ///   Marked volatile for memory visibility safety across async continuations,
  ///   though current code flow ensures write-before-read ordering.
  /// </summary>
  private volatile string _resolvedDirectory = null!;

  #endregion


  #region Constructors

  /// <summary>
  ///   Initializes a new instance of the <see cref="BlocklistUpdater"/> class.
  /// </summary>
  public BlocklistUpdater(
    IOptions<EmailValidationOptions> options,
    IHttpClientFactory httpClientFactory,
    IDisposableEmailDomainChecker checker,
    ILogger<BlocklistUpdater> logger)
  {
    _options = options.Value;
    _httpClientFactory = httpClientFactory;
    _checker = checker;
    _logger = logger;
  }

  #endregion


  #region Methods - Protected

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken ct)
  {
    if (!_options.EnableAutoUpdate)
    {
      _logger.LogInformation("Blocklist auto-update is disabled");

      return;
    }

    // Use configured directory or fall back to cross-platform default.
    var directory = string.IsNullOrEmpty(_options.BlocklistDirectory)
      ? EmailValidationOptions.DefaultBlocklistDirectory
      : _options.BlocklistDirectory;

    // Ensure directory exists.
    Directory.CreateDirectory(directory);

    _logger.LogInformation(
      "Using blocklist directory: {Directory}",
      directory);

    // Store resolved directory for use in update loop.
    _resolvedDirectory = directory;

    _logger.LogInformation(
      "Blocklist auto-update started. Update interval: {Interval}, Initial delay: {InitialDelay}",
      _options.UpdateInterval,
      _options.InitialUpdateDelay);

    // Initial delay to avoid startup congestion (configurable, default 30s).
    if (_options.InitialUpdateDelay > TimeSpan.Zero)
    {
      await Task.Delay(_options.InitialUpdateDelay, ct);
    }

    while (!ct.IsCancellationRequested)
    {
      try
      {
        await UpdateBlocklistsAsync(ct);
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to update blocklists, will retry after interval");
      }

      await Task.Delay(_options.UpdateInterval, ct);
    }

    _logger.LogInformation("Blocklist auto-update stopped");
  }

  #endregion


  #region Methods - Private

  /// <summary>
  ///   Downloads and updates all blocklists from configured URLs.
  /// </summary>
  private async Task UpdateBlocklistsAsync(CancellationToken ct)
  {
    _logger.LogDebug("Checking for blocklist updates...");

    var client = _httpClientFactory.CreateClient();
    var directory = _resolvedDirectory;

    // Download primary blocklist.
    await DownloadAndSaveAsync(
      client,
      _options.BlocklistUrl,
      Path.Combine(directory, BlocklistFileNames.PrimaryBlocklist),
      "primary blocklist",
      ct);

    // Download primary allowlist.
    await DownloadAndSaveAsync(
      client,
      _options.AllowlistUrl,
      Path.Combine(directory, BlocklistFileNames.PrimaryAllowlist),
      "primary allowlist",
      ct);

    // Download custom blocklists.
    for (var i = 0; i < _options.CustomBlocklistUrls.Count; i++)
    {
      var url = _options.CustomBlocklistUrls[i];
      var filename = $"{BlocklistFileNames.CustomBlocklistPrefix}{i:D3}{BlocklistFileNames.FileExtension}";

      await DownloadAndSaveAsync(
        client,
        url,
        Path.Combine(directory, filename),
        $"custom blocklist #{i + 1}",
        ct);
    }

    // Download custom allowlists.
    for (var i = 0; i < _options.CustomAllowlistUrls.Count; i++)
    {
      var url = _options.CustomAllowlistUrls[i];
      var filename = $"{BlocklistFileNames.CustomAllowlistPrefix}{i:D3}{BlocklistFileNames.FileExtension}";

      await DownloadAndSaveAsync(
        client,
        url,
        Path.Combine(directory, filename),
        $"custom allowlist #{i + 1}",
        ct);
    }

    // Reload checker with all downloaded lists.
    _checker.ReloadFromDisk(directory);

    _logger.LogInformation(
      "Blocklists updated successfully. Primary + {CustomBlocklistCount} custom blocklists + {CustomAllowlistCount} custom allowlists",
      _options.CustomBlocklistUrls.Count,
      _options.CustomAllowlistUrls.Count);
  }


  /// <summary>
  ///   Downloads content from a URL and saves it to a file.
  /// </summary>
  private async Task DownloadAndSaveAsync(
    HttpClient client,
    string url,
    string filePath,
    string description,
    CancellationToken ct)
  {
    try
    {
      var content = await client.GetStringAsync(url, ct);
      await File.WriteAllTextAsync(filePath, content, ct);

      _logger.LogDebug("Downloaded {Description}: {Path}", description, filePath);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogWarning(
        ex,
        "Failed to download {Description} from {Url}",
        description,
        url);

      // Don't fail the entire update if a single custom URL fails.
      // The primary lists and other custom lists may still succeed.
    }
  }

  #endregion
}
