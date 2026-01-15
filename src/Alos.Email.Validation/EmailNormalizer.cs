namespace Alos.Email.Validation;

/// <summary>
///   Normalizes email addresses using provider-specific rules to prevent alias-based
///   duplicate account creation. This normalization is applied BEFORE the HMAC hash
///   to ensure equivalent addresses produce the same NormalizedEmail hash.
/// </summary>
/// <remarks>
///   <para><b>Gmail:</b> Removes dots and +suffix from local part</para>
///   <para><b>ProtonMail:</b> Removes dots, hyphens, underscores, and +suffix from local part</para>
///   <para><b>Fastmail:</b> Removes +suffix; also normalizes subdomain addressing (alias@user.fastmail.com → user@fastmail.com)</para>
///   <para><b>Yahoo:</b> Removes -suffix (hyphen-based aliases, does NOT support plus addressing)</para>
///   <para><b>Microsoft/iCloud:</b> Removes +suffix only</para>
///   <para><b>Tuta:</b> No normalization (does not support any addressing scheme)</para>
///   <para><b>Unknown providers:</b> Lowercase only by default; use <c>stripPlusForUnknownProviders</c> parameter to strip +suffix</para>
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
  ///   ProtonMail domains where dots, hyphens, underscores, and plus-addressing are ignored.
  ///   ProtonMail implements this as a security measure to prevent impersonation attacks
  ///   (e.g., blocking "journalist.name@protonmail.com" if "journalistname@protonmail.com" exists).
  /// </summary>
  private static readonly HashSet<string> ProtonMailDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    "protonmail.com",
    "protonmail.ch",
    "proton.me",
    "pm.me"
  };

  /// <summary>
  ///   Yahoo domains where hyphen-based addressing is used (remove -suffix).
  ///   Dots are significant in Yahoo (unlike Gmail).
  /// </summary>
  private static readonly HashSet<string> YahooDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    "yahoo.com", "yahoo.co.uk", "yahoo.fr", "yahoo.de", "yahoo.es",
    "yahoo.it", "yahoo.ca", "yahoo.com.au", "yahoo.com.br", "yahoo.co.jp",
    "yahoo.co.in", "yahoo.co.nz", "yahoo.com.mx", "yahoo.com.ar",
    "ymail.com", "rocketmail.com"
  };

  /// <summary>
  ///   Fastmail base domains for subdomain addressing detection.
  ///   Subdomain addressing: alias@user.fastmail.com → user@fastmail.com
  /// </summary>
  private static readonly string[] FastmailBaseDomains =
  [
    ".fastmail.com",
    ".fastmail.fm"
  ];

  /// <summary>
  ///   Fastmail direct domains (for plus-addressing without subdomain).
  /// </summary>
  private static readonly HashSet<string> FastmailDirectDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    "fastmail.com",
    "fastmail.fm"
  };

  /// <summary>
  ///   Providers that support plus-addressing (remove +suffix only, dots are significant).
  /// </summary>
  private static readonly HashSet<string> PlusAddressingDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    // Microsoft
    "outlook.com", "hotmail.com", "live.com", "msn.com",

    // iCloud
    "icloud.com", "me.com", "mac.com",

    // Yandex (Russian provider, international domains)
    "yandex.com", "yandex.ru", "yandex.ua", "yandex.by", "yandex.kz",
    "yandex.fr", "yandex.de", "yandex.net", "ya.ru",

    // GMX (United Internet AG)
    "gmx.com", "gmx.de", "gmx.net", "gmx.at", "gmx.ch", "gmx.fr", "gmx.es", "gmx.co.uk",

    // mail.com (United Internet AG - common domains)
    "mail.com", "email.com", "usa.com", "post.com", "europe.com", "asia.com",
    "iname.com", "writeme.com", "dr.com", "myself.com", "consultant.com",
    "accountant.com", "engineer.com", "lawyer.com", "graphic-designer.com",

    // Runbox (Norwegian privacy-focused)
    "runbox.com", "runbox.no",

    // Mailfence (Belgian privacy-focused)
    "mailfence.com",

    // Rambler (Russian provider)
    "rambler.ru", "lenta.ru", "autorambler.ru", "myrambler.ru", "ro.ru",

    // Rackspace (business email hosting)
    "rackspace.com", "emailsrvr.com"
  };

  /// <summary>
  ///   Providers that do NOT support plus-addressing at all.
  ///   For these providers, plus signs are either invalid or treated as literal characters,
  ///   so we should NOT strip them (doing so would be incorrect normalization).
  /// </summary>
  /// <remarks>
  ///   Yahoo is handled separately with its own hyphen-based alias system.
  ///   Tuta/Tutanota explicitly does not support any form of address aliasing.
  ///   AOL has unclear/limited plus addressing support.
  ///   Chinese providers (QQ, NetEase, Sina, Sohu, Aliyun) use different manual alias systems
  ///   and do not support RFC 5233 subaddressing.
  /// </remarks>
  private static readonly HashSet<string> NoPlusAddressingDomains = new(StringComparer.OrdinalIgnoreCase)
  {
    // Tuta (formerly Tutanota) - does not support plus addressing or any alias scheme
    "tuta.com",
    "tuta.io",
    "tutanota.com",
    "tutanota.de",
    "tutamail.com",
    "keemail.me",

    // AOL - unclear/limited plus addressing support
    "aol.com",
    "aim.com",

    // QQ Mail (Tencent) - uses manual alias system, no plus addressing
    // Largest Chinese email provider (~600M+ users)
    "qq.com",
    "foxmail.com",
    "vip.qq.com",

    // NetEase - uses manual alias system, no plus addressing
    // Second largest Chinese provider (~940M+ users)
    "163.com",
    "126.com",
    "yeah.net",

    // Sina Mail - no plus addressing support documented
    "sina.com",
    "sina.cn",

    // Sohu Mail - no plus addressing support documented
    "sohu.com",

    // Aliyun Mail (Alibaba Cloud) - uses manual alias system (up to 5)
    "aliyun.com"
  };

  #endregion


  #region Methods - Public

  /// <summary>
  ///   Normalizes an email address to its canonical form for uniqueness checking.
  /// </summary>
  /// <param name="email">The email address to normalize.</param>
  /// <param name="stripPlusForUnknownProviders">
  ///   When <c>true</c>, strips +suffix for unknown providers (more aggressive anti-abuse).
  ///   When <c>false</c> (default), preserves +suffix for unknown providers (conservative).
  ///   This only affects providers not in the known whitelist (Gmail, ProtonMail, Yahoo,
  ///   Fastmail, Microsoft, iCloud) or blacklist (Tuta).
  /// </param>
  /// <returns>
  ///   The normalized email address. Returns the original address (lowercased)
  ///   if no provider-specific rules apply and <paramref name="stripPlusForUnknownProviders"/> is false.
  /// </returns>
  public static string Normalize(string email, bool stripPlusForUnknownProviders = false)
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

    // ProtonMail: remove dots, hyphens, underscores, AND plus-suffix
    // ProtonMail ignores all these characters as a security measure against impersonation
    if (ProtonMailDomains.Contains(domain))
    {
      localPart = StripPlusSuffix(localPart);
      localPart = StripIgnoredCharacters(localPart, ['.', '-', '_']);

      return $"{localPart.ToLowerInvariant()}@{domain}";
    }

    // Yahoo: remove hyphen-suffix (Yahoo uses hyphen-based aliases, not plus)
    // Dots are significant in Yahoo (unlike Gmail)
    if (YahooDomains.Contains(domain))
    {
      localPart = StripHyphenSuffix(localPart);

      return $"{localPart.ToLowerInvariant()}@{domain}";
    }

    // Fastmail: handle subdomain addressing (alias@user.fastmail.com → user@fastmail.com)
    // and plus-addressing (user+tag@fastmail.com → user@fastmail.com)
    var fastmailResult = TryNormalizeFastmail(localPart, domain);

    if (fastmailResult != null)
      return fastmailResult;

    // Known plus-addressing providers (Microsoft, iCloud): remove plus-suffix only
    if (PlusAddressingDomains.Contains(domain))
    {
      localPart = StripPlusSuffix(localPart);

      return $"{localPart.ToLowerInvariant()}@{domain}";
    }

    // Providers that do NOT support plus-addressing (e.g., Tuta)
    // For these, plus signs are either invalid or literal, so don't strip them.
    if (NoPlusAddressingDomains.Contains(domain))
      return email.ToLowerInvariant();

    // Unknown providers: behavior depends on stripPlusForUnknownProviders parameter.
    // When true: strip plus-suffix (aggressive anti-abuse, assumes plus addressing is supported)
    // When false (default): preserve plus-suffix (conservative, avoids false positives)
    if (stripPlusForUnknownProviders)
    {
      localPart = StripPlusSuffix(localPart);

      return $"{localPart.ToLowerInvariant()}@{domain}";
    }

    // Default: no normalization for unknown providers (lowercase only)
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


  /// <summary>
  ///   Strips the hyphen suffix from a local part (e.g., "user-keyword" -> "user").
  ///   Used for Yahoo's hyphen-based alias system.
  /// </summary>
  private static string StripHyphenSuffix(string localPart)
  {
    var hyphenIndex = localPart.IndexOf('-');

    // hyphenIndex > 0 ensures we don't strip if username starts with -
    return hyphenIndex > 0 ? localPart[..hyphenIndex] : localPart;
  }


  /// <summary>
  ///   Attempts to normalize a Fastmail address, handling both subdomain addressing
  ///   and plus addressing.
  /// </summary>
  /// <param name="localPart">The local part of the email.</param>
  /// <param name="domain">The domain (already lowercased).</param>
  /// <returns>The normalized email, or null if not a Fastmail address.</returns>
  /// <remarks>
  ///   Fastmail supports two addressing methods:
  ///   <list type="bullet">
  ///     <item>Plus addressing: user+tag@fastmail.com → user@fastmail.com</item>
  ///     <item>Subdomain addressing: alias@user.fastmail.com → user@fastmail.com</item>
  ///   </list>
  /// </remarks>
  private static string? TryNormalizeFastmail(string localPart, string domain)
  {
    // Check for subdomain addressing (alias@user.fastmail.com)
    foreach (var baseDomain in FastmailBaseDomains)
    {
      if (domain.EndsWith(baseDomain, StringComparison.OrdinalIgnoreCase))
      {
        // Extract the subdomain as the new local part
        // e.g., "user.fastmail.com" → "user"
        var subdomain = domain[..^baseDomain.Length];

        // Validate subdomain is not empty and doesn't contain dots
        // (only one level of subdomain is supported)
        if (!string.IsNullOrEmpty(subdomain) && !subdomain.Contains('.'))
        {
          // The subdomain becomes the local part, base domain becomes the domain
          // Plus addressing on the local part is discarded (it's just the alias)
          var normalizedDomain = baseDomain[1..]; // Remove leading dot

          return $"{subdomain.ToLowerInvariant()}@{normalizedDomain}";
        }
      }
    }

    // Check for direct Fastmail domain with plus addressing
    if (FastmailDirectDomains.Contains(domain))
    {
      localPart = StripPlusSuffix(localPart);

      return $"{localPart.ToLowerInvariant()}@{domain}";
    }

    return null;
  }


  /// <summary>
  ///   Strips specified characters from a string.
  /// </summary>
  /// <param name="input">The input string.</param>
  /// <param name="charsToRemove">Characters to remove from the string.</param>
  /// <returns>The string with specified characters removed.</returns>
  private static string StripIgnoredCharacters(string input, char[] charsToRemove)
  {
    // Use Span-based approach for efficiency
    Span<char> buffer = stackalloc char[input.Length];
    var writeIndex = 0;

    foreach (var c in input)
    {
      if (Array.IndexOf(charsToRemove, c) < 0)
        buffer[writeIndex++] = c;
    }

    return new string(buffer[..writeIndex]);
  }

  #endregion
}
