namespace Alos.Email.Validation.Tests;

/// <summary>
///   Tests for <see cref="EmailNormalizer"/>.
/// </summary>
public class EmailNormalizerTests
{
  #region Gmail Tests

  [Theory]
  [InlineData("john.doe@gmail.com", "johndoe@gmail.com")]
  [InlineData("j.o.h.n.d.o.e@gmail.com", "johndoe@gmail.com")]
  [InlineData("johndoe@gmail.com", "johndoe@gmail.com")]
  [InlineData("JOHNDOE@GMAIL.COM", "johndoe@gmail.com")]
  public void Normalize_Gmail_RemovesDots(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john+spam@gmail.com", "john@gmail.com")]
  [InlineData("john.doe+newsletters@gmail.com", "johndoe@gmail.com")]
  [InlineData("john+@gmail.com", "john@gmail.com")]
  public void Normalize_Gmail_RemovesPlusSuffix(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john.doe@googlemail.com", "johndoe@googlemail.com")]
  [InlineData("john+spam@googlemail.com", "john@googlemail.com")]
  public void Normalize_GoogleMail_SameAsGmail(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Microsoft/Outlook Tests

  [Theory]
  [InlineData("john+spam@outlook.com", "john@outlook.com")]
  [InlineData("john+spam@hotmail.com", "john@hotmail.com")]
  [InlineData("john+spam@live.com", "john@live.com")]
  [InlineData("john+spam@msn.com", "john@msn.com")]
  public void Normalize_Microsoft_RemovesPlusSuffixOnly(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john.doe@outlook.com", "john.doe@outlook.com")]
  [InlineData("j.o.h.n@hotmail.com", "j.o.h.n@hotmail.com")]
  public void Normalize_Microsoft_PreservesDots(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region ProtonMail Tests

  [Theory]
  [InlineData("john+spam@protonmail.com", "john@protonmail.com")]
  [InlineData("john+spam@proton.me", "john@proton.me")]
  [InlineData("john+spam@pm.me", "john@pm.me")]
  public void Normalize_ProtonMail_RemovesPlusSuffix(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region iCloud Tests

  [Theory]
  [InlineData("john+spam@icloud.com", "john@icloud.com")]
  [InlineData("john+spam@me.com", "john@me.com")]
  [InlineData("john+spam@mac.com", "john@mac.com")]
  public void Normalize_iCloud_RemovesPlusSuffix(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Yahoo Tests

  [Theory]
  [InlineData("john@yahoo.com", "john@yahoo.com")]
  [InlineData("john.doe@yahoo.com", "john.doe@yahoo.com")]
  [InlineData("john-alias@yahoo.com", "john-alias@yahoo.com")]
  public void Normalize_Yahoo_NoNormalization(string input, string expected)
  {
    // Yahoo base names are pre-created, not spontaneous aliases
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Other Providers Tests

  [Theory]
  [InlineData("john@company.com", "john@company.com")]
  [InlineData("john.doe@example.org", "john.doe@example.org")]
  [InlineData("john+tag@custom-domain.io", "john+tag@custom-domain.io")]
  public void Normalize_UnknownProvider_LowercaseOnly(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Fact]
  public void Normalize_UnknownProvider_PreservesCase()
  {
    var result = EmailNormalizer.Normalize("JOHN@EXAMPLE.COM");

    result.Should().Be("john@example.com");
  }

  #endregion


  #region Edge Cases

  [Theory]
  [InlineData(null, null)]
  [InlineData("", "")]
  [InlineData("   ", "   ")]
  public void Normalize_NullOrWhitespace_ReturnsInput(string? input, string? expected)
  {
    var result = EmailNormalizer.Normalize(input!);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("notanemail", "notanemail")]
  [InlineData("@nodomain", "@nodomain")]
  [InlineData("noat.com", "noat.com")]
  public void Normalize_InvalidFormat_ReturnsLowercased(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Fact]
  public void Normalize_PlusAtStart_PreservesPlus()
  {
    // Edge case: username starts with +
    var result = EmailNormalizer.Normalize("+john@gmail.com");

    // Should not strip leading + as it's part of the username
    result.Should().Be("+john@gmail.com");
  }

  #endregion


  #region Long Email Address Tests

  [Fact]
  public void Normalize_VeryLongEmail_HandlesCorrectly()
  {
    // RFC 5321 specifies max local part is 64 chars, domain is 255 chars, total is 320 chars.
    // Test with a long but valid-length email.
    var longLocalPart = new string('a', 64);
    var longDomain = new string('b', 63) + ".com"; // 67 chars domain.
    var longEmail = $"{longLocalPart}@{longDomain}";

    // Act
    var result = EmailNormalizer.Normalize(longEmail);

    // Assert: Should return lowercased version.
    result.Should().Be(longEmail.ToLowerInvariant());
  }


  [Fact]
  public void Normalize_ExtremelyLongEmail_HandlesWithoutCrashing()
  {
    // Test with an email that exceeds RFC limits - should still handle gracefully.
    var extremeLocalPart = new string('a', 500);
    var extremeDomain = new string('b', 500) + ".com";
    var extremeEmail = $"{extremeLocalPart}@{extremeDomain}";

    // Act & Assert: Should not throw, should return lowercased version.
    var act = () => EmailNormalizer.Normalize(extremeEmail);

    act.Should().NotThrow();
    var result = EmailNormalizer.Normalize(extremeEmail);
    result.Should().Be(extremeEmail.ToLowerInvariant());
  }


  [Fact]
  public void ExtractDomain_VeryLongEmail_ExtractsDomainCorrectly()
  {
    // Arrange
    var longLocalPart = new string('a', 64);
    var expectedDomain = "example.com";
    var longEmail = $"{longLocalPart}@{expectedDomain}";

    // Act
    var result = EmailNormalizer.ExtractDomain(longEmail);

    // Assert
    result.Should().Be(expectedDomain);
  }

  #endregion


  #region Unicode/IDN Domain Tests

  [Theory]
  [InlineData("user@münchen.de", "münchen.de")] // German umlaut.
  [InlineData("user@日本.jp", "日本.jp")] // Japanese.
  [InlineData("user@пример.рф", "пример.рф")] // Russian.
  [InlineData("user@例え.jp", "例え.jp")] // Japanese kanji.
  public void ExtractDomain_UnicodeDomain_ExtractsDomainCorrectly(string email, string expectedDomain)
  {
    // Act
    var result = EmailNormalizer.ExtractDomain(email);

    // Assert: Domain should be extracted and lowercased.
    result.Should().Be(expectedDomain.ToLowerInvariant());
  }


  [Theory]
  [InlineData("user@münchen.de", "user@münchen.de")] // German umlaut.
  [InlineData("user@日本.jp", "user@日本.jp")] // Japanese.
  public void Normalize_UnicodeDomain_LowercasesCorrectly(string email, string expected)
  {
    // Act
    var result = EmailNormalizer.Normalize(email);

    // Assert: Should lowercase (for domains that support it).
    result.Should().Be(expected.ToLowerInvariant());
  }


  [Fact]
  public void Normalize_PunycodeEncodedDomain_HandlesCorrectly()
  {
    // Punycode representation of münchen.de is xn--mnchen-3ya.de
    var punycodeEmail = "user@xn--mnchen-3ya.de";

    // Act
    var result = EmailNormalizer.Normalize(punycodeEmail);

    // Assert: Should lowercase the punycode domain.
    result.Should().Be("user@xn--mnchen-3ya.de");
  }


  [Fact]
  public void ExtractDomain_MixedUnicodeAndAscii_ExtractsCorrectly()
  {
    // Domain with mixed Unicode and ASCII characters.
    var email = "user@tëst-日本.example.com";

    // Act
    var result = EmailNormalizer.ExtractDomain(email);

    // Assert
    result.Should().Be("tëst-日本.example.com");
  }

  #endregion


  #region ExtractDomain Tests

  [Theory]
  [InlineData("john@gmail.com", "gmail.com")]
  [InlineData("john@GMAIL.COM", "gmail.com")]
  [InlineData("john.doe+tag@example.org", "example.org")]
  public void ExtractDomain_ValidEmail_ReturnsDomain(string input, string expected)
  {
    var result = EmailNormalizer.ExtractDomain(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("notanemail")]
  [InlineData("@nodomain")]
  public void ExtractDomain_Invalid_ReturnsNull(string? input)
  {
    var result = EmailNormalizer.ExtractDomain(input);

    result.Should().BeNull();
  }

  #endregion


  #region Comprehensive Email Format Edge Cases

  /// <summary>
  ///   Tests handling of multiple @ signs in email addresses.
  ///   Per RFC 5322, only the last @ should be the delimiter.
  /// </summary>
  [Theory]
  [InlineData("user@domain@example.com", "example.com")]       // Multiple @, use last one
  [InlineData("user@@example.com", "example.com")]             // Double @
  [InlineData("user@@@example.com", "example.com")]            // Triple @
  [InlineData("a@b@c@d@example.com", "example.com")]           // Many @
  public void ExtractDomain_MultipleAtSigns_UsesLastAt(string email, string expectedDomain)
  {
    // The implementation uses LastIndexOf('@'), so it should extract the last domain.
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain);
  }


  /// <summary>
  ///   Tests handling of emails with trailing @.
  /// </summary>
  [Theory]
  [InlineData("user@")]                                        // Nothing after @
  [InlineData("user@   ")]                                     // Only whitespace after @
  public void ExtractDomain_TrailingAt_ReturnsNull(string email)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().BeNull("trailing @ with no domain should be invalid");
  }


  /// <summary>
  ///   Tests handling of emails with only @ symbol.
  /// </summary>
  [Theory]
  [InlineData("@")]
  [InlineData("@@")]
  [InlineData("@@@")]
  public void ExtractDomain_OnlyAtSymbols_ReturnsNull(string email)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().BeNull("email with only @ symbols should be invalid");
  }


  /// <summary>
  ///   Tests handling of domains with consecutive dots.
  /// </summary>
  [Theory]
  [InlineData("user@domain..com", "domain..com")]              // Double dot in domain
  [InlineData("user@..example.com", "..example.com")]          // Leading double dot
  [InlineData("user@example..com.", "example..com.")]          // Trailing dot
  public void ExtractDomain_DotsInDomain_ExtractsAsIs(string email, string expectedDomain)
  {
    // ExtractDomain doesn't validate domain format, just extracts it.
    // DNS validation will catch invalid domains later.
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain);
  }


  /// <summary>
  ///   Tests handling of special characters in local part (RFC 5322 allows many).
  /// </summary>
  [Theory]
  [InlineData("user!def@example.com", "example.com")]          // Exclamation mark
  [InlineData("user#hash@example.com", "example.com")]         // Hash
  [InlineData("user$dollar@example.com", "example.com")]       // Dollar
  [InlineData("user%percent@example.com", "example.com")]      // Percent
  [InlineData("user&and@example.com", "example.com")]          // Ampersand
  [InlineData("user'quote@example.com", "example.com")]        // Single quote
  [InlineData("user*star@example.com", "example.com")]         // Asterisk
  [InlineData("user/slash@example.com", "example.com")]        // Slash
  [InlineData("user=equals@example.com", "example.com")]       // Equals
  [InlineData("user?question@example.com", "example.com")]     // Question mark
  [InlineData("user^caret@example.com", "example.com")]        // Caret
  [InlineData("user`backtick@example.com", "example.com")]     // Backtick
  [InlineData("user{brace}@example.com", "example.com")]       // Braces
  [InlineData("user|pipe@example.com", "example.com")]         // Pipe
  [InlineData("user~tilde@example.com", "example.com")]        // Tilde
  public void ExtractDomain_SpecialCharsInLocalPart_ExtractsDomain(string email, string expectedDomain)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain);
  }


  /// <summary>
  ///   Tests handling of quoted local parts (RFC 5321 allows quoted strings).
  /// </summary>
  [Theory]
  [InlineData("\"john doe\"@example.com", "example.com")]      // Space in quotes
  [InlineData("\"john@doe\"@example.com", "example.com")]      // @ in quotes
  [InlineData("\"john\\\"doe\"@example.com", "example.com")]   // Escaped quote
  [InlineData("\"\"@example.com", "example.com")]              // Empty quotes
  public void ExtractDomain_QuotedLocalPart_ExtractsDomain(string email, string expectedDomain)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain);
  }


  /// <summary>
  ///   Tests handling of IP address domains (RFC 5321).
  /// </summary>
  [Theory]
  [InlineData("user@[192.168.1.1]", "[192.168.1.1]")]          // IPv4 literal
  [InlineData("user@[IPv6:2001:db8::1]", "[ipv6:2001:db8::1]")] // IPv6 literal
  [InlineData("user@192.168.1.1", "192.168.1.1")]              // IPv4 without brackets
  public void ExtractDomain_IpAddressDomain_ExtractsDomain(string email, string expectedDomain)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain.ToLowerInvariant());
  }


  /// <summary>
  ///   Tests handling of whitespace in various positions.
  /// </summary>
  [Theory]
  [InlineData(" user@example.com", "example.com")]             // Leading space
  [InlineData("user@example.com ", "example.com ")]            // Trailing space (becomes part of domain)
  [InlineData("user @example.com", "example.com")]             // Space before @
  [InlineData("user@ example.com", " example.com")]            // Space after @ (becomes part of domain)
  public void ExtractDomain_WhitespacePositions_HandlesAsIs(string email, string expectedDomain)
  {
    // Note: The implementation doesn't trim - it extracts as-is.
    // Callers should trim input if needed.
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain.ToLowerInvariant());
  }


  /// <summary>
  ///   Tests handling of control characters and injection attempts.
  /// </summary>
  [Theory]
  [InlineData("user\t@example.com", "example.com")]            // Tab in local part
  [InlineData("user\n@example.com", "example.com")]            // Newline in local part
  [InlineData("user\r@example.com", "example.com")]            // CR in local part
  [InlineData("user\0@example.com", "example.com")]            // Null char in local part
  public void ExtractDomain_ControlCharsInLocalPart_ExtractsDomain(string email, string expectedDomain)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain);
  }


  /// <summary>
  ///   Tests that HTML/script injection in emails is handled safely.
  /// </summary>
  [Theory]
  [InlineData("<script>alert(1)</script>@example.com", "example.com")]
  [InlineData("user@<script>alert(1)</script>.com", "<script>alert(1)</script>.com")]
  [InlineData("user@example.com<script>", "example.com<script>")]
  public void ExtractDomain_HtmlInjectionAttempt_ExtractsAsIs(string email, string expectedDomain)
  {
    // ExtractDomain extracts as-is - HTML encoding is caller's responsibility.
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain.ToLowerInvariant());
  }


  /// <summary>
  ///   Tests handling of domains with port numbers (invalid but could be passed).
  /// </summary>
  [Theory]
  [InlineData("user@example.com:25", "example.com:25")]
  [InlineData("user@example.com:8080", "example.com:8080")]
  public void ExtractDomain_DomainWithPort_ExtractsWithPort(string email, string expectedDomain)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain);
  }


  /// <summary>
  ///   Tests handling of URLs mistakenly passed as emails.
  /// </summary>
  [Theory]
  [InlineData("http://user@example.com", "example.com")]       // URL with @ (user info)
  [InlineData("https://user:pass@example.com", "example.com")] // URL with credentials
  [InlineData("mailto:user@example.com", "example.com")]       // mailto: scheme
  public void ExtractDomain_UrlLikeInput_ExtractsLastDomain(string input, string expectedDomain)
  {
    // Uses LastIndexOf('@'), so extracts domain after last @.
    var result = EmailNormalizer.ExtractDomain(input);

    result.Should().Be(expectedDomain);
  }


  /// <summary>
  ///   Tests normalization of emails with multiple + signs.
  /// </summary>
  [Theory]
  [InlineData("john+tag1+tag2@gmail.com", "john@gmail.com")]   // Multiple + signs
  [InlineData("john++tag@gmail.com", "john@gmail.com")]        // Double +
  [InlineData("john+++@gmail.com", "john@gmail.com")]          // Triple + at end
  public void Normalize_MultiplePlusSigns_StripsFromFirstPlus(string email, string expected)
  {
    var result = EmailNormalizer.Normalize(email);

    result.Should().Be(expected);
  }


  /// <summary>
  ///   Tests normalization preserves dots for providers that don't ignore them.
  /// </summary>
  [Theory]
  [InlineData("j.o.h.n@outlook.com", "j.o.h.n@outlook.com")]
  [InlineData("j.o.h.n@yahoo.com", "j.o.h.n@yahoo.com")]
  [InlineData("j.o.h.n@company.com", "j.o.h.n@company.com")]
  public void Normalize_DotsInLocalPart_PreservedForNonGmail(string email, string expected)
  {
    var result = EmailNormalizer.Normalize(email);

    result.Should().Be(expected);
  }


  /// <summary>
  ///   Tests handling of case sensitivity in domain vs local part.
  /// </summary>
  [Theory]
  [InlineData("JOHN@GMAIL.COM", "john@gmail.com")]             // All caps
  [InlineData("JoHn@GmAiL.cOm", "john@gmail.com")]             // Mixed case
  [InlineData("john@GMAIL.COM", "john@gmail.com")]             // Only domain caps
  public void Normalize_CaseSensitivity_LowercasesAll(string email, string expected)
  {
    var result = EmailNormalizer.Normalize(email);

    result.Should().Be(expected);
  }


  /// <summary>
  ///   Tests handling of empty local part with valid-looking domain.
  /// </summary>
  [Fact]
  public void ExtractDomain_EmptyLocalPart_ReturnsNull()
  {
    // @ at index 0 means empty local part, which should be invalid.
    var result = EmailNormalizer.ExtractDomain("@example.com");

    result.Should().BeNull("empty local part should be invalid");
  }


  /// <summary>
  ///   Tests handling of very short emails.
  /// </summary>
  [Theory]
  [InlineData("a@b", "b")]                                     // Minimum valid: 1 char each
  [InlineData("a@b.c", "b.c")]                                 // Short with TLD
  public void ExtractDomain_VeryShortEmail_ExtractsDomain(string email, string expectedDomain)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain);
  }


  /// <summary>
  ///   Tests boundary condition: email ending with @.
  /// </summary>
  [Fact]
  public void ExtractDomain_EndsWithAt_ReturnsNull()
  {
    var result = EmailNormalizer.ExtractDomain("user@");

    result.Should().BeNull("email ending with @ has no domain");
  }


  /// <summary>
  ///   Tests boundary condition: email starting with @.
  /// </summary>
  [Fact]
  public void ExtractDomain_StartsWithAt_ReturnsNull()
  {
    // @ at index 0 means atIndex <= 0 check fails.
    var result = EmailNormalizer.ExtractDomain("@example.com");

    result.Should().BeNull("email starting with @ has no local part");
  }


  /// <summary>
  ///   Tests handling of domain-only TLDs (no subdomain).
  /// </summary>
  [Theory]
  [InlineData("user@localhost", "localhost")]
  [InlineData("user@test", "test")]
  [InlineData("admin@internal", "internal")]
  public void ExtractDomain_SingleLabelDomain_ExtractsDomain(string email, string expectedDomain)
  {
    var result = EmailNormalizer.ExtractDomain(email);

    result.Should().Be(expectedDomain);
  }

  #endregion
}
