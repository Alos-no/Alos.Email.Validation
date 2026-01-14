namespace Alos.Email.Validation.Tests.Shared.TestHelpers;

using System.Net;
using Moq;
using Moq.Protected;

/// <summary>
///   Factory methods for creating HTTP-related test mocks.
/// </summary>
public static class HttpMockFactory
{
  /// <summary>
  ///   Creates a mock HTTP message handler that returns specified content for blocklist/allowlist URLs.
  /// </summary>
  /// <param name="blocklistContent">Content to return for blocklist URLs (URLs containing "blocklist").</param>
  /// <param name="allowlistContent">Content to return for allowlist URLs (URLs containing "allowlist").</param>
  /// <param name="throwException">If true, throws HttpRequestException instead of returning content.</param>
  /// <returns>A configured HttpMessageHandler mock.</returns>
  public static HttpMessageHandler CreateMockHttpHandler(
    string blocklistContent = "",
    string allowlistContent = "",
    bool throwException = false)
  {
    var mockHandler = new Mock<HttpMessageHandler>();

    if (throwException)
    {
      mockHandler
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
          "SendAsync",
          ItExpr.IsAny<HttpRequestMessage>(),
          ItExpr.IsAny<CancellationToken>())
        .ThrowsAsync(new HttpRequestException("Simulated network error"));
    }
    else
    {
      mockHandler
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
          "SendAsync",
          ItExpr.IsAny<HttpRequestMessage>(),
          ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
        {
          var content = request.RequestUri?.ToString().Contains("blocklist") == true
            ? blocklistContent
            : allowlistContent;

          return new HttpResponseMessage(HttpStatusCode.OK)
          {
            Content = new StringContent(content)
          };
        });
    }

    return mockHandler.Object;
  }


  /// <summary>
  ///   Creates a mock HTTP message handler that tracks requested URLs and supports
  ///   custom blocklist/allowlist URLs with distinct content.
  /// </summary>
  /// <param name="blocklistContent">Content for the primary blocklist URL.</param>
  /// <param name="allowlistContent">Content for the primary allowlist URL.</param>
  /// <param name="customBlocklistContents">Array of content for custom blocklist URLs (by index).</param>
  /// <param name="customAllowlistContents">Array of content for custom allowlist URLs (by index).</param>
  /// <param name="requestedUrls">List to track all requested URLs (populated during test execution).</param>
  /// <returns>A configured HttpMessageHandler mock.</returns>
  public static HttpMessageHandler CreateMockHttpHandlerWithUrlTracking(
    string blocklistContent,
    string allowlistContent,
    string[] customBlocklistContents,
    string[] customAllowlistContents,
    List<string> requestedUrls)
  {
    var customBlocklistIndex = 0;
    var customAllowlistIndex = 0;

    var mockHandler = new Mock<HttpMessageHandler>();

    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        var url = request.RequestUri?.ToString() ?? "";
        requestedUrls.Add(url);

        string content;

        // Determine content based on URL patterns.
        if (url.Contains("custom") && url.Contains("blocklist"))
        {
          content = customBlocklistIndex < customBlocklistContents.Length
            ? customBlocklistContents[customBlocklistIndex++]
            : "";
        }
        else if (url.Contains("custom") && url.Contains("allowlist"))
        {
          content = customAllowlistIndex < customAllowlistContents.Length
            ? customAllowlistContents[customAllowlistIndex++]
            : "";
        }
        else if (url.Contains("blocklist"))
        {
          content = blocklistContent;
        }
        else
        {
          content = allowlistContent;
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(content)
        };
      });

    return mockHandler.Object;
  }


  /// <summary>
  ///   Creates a mock HTTP message handler that fails for URLs matching a pattern
  ///   but succeeds for others.
  /// </summary>
  /// <param name="blocklistContent">Content for successful blocklist URLs.</param>
  /// <param name="allowlistContent">Content for successful allowlist URLs.</param>
  /// <param name="failingUrlPattern">Pattern to match for URLs that should fail.</param>
  /// <returns>A configured HttpMessageHandler mock.</returns>
  public static HttpMessageHandler CreateMockHttpHandlerWithSelectiveFailure(
    string blocklistContent,
    string allowlistContent,
    string failingUrlPattern)
  {
    var mockHandler = new Mock<HttpMessageHandler>();

    mockHandler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
      {
        var url = request.RequestUri?.ToString() ?? "";

        // Fail for URLs matching the pattern.
        if (url.Contains(failingUrlPattern))
        {
          throw new HttpRequestException($"Simulated failure for {url}");
        }

        // Succeed for other URLs.
        var content = url.Contains("blocklist") ? blocklistContent : allowlistContent;

        // For custom URLs that succeed, return specific content.
        if (url.Contains("custom2") && url.Contains("blocklist"))
        {
          content = "custom2-blocked.com";
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(content)
        };
      });

    return mockHandler.Object;
  }


  /// <summary>
  ///   Creates a mock IHttpClientFactory that returns a client using the provided handler.
  /// </summary>
  /// <param name="handler">The HttpMessageHandler to use for created clients.</param>
  /// <returns>A configured IHttpClientFactory mock.</returns>
  public static IHttpClientFactory CreateMockHttpClientFactory(HttpMessageHandler handler)
  {
    var mockFactory = new Mock<IHttpClientFactory>();
    mockFactory
      .Setup(f => f.CreateClient(It.IsAny<string>()))
      .Returns(new HttpClient(handler));

    return mockFactory.Object;
  }
}
