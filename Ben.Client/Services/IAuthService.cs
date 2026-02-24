namespace Ben.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    bool IsLocalOnly { get; }
    string? UserDisplayName { get; }
    string? UserEmail { get; }

    Task<bool> SignInWithMicrosoftAsync();
    Task<bool> SignInWithGoogleAsync();
    Task<bool> SignInWithAppleAsync();
    void UseLocalOnly();
    Task SignOutAsync();
    Task<string?> GetAccessTokenAsync();
    Task InitializeAsync();
}
