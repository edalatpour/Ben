namespace Ben.Views;

using Ben.Models;
using Ben.Services;
using Ben.ViewModels;
using Microsoft.Maui.Devices;

public partial class DailyHostPage : ContentPage
{
    private readonly DailyViewModel _viewModel;
    private readonly AuthenticationService _authService;
    readonly TaskPageView _portraitTasks;
    readonly NotesPageView _portraitNotes;
    bool _isNavigating;
    bool _isLandscape;

    public DailyHostPage(DailyViewModel vm, AuthenticationService authService)
    {
        InitializeComponent();
        _viewModel = vm;
        _authService = authService;
        BindingContext = vm;
        _portraitTasks = new TaskPageView { BindingContext = ViewModel };
        _portraitNotes = new NotesPageView { BindingContext = ViewModel };
        UpdatePortraitPage();
    }

    DailyViewModel ViewModel => _viewModel;

    public bool ShowDesktopArrows { get; } = DeviceInfo.Idiom == DeviceIdiom.Desktop;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        bool isLandscape = width > height;
        _isLandscape = isLandscape;

        LandscapeGrid.IsVisible = isLandscape;
        PortraitGrid.IsVisible = !isLandscape;

        if (!isLandscape)
            UpdatePortraitPage();
    }

    void UpdatePortraitPage()
    {
        if (ViewModel.SubPage == 0)
        {
            SinglePageHost.Content = _portraitTasks;
        }
        else
        {
            SinglePageHost.Content = _portraitNotes;
        }
    }

    async Task PreviousPage()
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            if (_isLandscape)
            {
                if (ViewModel.CurrentDay != null)
                {
                    await ViewModel.LoadDay(ViewModel.CurrentDay.Key.AddDays(-1));
                }
            }
            else
            {
                await ViewModel.GoBackwardAsync();
                UpdatePortraitPage();
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    async Task NextPage()
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            if (_isLandscape)
            {
                if (ViewModel.CurrentDay != null)
                {
                    await ViewModel.LoadDay(ViewModel.CurrentDay.Key.AddDays(1));
                }
            }
            else
            {
                await ViewModel.GoForwardAsync();
                UpdatePortraitPage();
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    async void OnSwipeLeft(object sender, SwipedEventArgs e)
    {
        await NextPage();
    }

    async void OnSwipeRight(object sender, SwipedEventArgs e)
    {
        await PreviousPage();
    }

    async void OnPreviousClicked(object sender, EventArgs e)
    {
        await PreviousPage();
    }

    async void OnNextClicked(object sender, EventArgs e)
    {
        await NextPage();
    }

    async void OnAccountClicked(object sender, EventArgs e)
    {
        if (_authService.IsAuthenticated)
        {
            bool confirm = await DisplayAlert(
                "Sign Out",
                "Sign out and stop syncing? Your local data will be kept.",
                "Sign Out",
                "Cancel");

            if (confirm)
            {
                await _authService.SignOutAsync();
                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            }
        }
        else
        {
            // User is in offline mode; offer to sign in now.
            await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
        }
    }
}
