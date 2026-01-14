namespace Alos.Email.Validation;

/// <summary>
///   Validates that an email domain has valid MX records and can receive email.
/// </summary>
public interface IMxRecordValidator
{
  /// <summary>
  ///   Verifies that the domain has valid MX records.
  /// </summary>
  /// <param name="domain">The email domain to check (e.g., "gmail.com").</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>True if the domain has at least one MX record, or on timeout (fail-open).</returns>
  Task<bool> HasValidMxRecordsAsync(string domain, CancellationToken ct = default);
}
