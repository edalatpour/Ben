namespace Ben.Views;

using Ben.Models;
using Ben.ViewModels;
using Microsoft.Maui.Devices;
using Ben.Services;

public partial class DailyHostPage : ContentPage
{
    private readonly DailyViewModel _viewModel;
    readonly TaskPageView _tasksView;
    readonly NotesPageView _notesView;
    bool _isNavigating;
    bool _isLandscape;

    public DailyHostPage(DailyViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;
        _tasksView = new TaskPageView(_viewModel);
        _notesView = new NotesPageView(_viewModel);
        ApplyLayout();
    }

    DailyViewModel ViewModel => _viewModel;

    public bool ShowDesktopArrows { get; } = DeviceInfo.Idiom == DeviceIdiom.Desktop;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        bool isLandscape = width > height;
        if (_isLandscape == isLandscape)
        {
            return;
        }

        _isLandscape = isLandscape;
        ApplyLayout();
    }

    void UpdatePortraitPage()
    {
        if (ViewModel.SubPage == 0)
        {
            AttachView(SinglePageHost, _tasksView);
        }
        else
        {
            AttachView(SinglePageHost, _notesView);
        }
    }

    void ApplyLayout()
    {
        LandscapeGrid.IsVisible = _isLandscape;
        PortraitGrid.IsVisible = !_isLandscape;

        if (_isLandscape)
        {
            SinglePageHost.Content = null;
            AttachView(LandscapeTasksHost, _tasksView);
            AttachView(LandscapeNotesHost, _notesView);
            return;
        }

        LandscapeTasksHost.Content = null;
        LandscapeNotesHost.Content = null;
        UpdatePortraitPage();
    }

    static void AttachView(ContentView host, View view)
    {
        if (view.Parent is ContentView parentHost && !ReferenceEquals(parentHost, host))
        {
            parentHost.Content = null;
        }

        host.Content = view;
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
                    await ViewModel.NavigatePageAsync(-1);
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
                    await ViewModel.NavigatePageAsync(1);
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

    async void OnSyncStatusTapped(object sender, EventArgs e)
    {
        await ViewModel.ForceSyncAsync();
    }

    async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }

    async void OnCatchUpClicked(object sender, TappedEventArgs e)
    {
        string? currentKey = ViewModel.CurrentDay?.Key;
        if (string.IsNullOrWhiteSpace(currentKey)
            || !KeyConvention.TryParseDateKey(currentKey, out DateTime destinationDate))
        {
            return;
        }

        string destinationDateKey = currentKey;
        var preview = await ViewModel.BuildCatchUpForwardSqlPreviewAsync(destinationDateKey);
        if (preview.Count <= 0)
        {
            await DisplayAlertAsync("Catch Up", $"No open tasks to forward to {destinationDate:yyyy-MM-dd}.", "OK");
            return;
        }

        bool shouldForward = await DisplayAlertAsync(
            "Catch Up",
            $"Forward {preview.Count} open tasks to {destinationDate:yyyy-MM-dd}?",
            "Yes",
            "No");

        if (!shouldForward)
        {
            return;
        }

        var execution = await ViewModel.ExecuteCatchUpAsync(destinationDateKey);
        await DisplayAlertAsync(
            "Catch Up",
            $"Forwarded {execution.CandidateCount} open task(s) to {destinationDate:yyyy-MM-dd}.",
            "OK");
    }
}