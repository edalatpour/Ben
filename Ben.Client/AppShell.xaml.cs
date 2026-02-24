using Ben.Services;

namespace Ben;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;

    public AppShell(IAuthService authService)
    {
        _authService = authService;
        InitializeComponent();

        // If the user is already authenticated or has chosen local-only mode,
        // navigate to the main app as soon as the Shell has completed its first navigation.
        if (authService.IsAuthenticated || authService.IsLocalOnly)
        {
            Navigated += OnFirstNavigated;
        }

        // Kick off async token refresh in the background (non-blocking).
        _ = authService.InitializeAsync();
    }

    private async void OnFirstNavigated(object? sender, ShellNavigatedEventArgs args)
    {
        Navigated -= OnFirstNavigated;
        await GoToAsync("//main");
    }
}

