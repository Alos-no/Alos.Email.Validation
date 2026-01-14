namespace Alos.Email.Validation;

/// <summary>
///   Checks if an email domain belongs to a known relay/forwarding service that enables
///   unlimited alias creation. These services are blocked to prevent multi-accounting.
/// </summary>
/// <remarks>
///   <para>
///     Blocked services include: Apple Hide My Email, Firefox Relay, DuckDuckGo Email Protection,
///     SimpleLogin, Proton Pass, addy.io, Fastmail Masked Email, Cloaked, and Burner Mail.
///   </para>
///   <para>
///     Some services use username-based subdomains (e.g., <c>alias@username.anonaddy.com</c>).
///     These are handled via wildcard matching.
///   </para>
/// </remarks>
public static class RelayServiceBlocklist
{
  #region Constants & Statics

  /// <summary>
  ///   Exact domain matches (21 domains).
  /// </summary>
  private static readonly HashSet<string> BlockedDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    // Apple
    "privaterelay.appleid.com",
    // Mozilla
    "mozmail.com",
    // DuckDuckGo
    "duck.com",
    // SimpleLogin (Proton)
    "simplelogin.com", "simplelogin.co", "simplelogin.io", "simplelogin.fr",
    "aleeas.com", "slmails.com", "silomails.com", "slmail.me",
    // Proton Pass
    "passmail.net",
    // addy.io (AnonAddy)
    "addy.io", "anonaddy.com", "anonaddy.me",
    // GitHub
    "users.noreply.github.com",
    // Fastmail
    "fastmail.com",
    // Cloaked
    "cloaked.id", "myclkd.email", "clkdmail.com",
    // Burner Mail
    "nicoric.com"
  };

  /// <summary>
  ///   Parent domains that allow username-based subdomains (e.g., *.anonaddy.com).
  /// </summary>
  private static readonly string[] WildcardParentDomains =
  [
    "anonaddy.com", // addy.io: alias@username.anonaddy.com
    "aleeas.com"    // SimpleLogin: alias@username.aleeas.com
  ];

  #endregion


  #region Methods - Public

  /// <summary>
  ///   Checks if the specified domain is a blocked relay service.
  /// </summary>
  /// <param name="domain">The email domain to check (e.g., "duck.com").</param>
  /// <returns>True if the domain is a known relay service and should be blocked.</returns>
  public static bool IsRelayService(string? domain)
  {
    if (string.IsNullOrWhiteSpace(domain))
      return false;

    // Check exact match first
    if (BlockedDomains.Contains(domain))
      return true;

    // Check wildcard patterns (e.g., username.anonaddy.com)
    foreach (var parentDomain in WildcardParentDomains)
    {
      if (domain.EndsWith($".{parentDomain}", StringComparison.OrdinalIgnoreCase))
        return true;
    }

    return false;
  }

  #endregion
}
