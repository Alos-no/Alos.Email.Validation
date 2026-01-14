namespace Alos.Email.Validation;

/// <summary>
///   Result of email validation with error details.
/// </summary>
public sealed record EmailValidationResult
{
  #region Properties & Fields - Public

  /// <summary>
  ///   Gets whether the email passed all validation checks.
  /// </summary>
  public bool IsValid { get; init; }

  /// <summary>
  ///   Gets the validation error, if any.
  /// </summary>
  public EmailValidationError? Error { get; init; }

  #endregion


  #region Methods - Public

  /// <summary>
  ///   Creates a successful validation result.
  /// </summary>
  public static EmailValidationResult Success() => new() { IsValid = true };

  /// <summary>
  ///   Creates a failed validation result with the specified error.
  /// </summary>
  public static EmailValidationResult Failure(EmailValidationError error) =>
    new() { IsValid = false, Error = error };

  #endregion
}


/// <summary>
///   Email validation error codes.
/// </summary>
public enum EmailValidationError
{
  /// <summary>Email format is invalid (RFC 5322).</summary>
  InvalidFormat,

  /// <summary>Email uses a relay/forwarding service (Apple, Firefox, etc.).</summary>
  RelayService,

  /// <summary>Email uses a disposable/temporary domain.</summary>
  Disposable,

  /// <summary>Email domain has no MX records.</summary>
  InvalidDomain
}
