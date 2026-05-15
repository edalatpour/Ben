using CommunityToolkit.Datasync.Client.Authentication;
using Ben.Models;

namespace Ben.Services.Auth;

public sealed class UnifiedAuthService : IUnifiedAuthService
{
    private readonly AuthenticationService _authenticationService;
    private readonly ExternalIdAuthService _externalIdAuthService;
    private readonly IUnifiedAuthSessionStore _sessionStore;

    public UnifiedAuthService(
        AuthenticationService authenticationService,
        ExternalIdAuthService externalIdAuthService,
        IUnifiedAuthSessionStore sessionStore)
    {
        _authenticationService = authenticationService;
        _externalIdAuthService = externalIdAuthService;
        _sessionStore = sessionStore;

        _authenticationService.AuthenticationStateChanged += OnProviderAuthenticationStateChanged;
        _externalIdAuthService.AuthenticationStateChanged += OnProviderAuthenticationStateChanged;

        PersistCurrentSessionSnapshot();
    }

    public event EventHandler? AuthenticationStateChanged;

    public bool IsAuthenticated => _authenticationService.IsAuthenticated || _externalIdAuthService.IsAuthenticated;

    public UnifiedAuthProvider ActiveProvider
    {
        get
        {
            if (_externalIdAuthService.IsAuthenticated)
            {
                return ProviderFromExternalIdName(_externalIdAuthService.Provider);
            }

            if (_authenticationService.IsAuthenticated)
            {
                return UnifiedAuthProvider.Microsoft;
            }

            return UnifiedAuthProvider.None;
        }
    }

    public UnifiedAuthSession? CurrentSession => _sessionStore.Load();

    public Task<AuthenticationToken> GetAuthenticationTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_externalIdAuthService.IsAuthenticated)
        {
            return _externalIdAuthService.GetAuthenticationTokenAsync(cancellationToken);
        }

        return _authenticationService.GetAuthenticationTokenAsync(cancellationToken);
    }

    public async Task<UnifiedIdentity?> SignInWithProviderAsync(
        UnifiedAuthProvider provider,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        return provider switch
        {
            UnifiedAuthProvider.Microsoft => await SignInWithMicrosoftAsync(),
            UnifiedAuthProvider.Apple => await _externalIdAuthService.AuthenticateAsync(),
            UnifiedAuthProvider.Google => await SignInWithGooglePlaceholderAsync(),
            _ => null
        };
    }

    public async Task SignOutAsync()
    {
        if (_authenticationService.IsAuthenticated)
        {
            await _authenticationService.SignOutAsync();
        }

        if (_externalIdAuthService.IsAuthenticated)
        {
            _externalIdAuthService.SignOut();
        }

        _sessionStore.Clear();
    }

    private async Task<UnifiedIdentity?> SignInWithMicrosoftAsync()
    {
        var result = await _authenticationService.SignInAsync();
        if (result == null)
        {
            return null;
        }

        return new UnifiedIdentity
        {
            Provider = UnifiedAuthProvider.Microsoft.ToString(),
            UserId = result.Account?.HomeAccountId?.Identifier ?? result.Account?.Username ?? string.Empty,
            Email = result.Account?.Username ?? string.Empty,
            Name = result.Account?.Username ?? string.Empty,
            AccessToken = result.AccessToken,
            IdToken = result.IdToken
        };
    }

    private static Task<UnifiedIdentity?> SignInWithGooglePlaceholderAsync()
    {
        return Task.FromResult<UnifiedIdentity?>(null);
    }

    private void OnProviderAuthenticationStateChanged(object? sender, EventArgs e)
    {
        PersistCurrentSessionSnapshot();
        AuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PersistCurrentSessionSnapshot()
    {
        if (!IsAuthenticated)
        {
            _sessionStore.Clear();
            return;
        }

        UnifiedAuthSession session = ActiveProvider switch
        {
            UnifiedAuthProvider.Microsoft => new UnifiedAuthSession
            {
                Provider = UnifiedAuthProvider.Microsoft,
                UserId = _authenticationService.UserEmail ?? string.Empty,
                Email = _authenticationService.UserEmail ?? string.Empty,
                Name = _authenticationService.UserName ?? string.Empty
            },
            UnifiedAuthProvider.Apple or UnifiedAuthProvider.Google => new UnifiedAuthSession
            {
                Provider = ProviderFromExternalIdName(_externalIdAuthService.Provider),
                UserId = _externalIdAuthService.UserEmail ?? _externalIdAuthService.UserName ?? string.Empty,
                Email = _externalIdAuthService.UserEmail ?? string.Empty,
                Name = _externalIdAuthService.UserName ?? string.Empty
            },
            _ => new UnifiedAuthSession { Provider = UnifiedAuthProvider.None }
        };

        _sessionStore.Save(session);
    }

    private static UnifiedAuthProvider ProviderFromExternalIdName(string? provider)
    {
        if (string.Equals(provider, "google", StringComparison.OrdinalIgnoreCase))
        {
            return UnifiedAuthProvider.Google;
        }

        if (string.Equals(provider, "apple", StringComparison.OrdinalIgnoreCase))
        {
            return UnifiedAuthProvider.Apple;
        }

        return UnifiedAuthProvider.Apple;
    }
}
