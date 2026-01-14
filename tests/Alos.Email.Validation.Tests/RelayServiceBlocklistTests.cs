namespace Alos.Email.Validation.Tests;

/// <summary>
///   Tests for <see cref="RelayServiceBlocklist"/>.
/// </summary>
public class RelayServiceBlocklistTests
{
  #region Exact Domain Tests

  [Theory]
  [InlineData("privaterelay.appleid.com")]    // Apple Hide My Email
  [InlineData("mozmail.com")]                  // Firefox Relay
  [InlineData("duck.com")]                     // DuckDuckGo
  [InlineData("simplelogin.com")]              // SimpleLogin
  [InlineData("simplelogin.co")]
  [InlineData("simplelogin.io")]
  [InlineData("simplelogin.fr")]
  [InlineData("aleeas.com")]                   // SimpleLogin alias
  [InlineData("slmails.com")]
  [InlineData("passmail.net")]                 // Proton Pass
  [InlineData("addy.io")]                      // addy.io
  [InlineData("anonaddy.com")]
  [InlineData("users.noreply.github.com")]     // GitHub
  [InlineData("fastmail.com")]                 // Fastmail masked email
  [InlineData("cloaked.id")]                   // Cloaked
  [InlineData("nicoric.com")]                  // Burner Mail
  public void IsRelayService_BlockedDomain_ReturnsTrue(string domain)
  {
    var result = RelayServiceBlocklist.IsRelayService(domain);

    result.Should().BeTrue($"'{domain}' should be blocked as a relay service");
  }


  [Theory]
  [InlineData("PRIVATERELAY.APPLEID.COM")]
  [InlineData("Duck.Com")]
  [InlineData("MOZMAIL.COM")]
  public void IsRelayService_CaseInsensitive(string domain)
  {
    var result = RelayServiceBlocklist.IsRelayService(domain);

    result.Should().BeTrue("comparison should be case-insensitive");
  }

  #endregion


  #region Wildcard Domain Tests

  [Theory]
  [InlineData("username.anonaddy.com")]        // addy.io user subdomains
  [InlineData("johndoe.anonaddy.com")]
  [InlineData("myalias.anonaddy.com")]
  [InlineData("username.aleeas.com")]          // SimpleLogin user subdomains
  public void IsRelayService_WildcardSubdomain_ReturnsTrue(string domain)
  {
    var result = RelayServiceBlocklist.IsRelayService(domain);

    result.Should().BeTrue($"'{domain}' should be blocked via wildcard matching");
  }


  [Theory]
  [InlineData("User.AnonAddy.Com")]
  [InlineData("USERNAME.ALEEAS.COM")]
  public void IsRelayService_WildcardCaseInsensitive(string domain)
  {
    var result = RelayServiceBlocklist.IsRelayService(domain);

    result.Should().BeTrue("wildcard comparison should be case-insensitive");
  }

  #endregion


  #region Non-Blocked Domains Tests

  [Theory]
  [InlineData("gmail.com")]
  [InlineData("outlook.com")]
  [InlineData("yahoo.com")]
  [InlineData("protonmail.com")]    // ProtonMail itself is fine, only Proton Pass (passmail.net) is blocked
  [InlineData("icloud.com")]
  [InlineData("company.com")]
  [InlineData("example.org")]
  public void IsRelayService_LegitimateProvider_ReturnsFalse(string domain)
  {
    var result = RelayServiceBlocklist.IsRelayService(domain);

    result.Should().BeFalse($"'{domain}' should not be blocked");
  }


  [Theory]
  [InlineData("notanonaddy.com")]      // Similar but different domain
  [InlineData("anonaddy.org")]         // Wrong TLD
  [InlineData("myanonaddy.com")]       // Prefix, not subdomain
  public void IsRelayService_SimilarButDifferent_ReturnsFalse(string domain)
  {
    var result = RelayServiceBlocklist.IsRelayService(domain);

    result.Should().BeFalse($"'{domain}' should not match wildcard patterns");
  }

  #endregion


  #region Edge Cases

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void IsRelayService_NullOrEmpty_ReturnsFalse(string? domain)
  {
    var result = RelayServiceBlocklist.IsRelayService(domain);

    result.Should().BeFalse();
  }

  #endregion


  #region Unicode/IDN Subdomain Tests

  /// <summary>
  ///   Tests that Punycode-encoded IDN subdomains are correctly matched by wildcard patterns.
  ///   Email addresses use Punycode for the domain part (ACE encoding).
  /// </summary>
  [Theory]
  [InlineData("xn--80ak6aa92e.anonaddy.com")]   // Cyrillic subdomain in Punycode
  [InlineData("xn--nxasmq5b.anonaddy.com")]     // Greek subdomain in Punycode
  [InlineData("xn--80ak6aa92e.aleeas.com")]     // Same for SimpleLogin
  public void IsRelayService_PunycodeSubdomain_ReturnsTrue(string domain)
  {
    // Act
    var result = RelayServiceBlocklist.IsRelayService(domain);

    // Assert: Punycode subdomains should still match wildcard patterns.
    result.Should().BeTrue($"'{domain}' should be blocked via wildcard matching");
  }


  /// <summary>
  ///   Tests that direct Unicode subdomains are handled.
  ///   Note: In practice, email addresses should use Punycode for domains,
  ///   but we test this for robustness.
  /// </summary>
  [Theory]
  [InlineData("пользователь.anonaddy.com")]    // Russian Cyrillic username
  [InlineData("χρήστης.anonaddy.com")]          // Greek username
  [InlineData("用户名.anonaddy.com")]            // Chinese username
  public void IsRelayService_UnicodeSubdomain_ReturnsTrue(string domain)
  {
    // Act
    var result = RelayServiceBlocklist.IsRelayService(domain);

    // Assert: Direct Unicode subdomains should still match wildcard patterns.
    // The EndsWith check with OrdinalIgnoreCase should handle these correctly.
    result.Should().BeTrue($"'{domain}' should be blocked via wildcard matching");
  }


  /// <summary>
  ///   Tests that domains with special characters in subdomains are handled.
  /// </summary>
  [Theory]
  [InlineData("user-name.anonaddy.com")]       // Hyphen in subdomain
  [InlineData("user_name.anonaddy.com")]       // Underscore (invalid DNS but could be passed)
  [InlineData("user123.anonaddy.com")]         // Numbers in subdomain
  [InlineData("123.anonaddy.com")]             // Numeric-only subdomain
  public void IsRelayService_SpecialCharacterSubdomain_ReturnsTrue(string domain)
  {
    // Act
    var result = RelayServiceBlocklist.IsRelayService(domain);

    // Assert
    result.Should().BeTrue($"'{domain}' should be blocked via wildcard matching");
  }


  /// <summary>
  ///   Tests that deeply nested subdomains are handled.
  /// </summary>
  [Theory]
  [InlineData("sub.user.anonaddy.com")]        // Two-level subdomain
  [InlineData("deep.sub.user.anonaddy.com")]   // Three-level subdomain
  public void IsRelayService_NestedSubdomain_ReturnsTrue(string domain)
  {
    // Act
    var result = RelayServiceBlocklist.IsRelayService(domain);

    // Assert: Nested subdomains ending in blocked parent should still match.
    result.Should().BeTrue($"'{domain}' should be blocked via wildcard matching");
  }


  /// <summary>
  ///   Tests that similar-looking Unicode domains that are NOT subdomains don't match.
  ///   This tests potential homograph attacks or look-alike domains.
  /// </summary>
  [Theory]
  [InlineData("аnonaddy.com")]                 // Cyrillic 'а' instead of Latin 'a' at start
  [InlineData("anonaddy.соm")]                 // Cyrillic 'с' and 'о' in TLD
  [InlineData("user.аnonaddy.com")]            // Subdomain of homograph parent
  public void IsRelayService_HomographDomain_ReturnsFalse(string domain)
  {
    // Act
    var result = RelayServiceBlocklist.IsRelayService(domain);

    // Assert: Homograph domains should NOT match (they're different Unicode codepoints).
    // These are look-alike attacks that should be caught by other validation layers.
    result.Should().BeFalse($"'{domain}' uses Unicode look-alikes and should not match");
  }

  #endregion
}
