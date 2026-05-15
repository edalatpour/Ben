namespace Ben.Services.Auth;

public sealed class UnifiedAuthSession
{
    public UnifiedAuthProvider Provider { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresOn { get; init; }

    public bool IsAuthenticated => Provider != UnifiedAuthProvider.None
        && !string.IsNullOrWhiteSpace(UserId);
}
