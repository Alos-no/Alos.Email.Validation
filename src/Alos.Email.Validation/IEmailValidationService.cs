namespace Alos.Email.Validation;

/// <summary>
///   Orchestrates comprehensive email validation including format, blocklists, and MX records.
/// </summary>
public interface IEmailValidationService
{
  /// <summary>
  ///   Validates an email address against all anti-abuse rules.
  /// </summary>
  /// <param name="email">The email address to validate.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>The validation result with error details if invalid.</returns>
  Task<EmailValidationResult> ValidateAsync(string email, CancellationToken ct = default);

  /// <summary>
  ///   Checks if the email domain is a blocked relay service (synchronous).
  /// </summary>
  /// <param name="email">The email address to check.</param>
  /// <returns>True if the domain is a relay service.</returns>
  bool IsRelayService(string email);

  /// <summary>
  ///   Checks if the email domain is a known disposable provider (synchronous).
  /// </summary>
  /// <param name="email">The email address to check.</param>
  /// <returns>True if the domain is disposable.</returns>
  bool IsDisposable(string email);

  /// <summary>
  ///   Normalizes an email address using provider-specific rules.
  /// </summary>
  /// <param name="email">The email address to normalize.</param>
  /// <returns>The normalized email address.</returns>
  string Normalize(string email);
}
