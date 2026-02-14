namespace Alos.Email.Validation;

using System.Text.RegularExpressions;
using Configuration;
using Microsoft.Extensions.Options;

/// <summary>
///   Orchestrates comprehensive email validation including format, blocklists, and MX records.
/// </summary>
public sealed partial class EmailValidationService(
  IDisposableEmailDomainChecker    disposableChecker,
  IMxRecordValidator               mxValidator,
  IOptions<EmailValidationOptions> options) : IEmailValidationService
{
  #region Constants & Statics

  /// <summary>
  ///   HTML5 Living Standard email validation regex pattern.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     This pattern is based on the HTML5 Living Standard specification, which defines a
  ///     "willful violation" of RFC 5322 to provide practical validation that works for
  ///     99.99% of real-world email addresses.
  ///   </para>
  ///   <para>
  ///     Source: https://html.spec.whatwg.org/multipage/input.html#valid-e-mail-address
  ///   </para>
  /// </remarks>
  private const string Html5EmailPattern =
    @"^[a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$";

  #endregion


  #region Properties & Fields - Non-Public

  /// <summary>The email validation configuration options.</summary>
  private readonly EmailValidationOptions _options = options.Value;

  /// <summary>
  ///   HashSet for O(1) lookup of whitelisted MX domains.
  ///   Populated lazily from options on first access.
  /// </summary>
  private HashSet<string>? _whitelistedMxDomainsSet;

  #endregion


  #region Methods - Public

  /// <inheritdoc />
  public async Task<EmailValidationResult> ValidateEmailAsync(string email, CancellationToken ct = default)
  {
    // 1. Validate format using HTML5 Living Standard regex
    if (!ValidateFormat(email))
      return EmailValidationResult.Failure(EmailValidationError.InvalidFormat);

    var domain = EmailNormalizer.ExtractDomain(email);

    // Should not happen after format validation, but be defensive
    if (domain is null)
      return EmailValidationResult.Failure(EmailValidationError.InvalidFormat);

    // 2. Check relay service blocklist
    if (RelayServiceBlocklist.IsRelayService(domain))
      return EmailValidationResult.Failure(EmailValidationError.RelayService);

    // 3. Check disposable domain blocklist
    if (disposableChecker.IsDisposable(domain))
      return EmailValidationResult.Failure(EmailValidationError.Disposable);

    // 4. Verify MX records (async DNS lookup)
    //    Skip if domain is whitelisted (e.g., test domains without real DNS)
    if (!IsMxWhitelisted(domain) && !await mxValidator.HasValidMxRecordsAsync(domain, ct))
      return EmailValidationResult.Failure(EmailValidationError.InvalidDomain);

    return EmailValidationResult.Success();
  }


  /// <inheritdoc />
  public async Task<EmailValidationResult> ValidateMxAsync(string email, CancellationToken ct = default)
  {
    var domain = EmailNormalizer.ExtractDomain(email);

    if (domain is null)
      return EmailValidationResult.Failure(EmailValidationError.InvalidFormat);

    // Skip MX validation for whitelisted domains (e.g., test domains)
    if (IsMxWhitelisted(domain))
      return EmailValidationResult.Success();

    // Perform async DNS MX record lookup
    if (!await mxValidator.HasValidMxRecordsAsync(domain, ct))
      return EmailValidationResult.Failure(EmailValidationError.InvalidDomain);

    return EmailValidationResult.Success();
  }


  /// <inheritdoc />
  public bool ValidateFormat(string email)
  {
    if (string.IsNullOrWhiteSpace(email))
      return false;

    // Use source-generated regex for optimal performance
    return Html5EmailRegex().IsMatch(email);
  }


  /// <inheritdoc />
  public bool IsRelayService(string email)
  {
    var domain = EmailNormalizer.ExtractDomain(email);

    return RelayServiceBlocklist.IsRelayService(domain);
  }


  /// <inheritdoc />
  public bool IsDisposable(string email)
  {
    var domain = EmailNormalizer.ExtractDomain(email);

    return disposableChecker.IsDisposable(domain);
  }

  #endregion


  #region Methods - Private

  /// <summary>
  ///   Checks if a domain is in the MX whitelist (bypasses MX validation).
  /// </summary>
  /// <param name="domain">The domain to check.</param>
  /// <returns>True if the domain is whitelisted for MX validation bypass.</returns>
  private bool IsMxWhitelisted(string domain)
  {
    // Lazy initialization of the HashSet for O(1) lookups
    _whitelistedMxDomainsSet ??= new HashSet<string>(
      _options.WhitelistedMxDomains,
      StringComparer.OrdinalIgnoreCase);

    return _whitelistedMxDomainsSet.Contains(domain);
  }


  /// <summary>
  ///   Source-generated regex for HTML5 Living Standard email validation.
  ///   Using source generation for optimal performance (no runtime compilation).
  /// </summary>
  [GeneratedRegex(Html5EmailPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant)]
  private static partial Regex Html5EmailRegex();

  #endregion
}
