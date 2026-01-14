namespace Alos.Email.Validation;

/// <summary>
///   Constants for blocklist and allowlist file naming conventions.
/// </summary>
/// <remarks>
///   <para>
///     These constants define the expected file names for blocklist files:
///   </para>
///   <list type="bullet">
///     <item><see cref="PrimaryBlocklist"/> - Main blocklist of disposable domains</item>
///     <item><see cref="PrimaryAllowlist"/> - Allowlist to override false positives</item>
///     <item><see cref="CustomBlocklistPrefix"/> - Prefix for custom blocklist files</item>
///     <item><see cref="CustomAllowlistPrefix"/> - Prefix for custom allowlist files</item>
///   </list>
/// </remarks>
public static class BlocklistFileNames
{
  /// <summary>
  ///   Filename for the primary blocklist.
  /// </summary>
  public const string PrimaryBlocklist = "disposable_email_blocklist.conf";

  /// <summary>
  ///   Filename for the primary allowlist.
  /// </summary>
  public const string PrimaryAllowlist = "disposable_email_allowlist.conf";

  /// <summary>
  ///   Prefix for custom blocklist files (e.g., custom_blocklist_internal.conf).
  /// </summary>
  public const string CustomBlocklistPrefix = "custom_blocklist_";

  /// <summary>
  ///   Prefix for custom allowlist files (e.g., custom_allowlist_partners.conf).
  /// </summary>
  public const string CustomAllowlistPrefix = "custom_allowlist_";

  /// <summary>
  ///   File extension for all blocklist files.
  /// </summary>
  public const string FileExtension = ".conf";
}
