namespace Ben.Services;

public sealed class NoOpThemeIdentityService : IThemeIdentityService
{
    public void ApplyThemeIdentity(string themeName)
    {
        // Intentionally no-op until platform implementations are added.
    }
}
