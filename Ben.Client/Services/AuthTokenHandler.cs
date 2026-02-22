using System.Net.Http.Headers;

namespace Ben.Services;

/// <summary>
/// A <see cref="DelegatingHandler"/> that attaches the current user's Bearer token
/// to every outgoing HTTP request made by the Datasync client.
/// When the user is not authenticated no Authorization header is added.
/// </summary>
public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthenticationService _authService;

    public AuthTokenHandler(AuthenticationService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? token = _authService.AccessToken;

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
