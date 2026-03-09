namespace Ben.Views;

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

