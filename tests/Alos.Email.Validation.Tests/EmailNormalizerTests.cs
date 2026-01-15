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
  [InlineData("john+spam@protonmail.ch", "john@protonmail.ch")]
  [InlineData("john+spam@proton.me", "john@proton.me")]
  [InlineData("john+spam@pm.me", "john@pm.me")]
  public void Normalize_ProtonMail_RemovesPlusSuffix(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john.doe@protonmail.com", "johndoe@protonmail.com")]
  [InlineData("j.o.h.n@proton.me", "john@proton.me")]
  [InlineData("first.last@pm.me", "firstlast@pm.me")]
  public void Normalize_ProtonMail_RemovesDots(string input, string expected)
  {
    // ProtonMail ignores dots as a security measure against impersonation
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john-doe@protonmail.com", "johndoe@protonmail.com")]
  [InlineData("first-middle-last@proton.me", "firstmiddlelast@proton.me")]
  [InlineData("user-name@pm.me", "username@pm.me")]
  public void Normalize_ProtonMail_RemovesHyphens(string input, string expected)
  {
    // ProtonMail ignores hyphens as a security measure against impersonation
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john_doe@protonmail.com", "johndoe@protonmail.com")]
  [InlineData("first_middle_last@proton.me", "firstmiddlelast@proton.me")]
  [InlineData("user_name@pm.me", "username@pm.me")]
  public void Normalize_ProtonMail_RemovesUnderscores(string input, string expected)
  {
    // ProtonMail ignores underscores as a security measure against impersonation
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("j.o-h_n+spam@protonmail.com", "john@protonmail.com")]
  [InlineData("first.middle-last_name+tag@proton.me", "firstmiddlelastname@proton.me")]
  [InlineData("a.b-c_d+e@pm.me", "abcd@pm.me")]
  public void Normalize_ProtonMail_RemovesAllIgnoredCharacters(string input, string expected)
  {
    // ProtonMail ignores dots, hyphens, underscores, and +suffix
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("JOHN.DOE@PROTONMAIL.COM", "johndoe@protonmail.com")]
  [InlineData("John-Doe@Proton.Me", "johndoe@proton.me")]
  public void Normalize_ProtonMail_LowercasesAndNormalizes(string input, string expected)
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


  #region Yandex Tests

  [Theory]
  [InlineData("john+spam@yandex.com", "john@yandex.com")]
  [InlineData("john+spam@yandex.ru", "john@yandex.ru")]
  [InlineData("john+spam@ya.ru", "john@ya.ru")]
  [InlineData("john+tag@yandex.fr", "john@yandex.fr")]
  public void Normalize_Yandex_RemovesPlusSuffix(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john.doe@yandex.com", "john.doe@yandex.com")]
  public void Normalize_Yandex_PreservesDots(string input, string expected)
  {
    // Dots are significant in Yandex
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region GMX/mail.com Tests

  [Theory]
  [InlineData("john+spam@gmx.com", "john@gmx.com")]
  [InlineData("john+spam@gmx.de", "john@gmx.de")]
  [InlineData("john+spam@gmx.net", "john@gmx.net")]
  [InlineData("john+tag@mail.com", "john@mail.com")]
  [InlineData("john+tag@email.com", "john@email.com")]
  [InlineData("john+tag@usa.com", "john@usa.com")]
  public void Normalize_GmxMailCom_RemovesPlusSuffix(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Other Known Plus-Addressing Providers Tests

  [Theory]
  [InlineData("john+spam@runbox.com", "john@runbox.com")]
  [InlineData("john+spam@mailfence.com", "john@mailfence.com")]
  [InlineData("john+spam@rambler.ru", "john@rambler.ru")]
  [InlineData("john+spam@rackspace.com", "john@rackspace.com")]
  public void Normalize_OtherKnownProviders_RemovesPlusSuffix(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region AOL Tests (No Plus Addressing Support)

  [Theory]
  [InlineData("john@aol.com", "john@aol.com")]
  [InlineData("john@aim.com", "john@aim.com")]
  public void Normalize_AOL_LowercasesOnly(string input, string expected)
  {
    // AOL does not reliably support plus addressing
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john+tag@aol.com", "john+tag@aol.com")]
  [InlineData("john+spam@aim.com", "john+spam@aim.com")]
  public void Normalize_AOL_PreservesPlusSuffix(string input, string expected)
  {
    // AOL doesn't reliably support plus addressing, so +suffix is preserved
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Yahoo Tests

  [Theory]
  [InlineData("john-shopping@yahoo.com", "john@yahoo.com")]
  [InlineData("john-newsletter@yahoo.com", "john@yahoo.com")]
  [InlineData("user-keyword@ymail.com", "user@ymail.com")]
  [InlineData("user-keyword@rocketmail.com", "user@rocketmail.com")]
  public void Normalize_Yahoo_RemovesHyphenSuffix(string input, string expected)
  {
    // Yahoo uses hyphen-based aliases (nickname-keyword@yahoo.com)
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john@yahoo.com", "john@yahoo.com")]
  [InlineData("john.doe@yahoo.com", "john.doe@yahoo.com")]
  public void Normalize_Yahoo_PreservesDots(string input, string expected)
  {
    // Dots are significant in Yahoo (unlike Gmail)
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john+tag@yahoo.com", "john+tag@yahoo.com")]
  [InlineData("user+spam@ymail.com", "user+spam@ymail.com")]
  public void Normalize_Yahoo_PreservesPlusSuffix(string input, string expected)
  {
    // Yahoo doesn't use plus addressing, so +suffix is preserved
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john@yahoo.co.uk", "john@yahoo.co.uk")]
  [InlineData("john-tag@yahoo.fr", "john@yahoo.fr")]
  [InlineData("john-tag@yahoo.de", "john@yahoo.de")]
  [InlineData("john-tag@yahoo.co.jp", "john@yahoo.co.jp")]
  public void Normalize_Yahoo_InternationalDomains(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Fact]
  public void Normalize_Yahoo_HyphenAtStart_Preserved()
  {
    // Edge case: username starts with hyphen (should not strip)
    var result = EmailNormalizer.Normalize("-john@yahoo.com");

    result.Should().Be("-john@yahoo.com");
  }

  #endregion


  #region Fastmail Tests

  [Theory]
  [InlineData("john+tag@fastmail.com", "john@fastmail.com")]
  [InlineData("john+spam@fastmail.fm", "john@fastmail.fm")]
  [InlineData("user+newsletter@fastmail.com", "user@fastmail.com")]
  public void Normalize_Fastmail_RemovesPlusSuffix(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("alias@batman.fastmail.com", "batman@fastmail.com")]
  [InlineData("shopping@john.fastmail.com", "john@fastmail.com")]
  [InlineData("newsletter@user.fastmail.fm", "user@fastmail.fm")]
  [InlineData("anything@bruce.fastmail.com", "bruce@fastmail.com")]
  public void Normalize_Fastmail_SubdomainAddressing(string input, string expected)
  {
    // Fastmail subdomain addressing: alias@user.fastmail.com → user@fastmail.com
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john.doe@fastmail.com", "john.doe@fastmail.com")]
  [InlineData("first.last@fastmail.fm", "first.last@fastmail.fm")]
  public void Normalize_Fastmail_PreservesDots(string input, string expected)
  {
    // Dots are significant in Fastmail
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("JOHN@FASTMAIL.COM", "john@fastmail.com")]
  [InlineData("Alias@User.Fastmail.Com", "user@fastmail.com")]
  public void Normalize_Fastmail_LowercasesCorrectly(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("alias@sub.domain.fastmail.com", "alias@sub.domain.fastmail.com")]
  [InlineData("alias+tag@sub.domain.fastmail.com", "alias+tag@sub.domain.fastmail.com")]
  public void Normalize_Fastmail_MultiLevelSubdomain_FallsToDefault(string input, string expected)
  {
    // Only one level of subdomain is supported for Fastmail subdomain addressing.
    // Multi-level subdomains fall through to default behavior (preserve plus suffix for unknown).
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("alias+tag@sub.domain.fastmail.com", "alias@sub.domain.fastmail.com")]
  public void Normalize_Fastmail_MultiLevelSubdomain_WithAggressiveMode(string input, string expected)
  {
    // With aggressive mode, multi-level subdomains strip plus suffix.
    var result = EmailNormalizer.Normalize(input, stripPlusForUnknownProviders: true);

    result.Should().Be(expected);
  }

  #endregion


  #region Tuta/Tutanota Tests (No Plus Addressing Support)

  [Theory]
  [InlineData("john@tuta.com", "john@tuta.com")]
  [InlineData("john@tuta.io", "john@tuta.io")]
  [InlineData("john@tutanota.com", "john@tutanota.com")]
  [InlineData("john@tutanota.de", "john@tutanota.de")]
  [InlineData("john@tutamail.com", "john@tutamail.com")]
  [InlineData("john@keemail.me", "john@keemail.me")]
  public void Normalize_Tuta_LowercasesOnly(string input, string expected)
  {
    // Tuta does not support plus addressing at all, so we don't strip +suffix
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john+tag@tuta.com", "john+tag@tuta.com")]
  [InlineData("john+spam@tutanota.com", "john+spam@tutanota.com")]
  [InlineData("user+newsletter@tuta.io", "user+newsletter@tuta.io")]
  public void Normalize_Tuta_PreservesPlusSuffix(string input, string expected)
  {
    // Tuta doesn't support plus addressing, so +suffix is preserved
    // (it's either invalid or treated as a literal different address)
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john.doe@tuta.com", "john.doe@tuta.com")]
  [InlineData("first.last@tutanota.com", "first.last@tutanota.com")]
  public void Normalize_Tuta_PreservesDots(string input, string expected)
  {
    // Dots are significant in Tuta
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Chinese Providers Tests (No Plus Addressing Support)

  [Theory]
  [InlineData("john@qq.com", "john@qq.com")]
  [InlineData("john@foxmail.com", "john@foxmail.com")]
  [InlineData("john@vip.qq.com", "john@vip.qq.com")]
  public void Normalize_QQMail_LowercasesOnly(string input, string expected)
  {
    // QQ Mail does not support plus addressing, uses manual alias system
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john+tag@qq.com", "john+tag@qq.com")]
  [InlineData("john+spam@foxmail.com", "john+spam@foxmail.com")]
  [InlineData("user+newsletter@vip.qq.com", "user+newsletter@vip.qq.com")]
  public void Normalize_QQMail_PreservesPlusSuffix(string input, string expected)
  {
    // QQ Mail doesn't support plus addressing, so +suffix is preserved
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john@163.com", "john@163.com")]
  [InlineData("john@126.com", "john@126.com")]
  [InlineData("john@yeah.net", "john@yeah.net")]
  public void Normalize_NetEase_LowercasesOnly(string input, string expected)
  {
    // NetEase does not support plus addressing, uses manual alias system
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john+tag@163.com", "john+tag@163.com")]
  [InlineData("john+spam@126.com", "john+spam@126.com")]
  [InlineData("user+newsletter@yeah.net", "user+newsletter@yeah.net")]
  public void Normalize_NetEase_PreservesPlusSuffix(string input, string expected)
  {
    // NetEase doesn't support plus addressing, so +suffix is preserved
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john@sina.com", "john@sina.com")]
  [InlineData("john@sina.cn", "john@sina.cn")]
  [InlineData("john@sohu.com", "john@sohu.com")]
  [InlineData("john@aliyun.com", "john@aliyun.com")]
  public void Normalize_OtherChineseProviders_LowercasesOnly(string input, string expected)
  {
    // Sina, Sohu, Aliyun do not support plus addressing
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john+tag@sina.com", "john+tag@sina.com")]
  [InlineData("john+spam@sohu.com", "john+spam@sohu.com")]
  [InlineData("user+newsletter@aliyun.com", "user+newsletter@aliyun.com")]
  public void Normalize_OtherChineseProviders_PreservesPlusSuffix(string input, string expected)
  {
    // Chinese providers don't support plus addressing, so +suffix is preserved
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john.doe@qq.com", "john.doe@qq.com")]
  [InlineData("first.last@163.com", "first.last@163.com")]
  [InlineData("user.name@sina.com", "user.name@sina.com")]
  public void Normalize_ChineseProviders_PreservesDots(string input, string expected)
  {
    // Dots are significant in Chinese providers
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("JOHN@QQ.COM", "john@qq.com")]
  [InlineData("USER@163.COM", "user@163.com")]
  [InlineData("Test@FOXMAIL.com", "test@foxmail.com")]
  public void Normalize_ChineseProviders_LowercasesCorrectly(string input, string expected)
  {
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }

  #endregion


  #region Other Providers Tests (Unknown Providers)

  [Theory]
  [InlineData("john@company.com", "john@company.com")]
  [InlineData("john.doe@example.org", "john.doe@example.org")]
  [InlineData("john+tag@company.com", "john+tag@company.com")]
  [InlineData("user+spam@custom-domain.io", "user+spam@custom-domain.io")]
  public void Normalize_UnknownProvider_PreservesAllByDefault(string input, string expected)
  {
    // By default, unknown providers only get lowercased (conservative approach)
    var result = EmailNormalizer.Normalize(input);

    result.Should().Be(expected);
  }


  [Theory]
  [InlineData("john+tag@company.com", "john@company.com")]
  [InlineData("john+spam@example.org", "john@example.org")]
  [InlineData("user+newsletter@custom-domain.io", "user@custom-domain.io")]
  public void Normalize_UnknownProvider_RemovesPlusSuffix_WhenAggressiveModeEnabled(string input, string expected)
  {
    // With stripPlusForUnknownProviders=true, strip +suffix for anti-abuse
    var result = EmailNormalizer.Normalize(input, stripPlusForUnknownProviders: true);

    result.Should().Be(expected);
  }


  [Fact]
  public void Normalize_UnknownProvider_PreservesDotsInBothModes()
  {
    // Dots are preserved for unknown providers regardless of mode
    var defaultResult = EmailNormalizer.Normalize("JOHN.DOE@EXAMPLE.COM");
    var aggressiveResult = EmailNormalizer.Normalize("JOHN.DOE@EXAMPLE.COM", stripPlusForUnknownProviders: true);

    defaultResult.Should().Be("john.doe@example.com");
    aggressiveResult.Should().Be("john.doe@example.com");
  }


  [Fact]
  public void Normalize_UnknownProvider_AggressiveMode_StripsPlusButPreservesDots()
  {
    var result = EmailNormalizer.Normalize("john.doe+tag@company.com", stripPlusForUnknownProviders: true);

    result.Should().Be("john.doe@company.com");
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
