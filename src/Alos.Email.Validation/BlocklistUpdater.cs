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
///     <see cref="IDisposableEmailDomainChecker"/> on reload, except a primary blocklist holding fewer
///     entries than <see cref="EmailValidationOptions.MinimumBlocklistEntryCount"/>, which keeps the previous file.
///   </para>
/// </remarks>
public sealed class BlocklistUpdater : BackgroundService
{
  #region Fields

  /// <summary>
  ///   Suffix of the file a download is written to before it replaces its destination; no list file pattern matches it.
  /// </summary>
  private const string DownloadTemporaryFileSuffix = ".download";

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

    // The directory the checker reads at start; never null here, because auto-update is on.
    var directory = _options.ResolveBlocklistDirectory()!;

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

    // Every list of the cycle in download order: the two primary lists, then each custom list URL.
    List<(string Url, string FileName, string Description, int MinimumEntryCount)> listDownloads =
    [
      (_options.BlocklistUrl, BlocklistFileNames.PrimaryBlocklist, "primary blocklist", _options.MinimumBlocklistEntryCount),
      (_options.AllowlistUrl, BlocklistFileNames.PrimaryAllowlist, "primary allowlist", 0),
      .. _options.CustomBlocklistUrls.Select((url, i) => (
        url,
        $"{BlocklistFileNames.CustomBlocklistPrefix}{i:D3}{BlocklistFileNames.FileExtension}",
        $"custom blocklist #{i + 1}",
        0)),
      .. _options.CustomAllowlistUrls.Select((url, i) => (
        url,
        $"{BlocklistFileNames.CustomAllowlistPrefix}{i:D3}{BlocklistFileNames.FileExtension}",
        $"custom allowlist #{i + 1}",
        0)),
    ];

    var writtenListCount = 0;

    foreach (var listDownload in listDownloads)
    {
      if (await DownloadAndSaveAsync(
        client,
        listDownload.Url,
        Path.Combine(directory, listDownload.FileName),
        listDownload.Description,
        listDownload.MinimumEntryCount,
        ct))
      {
        writtenListCount++;
      }
    }

    // Reload checker with all downloaded lists.
    _checker.ReloadFromDisk(directory);

    _logger.LogInformation(
      "Blocklist update cycle finished: {WrittenListCount} of {RequestedListCount} lists written, checker reloaded",
      writtenListCount,
      listDownloads.Count);
  }


  /// <summary>
  ///   Downloads content from a URL and saves it to a file, unless the content holds fewer entries than
  ///   a minimum entry count above 0 (then the previous file is kept and a warning is logged).
  /// </summary>
  /// <returns><c>true</c> when the destination file was replaced by the download.</returns>
  private async Task<bool> DownloadAndSaveAsync(
    HttpClient client,
    string url,
    string filePath,
    string description,
    int minimumEntryCount,
    CancellationToken ct)
  {
    try
    {
      var content = await client.GetStringAsync(url, ct);

      // Counted over the loader's own line breaks (\n, \r\n and a bare \r) and parser.
      using var contentReader = new StringReader(content);
      var downloadedEntryCount = DisposableEmailDomainChecker.ParseDomainsFromTextReader(contentReader).Count;

      if (downloadedEntryCount < minimumEntryCount)
      {
        _logger.LogWarning(
          "Downloaded {Description} from {Url} holds {DownloadedEntryCount} entries, below the minimum of {MinimumEntryCount}; the previous file is kept",
          description,
          url,
          downloadedEntryCount,
          minimumEntryCount);

        return false;
      }

      // Written beside the destination, then renamed over it, so an interrupted write never shortens the destination.
      var temporaryFilePath = filePath + DownloadTemporaryFileSuffix;

      try
      {
        await File.WriteAllTextAsync(temporaryFilePath, content, ct);
        File.Move(temporaryFilePath, filePath, overwrite: true);
      }
      catch
      {
        DeleteTemporaryFileKeepingTheOriginalFailure(temporaryFilePath);

        throw;
      }

      _logger.LogDebug("Downloaded {Description}: {Path}", description, filePath);

      return true;
    }
    catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
    {
      // An OperationCanceledException while ct is not cancelled is treated as HttpClient's own timeout.
      _logger.LogWarning(
        ex,
        "Failed to download {Description} from {Url}",
        description,
        url);

      // Don't fail the entire update if a single custom URL fails.
      // The primary lists and other custom lists may still succeed.
      return false;
    }
  }


  /// <summary>
  ///   Deletes a download's temporary file after its write or move failed. A deletion that fails too is
  ///   swallowed, so the caller rethrows the write or move failure; the next cycle overwrites the leftover file.
  /// </summary>
  private static void DeleteTemporaryFileKeepingTheOriginalFailure(string temporaryFilePath)
  {
    try
    {
      File.Delete(temporaryFilePath);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
    }
  }

  #endregion
}
