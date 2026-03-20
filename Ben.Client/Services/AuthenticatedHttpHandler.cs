using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Bennie.Services;

public class AuthenticatedHttpHandler : DelegatingHandler
{
    private readonly AuthenticationService _authService;

    public AuthenticatedHttpHandler(AuthenticationService authService, HttpMessageHandler? innerHandler = null)
        : base(innerHandler ?? new HttpClientHandler())
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Acquire token
        var result = await _authService.SignInAsync();
        if (result != null && !string.IsNullOrEmpty(result.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
