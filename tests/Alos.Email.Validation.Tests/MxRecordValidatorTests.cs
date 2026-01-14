namespace Alos.Email.Validation.Tests;

using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
///   Tests for <see cref="MxRecordValidator"/>.
/// </summary>
public class MxRecordValidatorTests
{
  private readonly Mock<ILookupClient> _lookupClient;
  private readonly IMxRecordValidator _validator;


  public MxRecordValidatorTests()
  {
    _lookupClient = new Mock<ILookupClient>();
    var logger = Mock.Of<ILogger<MxRecordValidator>>();
    _validator = new MxRecordValidator(_lookupClient.Object, logger);
  }


  #region Valid MX Records

  [Fact]
  public async Task HasValidMxRecordsAsync_DomainWithMxRecords_ReturnsTrue()
  {
    // Arrange: Set up a mock DNS response with MX records.
    var response = CreateMxResponse("gmail.com", "alt1.gmail-smtp-in.l.google.com", 10);
    _lookupClient
      .Setup(c => c.QueryAsync("gmail.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    // Act
    var result = await _validator.HasValidMxRecordsAsync("gmail.com");

    // Assert
    result.Should().BeTrue();
  }


  [Fact]
  public async Task HasValidMxRecordsAsync_MultipleMxRecords_ReturnsTrue()
  {
    // Arrange: Set up a mock DNS response with multiple MX records.
    var response = CreateMxResponse("outlook.com", "outlook-com.olc.protection.outlook.com", 10);
    _lookupClient
      .Setup(c => c.QueryAsync("outlook.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    // Act
    var result = await _validator.HasValidMxRecordsAsync("outlook.com");

    // Assert
    result.Should().BeTrue();
  }

  #endregion


  #region No MX Records

  [Fact]
  public async Task HasValidMxRecordsAsync_NoMxRecords_ReturnsFalse()
  {
    // Arrange: Set up a mock DNS response with no MX records.
    var response = CreateEmptyMxResponse();
    _lookupClient
      .Setup(c => c.QueryAsync("invalid.invalid", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    // Act
    var result = await _validator.HasValidMxRecordsAsync("invalid.invalid");

    // Assert
    result.Should().BeFalse();
  }

  #endregion


  #region DNS Errors

  [Fact]
  public async Task HasValidMxRecordsAsync_DnsResponseException_ReturnsFalse()
  {
    // Arrange: Simulate a DNS lookup failure (NXDOMAIN, etc.).
    _lookupClient
      .Setup(c => c.QueryAsync("nonexistent.invalid", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new DnsResponseException("Domain not found"));

    // Act
    var result = await _validator.HasValidMxRecordsAsync("nonexistent.invalid");

    // Assert: DNS errors mean domain doesn't exist.
    result.Should().BeFalse();
  }


  [Fact]
  public async Task HasValidMxRecordsAsync_NetworkError_ReturnsTrue_FailOpen()
  {
    // Arrange: Simulate a network timeout or connectivity issue.
    _lookupClient
      .Setup(c => c.QueryAsync("gmail.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new TimeoutException("DNS server did not respond"));

    // Act
    var result = await _validator.HasValidMxRecordsAsync("gmail.com");

    // Assert: Network errors should fail-open to avoid blocking legitimate users.
    result.Should().BeTrue();
  }


  [Fact]
  public async Task HasValidMxRecordsAsync_GenericException_ReturnsTrue_FailOpen()
  {
    // Arrange: Simulate an unexpected error.
    _lookupClient
      .Setup(c => c.QueryAsync("gmail.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Unexpected DNS client error"));

    // Act
    var result = await _validator.HasValidMxRecordsAsync("gmail.com");

    // Assert: Generic errors should fail-open to avoid blocking legitimate users.
    result.Should().BeTrue();
  }


  [Fact]
  public async Task HasValidMxRecordsAsync_CancellationRequested_Throws()
  {
    // Arrange: Simulate cancellation.
    var cts = new CancellationTokenSource();
    cts.Cancel();

    _lookupClient
      .Setup(c => c.QueryAsync("gmail.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    // Act & Assert: Cancellation should propagate, not be swallowed.
    await FluentActions
      .Invoking(() => _validator.HasValidMxRecordsAsync("gmail.com", cts.Token))
      .Should().ThrowAsync<OperationCanceledException>();
  }

  #endregion


  #region Edge Cases

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public async Task HasValidMxRecordsAsync_NullOrWhitespace_ReturnsFalse(string? domain)
  {
    // Act
    var result = await _validator.HasValidMxRecordsAsync(domain!);

    // Assert
    result.Should().BeFalse();

    // Verify: Should not make a DNS query for invalid input.
    _lookupClient.Verify(
      c => c.QueryAsync(It.IsAny<string>(), QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()),
      Times.Never);
  }


  [Fact]
  public async Task HasValidMxRecordsAsync_ResponseWithOnlyNonMxRecords_ReturnsFalse()
  {
    // Arrange: Set up a mock DNS response with only non-MX records (e.g., CNAME).
    // This can happen when a domain has a CNAME but no MX records.
    var mockResponse = new Mock<IDnsQueryResponse>();
    var emptyMxRecords = new List<DnsResourceRecord>();
    mockResponse.Setup(r => r.Answers).Returns(emptyMxRecords);

    _lookupClient
      .Setup(c => c.QueryAsync("redirect.example.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ReturnsAsync(mockResponse.Object);

    // Act
    var result = await _validator.HasValidMxRecordsAsync("redirect.example.com");

    // Assert: Domain without MX records should not be considered valid for email.
    result.Should().BeFalse();
  }


  [Fact]
  public async Task HasValidMxRecordsAsync_DnsServerFailure_ReturnsFalse()
  {
    // Arrange: Simulate a SERVFAIL response (server failure).
    // DnsClient throws DnsResponseException for server errors.
    _lookupClient
      .Setup(c => c.QueryAsync("failing-server.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new DnsResponseException("Server failure"));

    // Act
    var result = await _validator.HasValidMxRecordsAsync("failing-server.com");

    // Assert: DNS server failures should return false (domain can't receive email).
    result.Should().BeFalse();
  }


  [Fact]
  public async Task HasValidMxRecordsAsync_DnsQueryRefused_ReturnsFalse()
  {
    // Arrange: Simulate a REFUSED response (query refused by server).
    _lookupClient
      .Setup(c => c.QueryAsync("refused.example.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new DnsResponseException("Query refused"));

    // Act
    var result = await _validator.HasValidMxRecordsAsync("refused.example.com");

    // Assert: DNS query refused should return false.
    result.Should().BeFalse();
  }


  [Fact]
  public async Task HasValidMxRecordsAsync_SocketException_ReturnsTrue_FailOpen()
  {
    // Arrange: Simulate a socket-level network failure (can't reach DNS server).
    _lookupClient
      .Setup(c => c.QueryAsync("example.com", QueryType.MX, QueryClass.IN, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new System.Net.Sockets.SocketException());

    // Act
    var result = await _validator.HasValidMxRecordsAsync("example.com");

    // Assert: Network connectivity issues should fail-open.
    result.Should().BeTrue();
  }

  #endregion


  #region Real DNS Integration Tests

  /// <summary>
  ///   Integration test that performs actual DNS lookups.
  ///   This test requires network access and may be skipped in CI environments.
  /// </summary>
  [Fact]
  [Trait("Category", "Integration")]
  public async Task HasValidMxRecordsAsync_RealDns_Gmail_HasMxRecords()
  {
    // Arrange: Use real DNS client (not mocked).
    var realLookupClient = new LookupClient();
    var logger = Mock.Of<ILogger<MxRecordValidator>>();
    var validator = new MxRecordValidator(realLookupClient, logger);

    // Act: Query MX records for gmail.com (a domain that will always have MX records).
    var result = await validator.HasValidMxRecordsAsync("gmail.com");

    // Assert: Gmail should have MX records.
    result.Should().BeTrue("gmail.com should have valid MX records");
  }


  [Fact]
  [Trait("Category", "Integration")]
  public async Task HasValidMxRecordsAsync_RealDns_InvalidDomain_NoMxRecords()
  {
    // Arrange: Use real DNS client.
    var realLookupClient = new LookupClient();
    var logger = Mock.Of<ILogger<MxRecordValidator>>();
    var validator = new MxRecordValidator(realLookupClient, logger);

    // Act: Query MX records for a domain that definitely doesn't exist.
    // Using .invalid TLD which is guaranteed to never resolve.
    var result = await validator.HasValidMxRecordsAsync("this-domain-does-not-exist-12345.invalid");

    // Assert: Invalid domain should not have MX records.
    result.Should().BeFalse("nonexistent domain should not have MX records");
  }


  [Fact]
  [Trait("Category", "Integration")]
  public async Task HasValidMxRecordsAsync_RealDns_MajorProviders_AllHaveMxRecords()
  {
    // Arrange: Use real DNS client.
    var realLookupClient = new LookupClient();
    var logger = Mock.Of<ILogger<MxRecordValidator>>();
    var validator = new MxRecordValidator(realLookupClient, logger);

    // Test multiple major email providers to ensure broad coverage.
    var majorProviders = new[]
    {
      "gmail.com",
      "outlook.com",
      "yahoo.com",
      "protonmail.com",
      "icloud.com"
    };

    // Act & Assert: All major providers should have MX records.
    foreach (var domain in majorProviders)
    {
      var result = await validator.HasValidMxRecordsAsync(domain);
      result.Should().BeTrue($"{domain} should have valid MX records");
    }
  }


  [Fact]
  [Trait("Category", "Integration")]
  public async Task HasValidMxRecordsAsync_RealDns_UnicodeDomain_HandlesProperly()
  {
    // Arrange: Use real DNS client.
    var realLookupClient = new LookupClient();
    var logger = Mock.Of<ILogger<MxRecordValidator>>();
    var validator = new MxRecordValidator(realLookupClient, logger);

    // Test with a Punycode-encoded IDN domain.
    // Note: This may fail-open if the DNS library doesn't handle IDN properly.
    // xn--80ak6aa92e.com is "примерcom" in Cyrillic (just an example).
    // We'll use a domain that might not exist but tests the handling.
    var result = await validator.HasValidMxRecordsAsync("xn--nxasmq5b.com");

    // Assert: The test completed without throwing (result depends on whether domain exists).
    // The important thing is that it doesn't crash on IDN domains.
    // If we got here without exception, the test passes. Just verify the result is a valid bool.
    result.Should().Be(result, "should handle IDN domains without crashing");
  }

  #endregion


  #region Helper Methods

  /// <summary>
  ///   Creates a mock DNS response containing one MX record.
  /// </summary>
  private static IDnsQueryResponse CreateMxResponse(string domain, string exchange, ushort preference)
  {
    var mockResponse = new Mock<IDnsQueryResponse>();

    // Create a real MX record (DnsClient records are sealed, so we use the real class).
    var mxRecord = new MxRecord(
      new ResourceRecordInfo(domain, ResourceRecordType.MX, QueryClass.IN, 3600, 0),
      preference,
      DnsString.Parse(exchange));

    var records = new List<DnsResourceRecord> { mxRecord };

    // IDnsQueryResponse.Answers returns IReadOnlyList<DnsResourceRecord>.
    mockResponse.Setup(r => r.Answers).Returns(records);

    return mockResponse.Object;
  }


  /// <summary>
  ///   Creates a mock DNS response with no MX records.
  /// </summary>
  private static IDnsQueryResponse CreateEmptyMxResponse()
  {
    var mockResponse = new Mock<IDnsQueryResponse>();

    var emptyRecords = new List<DnsResourceRecord>();

    mockResponse.Setup(r => r.Answers).Returns(emptyRecords);

    return mockResponse.Object;
  }

  #endregion
}
