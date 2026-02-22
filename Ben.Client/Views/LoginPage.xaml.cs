using Ben.Services;

namespace Ben.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthenticationService _authService;

    public LoginPage(AuthenticationService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Attempt to silently restore a previous session (or honour a prior "skip" choice).
        // If successful, go straight to the main page without showing the login UI.
        if (await _authService.TryRestoreSessionAsync())
        {
            await NavigateToMainAsync();
        }
    }

    private async void OnMicrosoftClicked(object sender, EventArgs e)
    {
        await TrySignInAsync(() => _authService.SignInWithMicrosoftAsync());
    }

    private async void OnGoogleClicked(object sender, EventArgs e)
    {
        await TrySignInAsync(() => _authService.SignInWithGoogleAsync());
    }

    private async void OnAppleClicked(object sender, EventArgs e)
    {
        await TrySignInAsync(() => _authService.SignInWithAppleAsync());
    }

    private async void OnSkipClicked(object sender, EventArgs e)
    {
        // Persist the "skip" choice so this screen is not shown again on subsequent launches.
        _authService.MarkAsSkipped();
        await NavigateToMainAsync();
    }

    private async Task TrySignInAsync(Func<Task<bool>> signInFunc)
    {
        SetButtonsEnabled(false);
        SignInActivity.IsVisible = true;
        SignInActivity.IsRunning = true;

        try
        {
            bool success = await signInFunc();

            if (success)
            {
                await NavigateToMainAsync();
            }
            else
            {
                await DisplayAlert(
                    "Sign In Failed",
                    "Unable to complete sign in. Please try again or continue without signing in.",
                    "OK");
            }
        }
        finally
        {
            SignInActivity.IsRunning = false;
            SignInActivity.IsVisible = false;
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        MicrosoftButton.IsEnabled = enabled;
        GoogleButton.IsEnabled = enabled;
        AppleButton.IsEnabled = enabled;
        SkipButton.IsEnabled = enabled;
    }

    private static Task NavigateToMainAsync()
    {
        return Shell.Current.GoToAsync($"//{nameof(DailyHostPage)}");
    }
}
