namespace Alos.Email.Validation.Configuration;

/// <summary>
///   Configuration options for email validation services.
/// </summary>
/// <example>
///   <code>
///   {
///     "EmailValidation": {
///       "EnableAutoUpdate": true,
///       "UpdateInterval": "1.00:00:00",
///       "CustomBlocklist": ["spammer.com", "badactor.net"],
///       "CustomAllowlist": ["legitimate-service.com"],
///       "CustomBlocklistUrls": ["https://example.com/my-blocklist.txt"],
///       "CustomAllowlistUrls": ["https://example.com/my-allowlist.txt"]
///     }
///   }
///   </code>
/// </example>
public sealed class EmailValidationOptions
{
  #region Constants & Statics

  /// <summary>
  ///   The configuration section name.
  /// </summary>
  public const string SectionName = "EmailValidation";

  /// <summary>
  ///   The default blocklist directory name within the application data folder.
  /// </summary>
  private const string DefaultBlocklistDirectoryName = "email-blocklists";

  /// <summary>
  ///   The application name used for the data directory.
  /// </summary>
  private const string ApplicationName = "alos";


  /// <summary>
  ///   Gets the default cross-platform blocklist directory path.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     Returns a path in the user's local application data folder:
  ///   </para>
  ///   <list type="bullet">
  ///     <item>Windows: <c>%LOCALAPPDATA%\alos\email-blocklists</c></item>
  ///     <item>Linux: <c>~/.local/share/alos/email-blocklists</c></item>
  ///     <item>macOS: <c>~/Library/Application Support/alos/email-blocklists</c></item>
  ///   </list>
  /// </remarks>
  public static string DefaultBlocklistDirectory =>
    Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      ApplicationName,
      DefaultBlocklistDirectoryName);

  #endregion


  #region Properties & Fields - Public

  /// <summary>
  ///   Gets or sets the path to directory containing blocklist files.
  ///   If null, uses embedded resources only.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     When <see cref="EnableAutoUpdate"/> is true, downloaded blocklists are saved here.
  ///     All blocklist and allowlist files in this directory are loaded and merged at startup and on reload.
  ///   </para>
  /// </remarks>
  public string? BlocklistDirectory { get; set; }

  /// <summary>
  ///   Gets or sets whether automatic blocklist updates from remote URLs are enabled.
  ///   Requires <see cref="BlocklistDirectory"/> to be set.
  /// </summary>
  public bool EnableAutoUpdate { get; set; } = false;

  /// <summary>
  ///   Gets or sets the interval between update checks. Default: 24 hours.
  /// </summary>
  public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromHours(24);

  /// <summary>
  ///   Gets or sets the initial delay before the first update check. Default: 30 seconds.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     This delay prevents startup congestion by waiting before the first network request.
  ///     Set to <see cref="TimeSpan.Zero"/> for immediate updates (useful in testing).
  ///   </para>
  /// </remarks>
  public TimeSpan InitialUpdateDelay { get; set; } = TimeSpan.FromSeconds(30);

  /// <summary>
  ///   Gets or sets the URL for the primary blocklist.
  ///   Default: disposable-email-domains GitHub repository.
  /// </summary>
  public string BlocklistUrl { get; set; } =
    "https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/master/disposable_email_blocklist.conf";

  /// <summary>
  ///   Gets or sets the URL for the primary allowlist.
  ///   Default: disposable-email-domains GitHub repository.
  /// </summary>
  public string AllowlistUrl { get; set; } =
    "https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/master/allowlist.conf";

  /// <summary>
  ///   Gets or sets additional URLs for custom blocklists.
  ///   These are fetched by the auto-updater and merged with the primary blocklist.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     Use this for organization-specific or third-party blocklists that you want
  ///     to keep synchronized. Each URL should return a text file with one domain per line.
  ///     Lines starting with # are treated as comments and ignored.
  ///   </para>
  /// </remarks>
  public List<string> CustomBlocklistUrls { get; set; } = [];

  /// <summary>
  ///   Gets or sets additional URLs for custom allowlists.
  ///   These are fetched by the auto-updater and merged with the primary allowlist.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     Use this to override false positives from external allowlist sources.
  ///     The allowlist takes precedence over the blocklist.
  ///   </para>
  /// </remarks>
  public List<string> CustomAllowlistUrls { get; set; } = [];

  /// <summary>
  ///   Gets or sets custom domains to add to the blocklist (inline configuration).
  ///   These are merged with the base/downloaded blocklist at runtime.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     Use this for a small number of application-specific blocks that don't need
  ///     to be fetched from a URL. For larger lists, use <see cref="CustomBlocklistUrls"/>.
  ///   </para>
  /// </remarks>
  public List<string> CustomBlocklist { get; set; } = [];

  /// <summary>
  ///   Gets or sets custom domains to add to the allowlist (inline configuration).
  ///   These are merged with the base/downloaded allowlist at runtime.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     Use this to override false positives for specific domains.
  ///     The allowlist takes precedence over the blocklist.
  ///   </para>
  /// </remarks>
  public List<string> CustomAllowlist { get; set; } = [];

  #endregion
}
