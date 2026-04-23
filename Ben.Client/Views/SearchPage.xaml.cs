using System.Collections.ObjectModel;
using Ben.Models;
using Ben.Services;
using Ben.ViewModels;

namespace Ben.Views;

public partial class SearchPage : ContentPage
{
    private readonly DailyViewModel _viewModel;
    private readonly SearchSettingsService _searchSettingsService = new();
    private bool _isSearching;
    private SearchResultRow? _selectedResult;
    private string _searchText = string.Empty;

    public SearchPage(DailyViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = this;
    }

    public ObservableCollection<SearchResultRow> SearchResults { get; } = [];

    public SearchResultRow? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (ReferenceEquals(_selectedResult, value))
            {
                return;
            }

            _selectedResult = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (string.Equals(_searchText, value, StringComparison.Ordinal))
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
        {
            SearchEntry.Focus();
        });
    }

    async void OnSearchSubmitted(object sender, EventArgs e)
    {
        await PerformSearchAsync(SearchActionButton);
    }

    async void OnSearchClicked(object sender, EventArgs e)
    {
        await PerformSearchAsync(sender as Button);
    }

    async Task PerformSearchAsync(Button? searchButton)
    {
        if (_isSearching)
        {
            return;
        }

        string normalizedSearch = NormalizeText(SearchText);
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            SearchResults.Clear();
            SelectedResult = null;
            return;
        }

        _isSearching = true;
        if (searchButton != null)
        {
            searchButton.IsEnabled = false;
        }

        try
        {
            List<NoteSearchResult> results = await _viewModel.SearchNotesAsync(normalizedSearch);
            await _searchSettingsService.SaveRecentSearchAsync(normalizedSearch);

            SearchResults.Clear();
            foreach (NoteSearchResult result in results)
            {
                SearchResults.Add(SearchResultRow.From(result, normalizedSearch));
            }

            SelectedResult = SearchResults.FirstOrDefault();
        }
        catch
        {
            await DisplayAlertAsync("Search failed", "Could not search notes. Please try again.", "OK");
        }
        finally
        {
            _isSearching = false;
            if (searchButton != null)
            {
                searchButton.IsEnabled = true;
            }
        }
    }

    async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    async void OnGoClicked(object sender, EventArgs e)
    {
        if (SelectedResult == null)
        {
            await DisplayAlertAsync("Selection required", "Select a note result before tapping Go.", "OK");
            return;
        }

        string destinationKey = SelectedResult.DateKey;
        await Navigation.PopModalAsync();
        await _viewModel.NavigateToPageAsync(destinationKey);
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

    public sealed class SearchResultRow
    {
        public string DateKey { get; init; } = string.Empty;

        public string DateOrder { get; init; } = string.Empty;

        public FormattedString HighlightedText { get; init; } = new();

        public static SearchResultRow From(NoteSearchResult result, string searchText)
        {
            return new SearchResultRow
            {
                DateKey = result.DateKey,
                DateOrder = $"{result.Date.Month}/{result.Date.Day}.{result.Order}",
                HighlightedText = BuildHighlightedText(result.Text ?? string.Empty, searchText)
            };
        }

        static FormattedString BuildHighlightedText(string text, string searchText)
        {
            FormattedString formatted = new();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchText))
            {
                formatted.Spans.Add(new Span { Text = text });
                return formatted;
            }

            Color highlightForeground = ResolveColor("WritingPaper", Colors.White);
            Color highlightBackground = ResolveColor("Accent", Colors.Blue);

            int current = 0;
            while (current < text.Length)
            {
                int matchIndex = text.IndexOf(searchText, current, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    formatted.Spans.Add(new Span { Text = text[current..] });
                    break;
                }

                if (matchIndex > current)
                {
                    formatted.Spans.Add(new Span { Text = text[current..matchIndex] });
                }

                formatted.Spans.Add(new Span
                {
                    Text = text.Substring(matchIndex, searchText.Length),
                    TextColor = highlightForeground,
                    BackgroundColor = highlightBackground
                });

                current = matchIndex + searchText.Length;
            }

            return formatted;
        }

        static Color ResolveColor(string key, Color fallback)
        {
            if (Application.Current?.Resources.TryGetValue(key, out object? value) == true
                && value is Color color)
            {
                return color;
            }

            return fallback;
        }
    }
}
