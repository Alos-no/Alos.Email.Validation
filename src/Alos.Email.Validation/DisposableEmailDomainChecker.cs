namespace Alos.Email.Validation;

using Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///   Checks if an email domain is a known disposable/temporary email provider.
///   Uses embedded blocklist from disposable-email-domains (~38,000 domains).
/// </summary>
/// <remarks>
///   <para>
///     Supports hot-reload of blocklists from disk via <see cref="ReloadFromDisk"/>.
///     Uses reader-writer lock for thread-safe access during reloads.
///   </para>
///   <para>
///     When loading from disk, all files matching the blocklist/allowlist naming
///     conventions are loaded and merged together. This includes:
///     - Primary: disposable_email_blocklist.conf, disposable_email_allowlist.conf
///     - Custom: custom_blocklist_*.conf, custom_allowlist_*.conf
///   </para>
///   <para>
///     Inline custom blocklists and allowlists from configuration are also merged.
///     The allowlist always takes precedence over the blocklist.
///   </para>
/// </remarks>
public sealed class DisposableEmailDomainChecker : IDisposableEmailDomainChecker, IDisposable
{
  #region Fields

  private readonly ILogger<DisposableEmailDomainChecker> _logger;
  private readonly EmailValidationOptions _options;
  private readonly ReaderWriterLockSlim _lock = new();

  // Note: No volatile needed - ReaderWriterLockSlim provides necessary memory barriers.
  private HashSet<string> _blockedDomains;
  private HashSet<string> _allowlist;

  /// <summary>
  ///   Tracks whether the instance has been disposed.
  /// </summary>
  private bool _disposed;

  #endregion


  #region Constructors

  /// <summary>
  ///   Initializes a new instance of the <see cref="DisposableEmailDomainChecker"/> class.
  /// </summary>
  public DisposableEmailDomainChecker(
    IOptions<EmailValidationOptions> options,
    ILogger<DisposableEmailDomainChecker> logger)
  {
    _logger = logger;
    _options = options.Value;

    // Load base lists from disk or embedded resources.
    var diskDirectory = _options.BlocklistDirectory;

    if (!string.IsNullOrEmpty(diskDirectory) && Directory.Exists(diskDirectory))
    {
      _logger.LogInformation("Loading blocklists from disk: {Directory}", diskDirectory);
      (_blockedDomains, _allowlist) = LoadFromDisk(diskDirectory);
    }
    else
    {
      _logger.LogInformation("Loading blocklists from embedded resources");
      (_blockedDomains, _allowlist) = LoadFromEmbeddedResources();
    }

    // Merge inline custom lists from configuration.
    MergeInlineCustomLists(_options, _blockedDomains, _allowlist);

    _logger.LogInformation(
      "Loaded {BlocklistCount} blocked domains and {AllowlistCount} allowlisted domains (including custom)",
      _blockedDomains.Count,
      _allowlist.Count);
  }

  #endregion


  #region Methods - Public

  /// <inheritdoc />
  /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
  public bool IsDisposable(string? domain)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (string.IsNullOrWhiteSpace(domain))
      return false;

    _lock.EnterReadLock();

    try
    {
      // Allowlist takes precedence (false positive prevention).
      if (_allowlist.Contains(domain))
        return false;

      return _blockedDomains.Contains(domain);
    }
    finally
    {
      _lock.ExitReadLock();
    }
  }


  /// <inheritdoc />
  /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
  public void ReloadFromDisk(string directory)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    _logger.LogInformation("Reloading blocklists from disk: {Directory}", directory);

    var (newBlocklist, newAllowlist) = LoadFromDisk(directory);

    // Merge inline custom lists into the newly loaded base lists.
    MergeInlineCustomLists(_options, newBlocklist, newAllowlist);

    _lock.EnterWriteLock();

    try
    {
      _blockedDomains = newBlocklist;
      _allowlist = newAllowlist;
    }
    finally
    {
      _lock.ExitWriteLock();
    }

    _logger.LogInformation(
      "Reloaded {BlocklistCount} blocked domains and {AllowlistCount} allowlisted domains (including custom)",
      newBlocklist.Count,
      newAllowlist.Count);
  }

  #endregion


  #region Methods - Private

  /// <summary>
  ///   Merges inline custom blocklist and allowlist from configuration into the provided sets.
  /// </summary>
  /// <param name="options">The configuration options containing inline custom lists.</param>
  /// <param name="blocklist">The blocklist to merge into.</param>
  /// <param name="allowlist">The allowlist to merge into.</param>
  private void MergeInlineCustomLists(
    EmailValidationOptions options,
    HashSet<string> blocklist,
    HashSet<string> allowlist)
  {
    var customBlocklistCount = 0;
    var customAllowlistCount = 0;

    // Merge inline custom blocklist from configuration.
    if (options.CustomBlocklist.Count > 0)
    {
      foreach (var domain in options.CustomBlocklist)
      {
        if (!string.IsNullOrWhiteSpace(domain) && blocklist.Add(domain.Trim().ToLowerInvariant()))
        {
          customBlocklistCount++;
        }
      }
    }

    // Merge inline custom allowlist from configuration.
    if (options.CustomAllowlist.Count > 0)
    {
      foreach (var domain in options.CustomAllowlist)
      {
        if (!string.IsNullOrWhiteSpace(domain) && allowlist.Add(domain.Trim().ToLowerInvariant()))
        {
          customAllowlistCount++;
        }
      }
    }

    if (customBlocklistCount > 0 || customAllowlistCount > 0)
    {
      _logger.LogInformation(
        "Merged {CustomBlocklistCount} inline blocked domains and {CustomAllowlistCount} inline allowed domains",
        customBlocklistCount,
        customAllowlistCount);
    }
  }


  /// <summary>
  ///   Loads all blocklist and allowlist files from disk and merges them.
  /// </summary>
  private (HashSet<string> Blocklist, HashSet<string> Allowlist) LoadFromDisk(string directory)
  {
    var blocklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var allowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Load primary blocklist if it exists.
    var primaryBlocklistPath = Path.Combine(directory, BlocklistFileNames.PrimaryBlocklist);

    if (File.Exists(primaryBlocklistPath))
    {
      var domains = LoadDomainsFromFile(primaryBlocklistPath);
      blocklist.UnionWith(domains);

      _logger.LogDebug("Loaded {Count} domains from primary blocklist", domains.Count);
    }

    // Load primary allowlist if it exists.
    var primaryAllowlistPath = Path.Combine(directory, BlocklistFileNames.PrimaryAllowlist);

    if (File.Exists(primaryAllowlistPath))
    {
      var domains = LoadDomainsFromFile(primaryAllowlistPath);
      allowlist.UnionWith(domains);

      _logger.LogDebug("Loaded {Count} domains from primary allowlist", domains.Count);
    }

    // Load all custom blocklist files (custom_blocklist_*.conf).
    var customBlocklistFiles = Directory.GetFiles(directory, $"{BlocklistFileNames.CustomBlocklistPrefix}*{BlocklistFileNames.FileExtension}");

    foreach (var file in customBlocklistFiles)
    {
      var domains = LoadDomainsFromFile(file);
      blocklist.UnionWith(domains);

      _logger.LogDebug("Loaded {Count} domains from {File}", domains.Count, Path.GetFileName(file));
    }

    // Load all custom allowlist files (custom_allowlist_*.conf).
    var customAllowlistFiles = Directory.GetFiles(directory, $"{BlocklistFileNames.CustomAllowlistPrefix}*{BlocklistFileNames.FileExtension}");

    foreach (var file in customAllowlistFiles)
    {
      var domains = LoadDomainsFromFile(file);
      allowlist.UnionWith(domains);

      _logger.LogDebug("Loaded {Count} domains from {File}", domains.Count, Path.GetFileName(file));
    }

    _logger.LogDebug(
      "Loaded from disk: {BlocklistCount} blocked domains ({BlocklistFiles} files), {AllowlistCount} allowed domains ({AllowlistFiles} files)",
      blocklist.Count,
      1 + customBlocklistFiles.Length,
      allowlist.Count,
      1 + customAllowlistFiles.Length);

    return (blocklist, allowlist);
  }


  /// <summary>
  ///   Loads blocklists from embedded resources.
  /// </summary>
  private (HashSet<string> Blocklist, HashSet<string> Allowlist) LoadFromEmbeddedResources()
  {
    var allowlist = LoadDomainsFromResource(BlocklistFileNames.PrimaryAllowlist);
    var blocklist = LoadDomainsFromResource(BlocklistFileNames.PrimaryBlocklist);

    return (blocklist, allowlist);
  }


  /// <summary>
  ///   Loads domains from a file.
  /// </summary>
  private static HashSet<string> LoadDomainsFromFile(string filePath)
  {
    return ParseDomainLines(File.ReadLines(filePath));
  }


  /// <summary>
  ///   Loads domains from an embedded resource.
  /// </summary>
  private HashSet<string> LoadDomainsFromResource(string resourceFileName)
  {
    var assembly = typeof(DisposableEmailDomainChecker).Assembly;
    var resourceName = $"Alos.Email.Validation.Resources.{resourceFileName}";

    using var stream = assembly.GetManifestResourceStream(resourceName);

    if (stream is null)
    {
      // Return empty set if resource not found (graceful degradation).
      _logger.LogWarning("Embedded resource not found: {ResourceName}", resourceName);

      return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    using var reader = new StreamReader(stream);

    return ParseDomainLines(ReadLines(reader));
  }


  /// <summary>
  ///   Parses domain lines from an enumerable of strings, filtering comments and empty lines.
  /// </summary>
  /// <param name="lines">The lines to parse.</param>
  /// <returns>A case-insensitive HashSet of domain names.</returns>
  private static HashSet<string> ParseDomainLines(IEnumerable<string> lines)
  {
    var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var line in lines)
    {
      var trimmed = line.Trim();

      if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith('#'))
      {
        domains.Add(trimmed);
      }
    }

    return domains;
  }


  /// <summary>
  ///   Reads all lines from a StreamReader as an enumerable.
  /// </summary>
  /// <param name="reader">The StreamReader to read from.</param>
  /// <returns>An enumerable of lines.</returns>
  private static IEnumerable<string> ReadLines(StreamReader reader)
  {
    while (reader.ReadLine() is { } line)
    {
      yield return line;
    }
  }

  #endregion


  #region IDisposable

  /// <inheritdoc />
  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;
    _lock.Dispose();
  }

  #endregion
}
