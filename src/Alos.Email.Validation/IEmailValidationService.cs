namespace Alos.Email.Validation;

/// <summary>
///   Orchestrates comprehensive email validation including format, blocklists, and MX records.
/// </summary>
public interface IEmailValidationService
{
  /// <summary>
  ///   Performs complete email validation: format, relay service, disposable domain, and MX records.
  /// </summary>
  /// <param name="email">The email address to validate.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>The validation result with error details if invalid.</returns>
  /// <remarks>
  ///   <para>
  ///     This method performs all validation checks in sequence:
  ///   </para>
  ///   <list type="number">
  ///     <item><description>Format validation (HTML5 Living Standard compliant)</description></item>
  ///     <item><description>Relay service blocklist check</description></item>
  ///     <item><description>Disposable domain blocklist check</description></item>
  ///     <item><description>MX record verification (async DNS lookup)</description></item>
  ///   </list>
  ///   <para>
  ///     Use this for complete validation. If you've already performed synchronous checks
  ///     (e.g., via FluentValidation), use <see cref="ValidateMxAsync"/> to avoid duplication.
  ///   </para>
  /// </remarks>
  Task<EmailValidationResult> ValidateEmailAsync(string email, CancellationToken ct = default);

  /// <summary>
  ///   Validates only the MX records for an email domain (async DNS lookup).
  /// </summary>
  /// <param name="email">The email address to validate.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>The validation result with error details if invalid.</returns>
  /// <remarks>
  ///   <para>
  ///     Use this method when format, relay, and disposable checks have already been performed
  ///     (e.g., by FluentValidation validators) and only MX verification is needed.
  ///   </para>
  ///   <para>
  ///     Domains listed in <c>WhitelistedMxDomains</c> bypass MX validation and return success.
  ///   </para>
  /// </remarks>
  Task<EmailValidationResult> ValidateMxAsync(string email, CancellationToken ct = default);

  /// <summary>
  ///   Validates the email address format using HTML5 Living Standard rules.
  /// </summary>
  /// <param name="email">The email address to validate.</param>
  /// <returns>True if the format is valid; false otherwise.</returns>
  /// <remarks>
  ///   <para>
  ///     Uses the HTML5 Living Standard email regex pattern, which is a practical implementation
  ///     of RFC 5322 that validates 99.99% of real-world email addresses while avoiding
  ///     the complexity of full RFC compliance.
  ///   </para>
  ///   <para>
  ///     This is a synchronous check suitable for use in FluentValidation validators.
  ///   </para>
  /// </remarks>
  bool ValidateFormat(string email);

  /// <summary>
  ///   Checks if the email domain is a blocked relay service (synchronous).
  /// </summary>
  /// <param name="email">The email address to check.</param>
  /// <returns>True if the domain is a relay service (e.g., Apple Hide My Email, Firefox Relay).</returns>
  bool IsRelayService(string email);

  /// <summary>
  ///   Checks if the email domain is a known disposable provider (synchronous).
  /// </summary>
  /// <param name="email">The email address to check.</param>
  /// <returns>True if the domain is disposable (e.g., Mailinator, Guerrilla Mail).</returns>
  bool IsDisposable(string email);

  /// <summary>
  ///   Normalizes an email address using provider-specific rules.
  /// </summary>
  /// <param name="email">The email address to normalize.</param>
  /// <returns>The normalized email address.</returns>
  /// <remarks>
  ///   <para>
  ///     Applies provider-specific normalization rules (e.g., removing dots and plus-addressing
  ///     from Gmail addresses) to produce a canonical form for duplicate detection.
  ///   </para>
  /// </remarks>
  string Normalize(string email);
}
