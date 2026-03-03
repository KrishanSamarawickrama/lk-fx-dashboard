using System.Net;

namespace LkFxDashboard.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;
    private readonly string _contentType;

    public MockHttpMessageHandler(
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string contentType = "text/html")
    {
        _content = content;
        _statusCode = statusCode;
        _contentType = contentType;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, System.Text.Encoding.UTF8, _contentType)
        };

        return Task.FromResult(response);
    }

    public static HttpClient CreateClient(
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string contentType = "text/html")
    {
        var handler = new MockHttpMessageHandler(content, statusCode, contentType);
        return new HttpClient(handler);
    }
}

public class MockHttpMessageHandlerForBytes : HttpMessageHandler
{
    private readonly byte[] _content;
    private readonly HttpStatusCode _statusCode;

    public MockHttpMessageHandlerForBytes(byte[] content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _content = content;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new ByteArrayContent(_content)
        };
        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        return Task.FromResult(response);
    }

    public static HttpClient CreateClient(byte[] content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandlerForBytes(content, statusCode);
        return new HttpClient(handler);
    }
}
