namespace Ben.Views;

using Ben.Models;
using Ben.ViewModels;

public partial class NoteDetailsPage : ContentPage
{
    private readonly DailyViewModel _viewModel;
    private readonly NoteItem _note;
    private readonly bool _isNewNote;

    public NoteDetailsPage(DailyViewModel viewModel, NoteItem note = null)
    {
        InitializeComponent();
        _viewModel = viewModel;

        if (note == null)
        {
            _isNewNote = true;
            _note = new NoteItem
            {
                Key = viewModel.CurrentDay?.Key ?? DateTime.Today
            };
        }
        else
        {
            _isNewNote = false;
            _note = note;
        }

        NoteEditor.Text = _note.Text;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(() =>
        {
            NoteEditor.Focus();
            int length = NoteEditor.Text?.Length ?? 0;
            NoteEditor.CursorPosition = length;
            NoteEditor.SelectionLength = 0;
        });
    }

    async void OnSaveClicked(object sender, EventArgs e)
    {
        string text = NormalizeText(NoteEditor.Text);
        if (string.IsNullOrEmpty(text))
        {
            await DisplayAlert("Validation", "Please enter note text.", "OK");
            return;
        }

        if (_isNewNote)
        {
            await _viewModel.AddNoteAsync(text);
        }
        else
        {
            string originalText = _note.Text;
            _note.Text = text;
            try
            {
                await _viewModel.UpdateNoteAsync(_note);
            }
            catch
            {
                _note.Text = originalText;
                throw;
            }
        }

        await Navigation.PopModalAsync();
    }

    static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("\u00A0", " ")
            .Replace("\u200B", " ")
            .Replace("\uFEFF", " ")
            .Trim();
    }

    async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
