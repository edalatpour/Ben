namespace Ben.Views;

using Ben.Models;
using Ben.ViewModels;

public partial class NotesPageView : ContentView
{
    public NotesPageView()
    {
        InitializeComponent();
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

        if (label.BindingContext is not NoteItem note || note.IsPlaceholder)
        {
            return;
        }

        var page = Application.Current?.MainPage;
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

        var page = Application.Current?.MainPage;
        if (page == null)
        {
            return;
        }

        await page.Navigation.PushModalAsync(new NoteDetailsPage(viewModel));
    }
}

