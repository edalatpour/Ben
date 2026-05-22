using CommunityToolkit.Datasync.Client.Authentication;
using Ben.Models;

namespace Ben.Services.Auth;

public interface IUnifiedAuthService
{
    event EventHandler? AuthenticationStateChanged;

    bool IsAuthenticated { get; }

    UnifiedAuthProvider ActiveProvider { get; }

    UnifiedAuthSession? CurrentSession { get; }

    Task<AuthenticationToken> GetAuthenticationTokenAsync(CancellationToken cancellationToken = default);

    Task<UnifiedIdentity?> SignInWithProviderAsync(UnifiedAuthProvider provider, CancellationToken cancellationToken = default);

    Task<UnifiedIdentity?> ReauthenticateAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync();
}
