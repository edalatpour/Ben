using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Ben.Services;

namespace Ben.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;
    private readonly DatasyncSyncService _syncService;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LoginViewModel(IAuthService authService, DatasyncSyncService syncService)
    {
        _authService = authService;
        _syncService = syncService;

        SignInWithMicrosoftCommand = new Command(
            async () => await SignInAsync(() => _authService.SignInWithMicrosoftAsync()),
            () => !IsBusy);

        SignInWithGoogleCommand = new Command(
            async () => await SignInAsync(() => _authService.SignInWithGoogleAsync()),
            () => !IsBusy);

        SignInWithAppleCommand = new Command(
            async () => await SignInAsync(() => _authService.SignInWithAppleAsync()),
            () => !IsBusy);

        ContinueLocallyCommand = new Command(ContinueLocally, () => !IsBusy);
    }

    // ------------------------------------------------------------------
    // Commands
    // ------------------------------------------------------------------

    public ICommand SignInWithMicrosoftCommand { get; }
    public ICommand SignInWithGoogleCommand { get; }
    public ICommand SignInWithAppleCommand { get; }
    public ICommand ContinueLocallyCommand { get; }

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotBusy));
            InvalidateCommands();
        }
    }

    public bool IsNotBusy => !IsBusy;

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
                return;

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task SignInAsync(Func<Task<bool>> signInFunc)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            bool success = await signInFunc();
            if (success)
            {
                _syncService.Restart();
                await NavigateToMainAsync();
            }
            else
            {
                ErrorMessage = "Sign-in was cancelled or failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An error occurred: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ContinueLocally()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            _authService.UseLocalOnly();
        }
        finally
        {
            IsBusy = false;
        }

        _ = NavigateToMainAsync();
    }

    private static Task NavigateToMainAsync() =>
        Shell.Current.GoToAsync("//main");

    private void InvalidateCommands()
    {
        (SignInWithMicrosoftCommand as Command)?.ChangeCanExecute();
        (SignInWithGoogleCommand as Command)?.ChangeCanExecute();
        (SignInWithAppleCommand as Command)?.ChangeCanExecute();
        (ContinueLocallyCommand as Command)?.ChangeCanExecute();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
