using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ben.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private bool _isSyncEnabled;
    private bool _isSignedIn;

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

    public SettingsViewModel()
    {
        // Initialize default values
        IsSyncEnabled = false;
        IsSignedIn = false;
    }

    [RelayCommand]
    private void SignIn()
    {
        // TODO: Implement sign in logic
    }

    [RelayCommand]
    private void SignOut()
    {
        // TODO: Implement sign out logic
    }
}
