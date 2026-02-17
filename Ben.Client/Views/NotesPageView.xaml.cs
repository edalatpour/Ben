namespace Ben.Views;

using Ben.Models;
using Ben.ViewModels;
using System.Linq;

public partial class NotesPageView : ContentView
{
    // DailyData _data;

    public NotesPageView()
    {
        InitializeComponent();
    }

    async void OnNewNoteCompleted(object sender, EventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (sender is not Entry entry)
        {
            return;
        }

        string text = NormalizeNoteText(entry.Text);
        if (string.IsNullOrEmpty(text))
        {
            entry.Text = string.Empty;
            return;
        }

        entry.Text = string.Empty;
        await viewModel.AddNoteAsync(text);
        Dispatcher.Dispatch(() =>
        {
            if (NotesList != null && !IsElementVisibleIn(entry, NotesList))
            {
                NotesList.ScrollTo(entry.BindingContext, position: ScrollToPosition.MakeVisible, animate: true);
            }

            entry.Focus();
        });
    }

    static string NormalizeNoteText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Trim non-breaking/zero-width spaces to avoid blank notes.
        return text
            .Replace("\u00A0", " ")
            .Replace("\u200B", " ")
            .Replace("\uFEFF", " ")
            .Trim();
    }

    async void OnEditNoteCompleted(object sender, EventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (sender is not Entry entry)
        {
            return;
        }

        if (entry.BindingContext is not NoteItem note || note.IsPlaceholder)
        {
            return;
        }

        note.IsEditing = false;

        string normalized = NormalizeNoteText(note.Text);
        if (string.IsNullOrEmpty(normalized))
        {
            note.Text = note.EditSnapshot ?? note.Text;
            note.EditSnapshot = null;
            return;
        }

        note.Text = normalized;
        note.EditSnapshot = null;
        await viewModel.UpdateNoteAsync(note);
    }

    static bool IsElementVisibleIn(VisualElement element, VisualElement container)
    {
        if (element == null || container == null)
        {
            return false;
        }

        if (element.Height <= 0 || container.Height <= 0)
        {
            return false;
        }

        if (!TryGetRelativeY(element, container, out double relativeY))
        {
            return false;
        }

        double top = relativeY;
        double bottom = relativeY + element.Height;
        return top >= 0 && bottom <= container.Height;
    }

    static bool TryGetRelativeY(VisualElement element, VisualElement container, out double relativeY)
    {
        relativeY = 0;
        VisualElement current = element;

        while (current != null && current != container)
        {
            relativeY += current.Y + current.TranslationY;
            current = current.Parent as VisualElement;
        }

        return current == container;
    }

    void OnNoteTapped(object sender, EventArgs e)
    {
        if (sender is not Label label)
        {
            return;
        }

        if (label.BindingContext is not NoteItem note || note.IsPlaceholder)
        {
            return;
        }

        note.EditSnapshot = note.Text;
        note.IsEditing = true;

        if (label.Parent is Grid grid)
        {
            Entry editEntry = grid.Children
                .OfType<Entry>()
                .FirstOrDefault(entry => entry.StyleClass?.Contains("NoteEditEntry") == true);
            if (editEntry == null)
            {
                return;
            }

            editEntry.Focus();
            Dispatcher.Dispatch(() =>
            {
                int length = editEntry.Text?.Length ?? 0;
                editEntry.CursorPosition = length;
                editEntry.SelectionLength = 0;
            });
        }
    }

    async void OnEditNoteUnfocused(object sender, FocusEventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (sender is not Entry entry)
        {
            return;
        }

        if (entry.BindingContext is not NoteItem note || note.IsPlaceholder)
        {
            return;
        }

        if (!note.IsEditing)
        {
            return;
        }

        string current = NormalizeNoteText(note.Text);
        string original = NormalizeNoteText(note.EditSnapshot);

        if (string.Equals(current, original, StringComparison.Ordinal))
        {
            note.IsEditing = false;
            note.EditSnapshot = null;
            return;
        }

        if (string.IsNullOrEmpty(current))
        {
            note.Text = note.EditSnapshot ?? note.Text;
            note.IsEditing = false;
            note.EditSnapshot = null;
            return;
        }

        note.IsEditing = false;
        note.EditSnapshot = null;
        await viewModel.UpdateNoteAsync(note);
    }

    // public void Load(DailyData data)
    // {
    //     _data = data;
    //     BindingContext = _data;
    //     NotesList.ItemsSource = data.Notes;
    // }
}
