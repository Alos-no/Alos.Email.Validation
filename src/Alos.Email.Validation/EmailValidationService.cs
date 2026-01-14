namespace Alos.Email.Validation;

/// <summary>
///   Orchestrates comprehensive email validation including format, blocklists, and MX records.
/// </summary>
public sealed class EmailValidationService(
  IDisposableEmailDomainChecker disposableChecker,
  IMxRecordValidator mxValidator) : IEmailValidationService
{
  #region Methods - Public

  /// <inheritdoc />
  public async Task<EmailValidationResult> ValidateAsync(string email, CancellationToken ct = default)
  {
    var domain = EmailNormalizer.ExtractDomain(email);

    if (domain is null)
      return EmailValidationResult.Failure(EmailValidationError.InvalidFormat);

    // Check relay service blocklist
    if (RelayServiceBlocklist.IsRelayService(domain))
      return EmailValidationResult.Failure(EmailValidationError.RelayService);

    // Check disposable domain blocklist
    if (disposableChecker.IsDisposable(domain))
      return EmailValidationResult.Failure(EmailValidationError.Disposable);

    // Verify MX records (async)
    if (!await mxValidator.HasValidMxRecordsAsync(domain, ct))
      return EmailValidationResult.Failure(EmailValidationError.InvalidDomain);

    return EmailValidationResult.Success();
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


  /// <inheritdoc />
  public string Normalize(string email) => EmailNormalizer.Normalize(email);

  #endregion
}
