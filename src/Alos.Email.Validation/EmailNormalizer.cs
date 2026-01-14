namespace Alos.Email.Validation;

/// <summary>
///   Normalizes email addresses using provider-specific rules to prevent alias-based
///   duplicate account creation. This normalization is applied BEFORE the HMAC hash
///   to ensure equivalent addresses produce the same NormalizedEmail hash.
/// </summary>
/// <remarks>
///   <para><b>Gmail:</b> Removes dots and +suffix from local part</para>
///   <para><b>Microsoft/Fastmail/ProtonMail/iCloud:</b> Removes +suffix only</para>
///   <para><b>Yahoo:</b> No normalization (base names are pre-created, not spontaneous)</para>
///   <para><b>Other providers:</b> No normalization (unknown alias behavior)</para>
/// </remarks>
public static class EmailNormalizer
{
  #region Constants & Statics

  /// <summary>
  ///   Gmail domains where dots and plus-addressing are ignored.
  /// </summary>
  private static readonly HashSet<string> GmailDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    "gmail.com",
    "googlemail.com"
  };

  /// <summary>
  ///   Providers that support plus-addressing (remove +suffix only, dots are significant).
  /// </summary>
  private static readonly HashSet<string> PlusAddressingDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    // Microsoft
    "outlook.com", "hotmail.com", "live.com", "msn.com",
    // Fastmail (fastmail.com is blocked as relay)
    "fastmail.fm",
    // ProtonMail
    "protonmail.com", "protonmail.ch", "proton.me", "pm.me",
    // iCloud
    "icloud.com", "me.com", "mac.com"
  };

  #endregion


  #region Methods - Public

  /// <summary>
  ///   Normalizes an email address to its canonical form for uniqueness checking.
  /// </summary>
  /// <param name="email">The email address to normalize.</param>
  /// <returns>
  ///   The normalized email address. Returns the original address (lowercased)
  ///   if no provider-specific rules apply.
  /// </returns>
  public static string Normalize(string email)
  {
    if (string.IsNullOrWhiteSpace(email))
      return email;

    var atIndex = email.LastIndexOf('@');

    if (atIndex <= 0 || atIndex >= email.Length - 1)
      return email.ToLowerInvariant();

    var localPart = email[..atIndex];
    var domain = email[(atIndex + 1)..].ToLowerInvariant();

    // Gmail: remove dots AND plus-suffix
    if (GmailDomains.Contains(domain))
    {
      localPart = StripPlusSuffix(localPart);
      localPart = localPart.Replace(".", "");

      return $"{localPart.ToLowerInvariant()}@{domain}";
    }

    // Other providers: remove plus-suffix only
    if (PlusAddressingDomains.Contains(domain))
    {
      localPart = StripPlusSuffix(localPart);

      return $"{localPart.ToLowerInvariant()}@{domain}";
    }

    // No normalization for Yahoo, other providers
    return email.ToLowerInvariant();
  }


  /// <summary>
  ///   Extracts the domain portion of an email address.
  /// </summary>
  /// <param name="email">The email address.</param>
  /// <returns>The domain in lowercase, or null if the email is invalid.</returns>
  public static string? ExtractDomain(string? email)
  {
    if (string.IsNullOrWhiteSpace(email))
      return null;

    var atIndex = email.LastIndexOf('@');

    if (atIndex <= 0 || atIndex >= email.Length - 1)
      return null;

    var domain = email[(atIndex + 1)..].ToLowerInvariant();

    // Domain must not be empty or whitespace-only.
    if (string.IsNullOrWhiteSpace(domain))
      return null;

    return domain;
  }

  #endregion


  #region Methods - Private

  /// <summary>
  ///   Strips the plus suffix from a local part (e.g., "user+tag" -> "user").
  /// </summary>
  private static string StripPlusSuffix(string localPart)
  {
    var plusIndex = localPart.IndexOf('+');

    // plusIndex > 0 ensures we don't strip if username starts with +
    return plusIndex > 0 ? localPart[..plusIndex] : localPart;
  }

  #endregion
}
