namespace Ben.Views;

using Ben.ViewModels;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Hide the Apple Sign-In button on non-Apple platforms.
#if !IOS && !MACCATALYST
        AppleButton.IsVisible = false;
#endif
    }
}
