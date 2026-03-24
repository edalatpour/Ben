namespace Ben.Views;

using Ben.Models;
using Ben.Services;
using Ben.ViewModels;

#nullable enable

public partial class NoteDetailsPage : ContentPage
{
    private readonly DailyViewModel _viewModel;
    private readonly NoteItem _note;
    private readonly bool _isNewNote;
    private bool _isSaving;

    public NoteDetailsPage(DailyViewModel viewModel)
        : this(viewModel, note: null)
    {
    }

    public NoteDetailsPage(DailyViewModel viewModel, NoteItem? note)
    {
        InitializeComponent();
        _viewModel = viewModel;

        if (note == null)
        {
            _isNewNote = true;
            _note = new NoteItem
            {
                Key = viewModel.CurrentDay?.Key ?? KeyConvention.ToDateKey(DateTime.Today)
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
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
        {
            NoteEditor.Focus();
            int length = NoteEditor.Text?.Length ?? 0;
            NoteEditor.CursorPosition = length;
            NoteEditor.SelectionLength = 0;
        });
    }

    async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        if (sender is Button saveButton)
        {
            saveButton.IsEnabled = false;
        }

        try
        {
            string text = NormalizeText(NoteEditor.Text);
            if (string.IsNullOrEmpty(text))
            {
                await DisplayAlertAsync("Validation", "Please enter note text.", "OK");
                return;
            }

            string originalText = _note.Text;
            try
            {
                await _viewModel.SaveNoteDetailsLocallyAsync(_note, text, _isNewNote);
            }
            catch
            {
                _note.Text = originalText;
                throw;
            }

            await Navigation.PopModalAsync();

            _ = _viewModel.CompleteNoteSaveAfterCloseAsync(_note, _isNewNote);
        }
        catch
        {
            await DisplayAlertAsync("Save failed", "Could not save the note. Please try again.", "OK");
        }
        finally
        {
            _isSaving = false;
            if (sender is Button saveButtonFinal)
            {
                saveButtonFinal.IsEnabled = true;
            }
        }
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
