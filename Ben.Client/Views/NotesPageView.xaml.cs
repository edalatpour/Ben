namespace Ben.Views;

using System.ComponentModel;
using Ben.Models;
using Ben.ViewModels;

public partial class NotesPageView : ContentView
{
    private readonly DailyViewModel _viewModel;

    public NotesPageView(DailyViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RebindNoteItems();
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(DailyViewModel.CurrentDay), StringComparison.Ordinal))
        {
            return;
        }

        RebindNoteItems();
    }

    void RebindNoteItems()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NotesList.ItemsSource = null;
            NotesList.InvalidateMeasure();
            NotesList.ItemsSource = _viewModel.CurrentDay?.Notes;
        });
    }

    async void OnNoteTapped(object sender, EventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (sender is not Label label)
        {
            return;
        }

        if (label.BindingContext is not NoteItem note)
        {
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        await page.Navigation.PushModalAsync(new NoteDetailsPage(viewModel, note));
    }

    async void OnAddNoteTapped(object sender, EventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        await page.Navigation.PushModalAsync(new NoteDetailsPage(viewModel));
    }
}

