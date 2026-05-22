namespace Ben.Services.Auth;

public interface IAuthenticationLifecycleCoordinator
{
    Task<bool> InitializeSignedInUserAsync();

    Task<bool> SignOutAsync();

    Task<bool> ResetLocalDataAsync();

    Task<bool> SignOutWithCleanupAsync();
}
