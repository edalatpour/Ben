using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ben.Services;

namespace Ben.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AuthenticationService _authService;
    private bool _isSyncEnabled;
    private bool _isSignedIn;
    private string _userEmail = string.Empty;
    private string _userName = string.Empty;

    public bool IsSyncEnabled
    {
        get => _isSyncEnabled;
        set => SetProperty(ref _isSyncEnabled, value);
    }

    public bool IsSignedIn
    {
        get => _isSignedIn;
        set => SetProperty(ref _isSignedIn, value);
    }

    public string UserEmail
    {
        get => _userEmail;
        set => SetProperty(ref _userEmail, value);
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public SettingsViewModel(AuthenticationService authService)
    {
        _authService = authService;
        
        // Initialize from stored auth state
        IsSignedIn = _authService.IsAuthenticated;
        UserEmail = _authService.UserEmail ?? string.Empty;
        UserName = _authService.UserName ?? string.Empty;
        
        IsSyncEnabled = false;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        try
        {
            var result = await _authService.SignInAsync();
            
            if (result != null)
            {
                IsSignedIn = true;
                UserEmail = _authService.UserEmail ?? string.Empty;
                UserName = _authService.UserName ?? string.Empty;
                
                await Application.Current!.MainPage!.DisplayAlert(
                    "Success",
                    $"Signed in as {UserEmail}",
                    "OK");
            }
            else
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "Error",
                    "Failed to sign in. Please try again.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                "Error",
                $"Sign in error: {ex.Message}",
                "OK");
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        try
        {
            await _authService.SignOutAsync();
            
            IsSignedIn = false;
            UserEmail = string.Empty;
            UserName = string.Empty;
            
            await Application.Current!.MainPage!.DisplayAlert(
                "Success",
                "Signed out successfully",
                "OK");
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                "Error",
                $"Sign out error: {ex.Message}",
                "OK");
        }
    }
}
