namespace Alos.Email.Validation;

using DnsClient;
using Microsoft.Extensions.Logging;

/// <summary>
///   Validates that an email domain has valid MX records and can receive email.
/// </summary>
/// <remarks>
///   <para>
///     Uses fail-open strategy: if DNS lookup fails due to network issues or timeout,
///     the domain is allowed to prevent blocking legitimate users.
///   </para>
/// </remarks>
public sealed class MxRecordValidator : IMxRecordValidator
{
  #region Fields

  private readonly ILookupClient _lookupClient;
  private readonly ILogger<MxRecordValidator> _logger;

  #endregion


  #region Constructors

  /// <summary>
  ///   Initializes a new instance of the <see cref="MxRecordValidator"/> class.
  /// </summary>
  public MxRecordValidator(ILogger<MxRecordValidator> logger)
  {
    _lookupClient = new LookupClient();
    _logger = logger;
  }


  /// <summary>
  ///   Initializes a new instance of the <see cref="MxRecordValidator"/> class with a custom lookup client.
  /// </summary>
  /// <param name="lookupClient">The DNS lookup client to use.</param>
  /// <param name="logger">The logger.</param>
  internal MxRecordValidator(ILookupClient lookupClient, ILogger<MxRecordValidator> logger)
  {
    _lookupClient = lookupClient;
    _logger = logger;
  }

  #endregion


  #region Methods - Public

  /// <inheritdoc />
  public async Task<bool> HasValidMxRecordsAsync(string domain, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(domain))
      return false;

    try
    {
      var result = await _lookupClient.QueryAsync(domain, QueryType.MX, QueryClass.IN, ct);
      var mxRecords = result.Answers.MxRecords().ToList();

      if (mxRecords.Count == 0)
      {
        _logger.LogDebug("No MX records found for domain {Domain}", domain);

        return false;
      }

      _logger.LogDebug(
        "Found {Count} MX records for domain {Domain}",
        mxRecords.Count,
        domain);

      return true;
    }
    catch (DnsResponseException ex)
    {
      // DNS lookup failed (domain doesn't exist, NXDOMAIN, etc.)
      _logger.LogDebug(ex, "DNS lookup failed for domain {Domain}", domain);

      return false;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      // Network errors, timeouts, etc. - fail open to avoid blocking legitimate users
      _logger.LogWarning(
        ex,
        "MX record check failed for domain {Domain}, allowing registration (fail-open)",
        domain);

      return true;
    }
  }

  #endregion
}
