using System;
using System.Diagnostics;

namespace Ben.Services;

public class LoggingHandler : DelegatingHandler
{
    public LoggingHandler() : base()
    {
    }

    public LoggingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[HTTP] >>> {request.Method} {request.RequestUri}");
        await WriteContentAsync(request.Content, cancellationToken);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        Debug.WriteLine($"[HTTP] <<< {response.StatusCode} {response.ReasonPhrase}");
        await WriteContentAsync(response.Content, cancellationToken);

        return response;
    }

    private static async Task WriteContentAsync(HttpContent? content, CancellationToken cancellationToken = default)
    {
        if (content != null)
        {
            Debug.WriteLine($"[HTTP] >>> {await content.ReadAsStringAsync(cancellationToken)}");
        }
    }
}
