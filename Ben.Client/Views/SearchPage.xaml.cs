using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Ben.Models;
using Ben.ViewModels;

namespace Ben.Views;

public partial class SearchPage : ContentPage
{
    private readonly DailyViewModel _viewModel;
    private CancellationTokenSource? _searchDebounceCts;
    private int _searchRequestId;
    private string _searchText = string.Empty;

    public SearchPage(DailyViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = this;
    }

    public ObservableCollection<SearchResultRow> SearchResults { get; } = [];

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
            ScheduleSearch();
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
        _searchDebounceCts?.Cancel();
        int requestId = ++_searchRequestId;
        await PerformSearchCoreAsync(requestId, CancellationToken.None);
    }

    void ScheduleSearch()
    {
        _searchDebounceCts?.Cancel();
        CancellationTokenSource debounceCts = new();
        _searchDebounceCts = debounceCts;
        int requestId = ++_searchRequestId;
        _ = PerformSearchDebouncedAsync(requestId, debounceCts.Token);
    }

    async Task PerformSearchDebouncedAsync(int requestId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(220), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await PerformSearchCoreAsync(requestId, cancellationToken);
    }

    async Task PerformSearchCoreAsync(int requestId, CancellationToken cancellationToken)
    {
        if (requestId != _searchRequestId || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        string normalizedSearch = NormalizeText(SearchText);
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            SearchResults.Clear();
            return;
        }

        try
        {
            List<NoteSearchResult> results = await _viewModel.SearchNotesAsync(normalizedSearch);

            if (requestId != _searchRequestId || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            SearchResults.Clear();
            foreach (NoteSearchResult result in results)
            {
                SearchResults.Add(SearchResultRow.From(result, normalizedSearch, NavigateToSearchResultAsync));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await DisplayAlertAsync("Search failed", "Could not search notes. Please try again.", "OK");
        }
    }

    async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    async Task NavigateToSearchResultAsync(string dateKey)
    {
        if (string.IsNullOrWhiteSpace(dateKey))
        {
            return;
        }

        await Navigation.PopModalAsync();
        await _viewModel.NavigateToPageAsync(dateKey);
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

        public FormattedString RowText { get; init; } = new();

        public static SearchResultRow From(NoteSearchResult result, string searchText, Func<string, Task> navigateToDateKey)
        {
            string dateOrder = $"{result.Date.Month}/{result.Date.Day}.{result.Order}";
            return new SearchResultRow
            {
                DateKey = result.DateKey,
                DateOrder = dateOrder,
                RowText = BuildRowText(result.Text ?? string.Empty, searchText, dateOrder, result.DateKey, navigateToDateKey)
            };
        }

        static FormattedString BuildRowText(
            string text,
            string searchText,
            string dateOrder,
            string dateKey,
            Func<string, Task> navigateToDateKey)
        {
            FormattedString formatted = new();
            Color accent = ResolveColor("Accent", Colors.Blue);
            Color link = ResolveColor("Link", accent);
            Color normalText = ResolveColor("UserText", Colors.Black);

            Span dateSpan = new()
            {
                Text = $"({dateOrder}):",
                TextColor = link,
                TextDecorations = TextDecorations.Underline
            };

            dateSpan.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await navigateToDateKey(dateKey))
            });

            formatted.Spans.Add(dateSpan);
            formatted.Spans.Add(new Span
            {
                Text = " ",
                TextColor = normalText
            });

            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchText))
            {
                formatted.Spans.Add(new Span
                {
                    Text = text,
                    TextColor = normalText
                });
                return formatted;
            }

            Regex matchRegex = new(Regex.Escape(searchText), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            MatchCollection matches = matchRegex.Matches(text);
            if (matches.Count == 0)
            {
                formatted.Spans.Add(new Span
                {
                    Text = text,
                    TextColor = normalText
                });
                return formatted;
            }

            int current = 0;
            foreach (Match match in matches)
            {
                if (!match.Success || match.Index < current)
                {
                    continue;
                }

                if (match.Index > current)
                {
                    formatted.Spans.Add(new Span
                    {
                        Text = text[current..match.Index],
                        TextColor = normalText
                    });
                }

                formatted.Spans.Add(new Span
                {
                    Text = match.Value,
                    TextColor = accent,
                    FontAttributes = FontAttributes.Bold
                });

                current = match.Index + match.Length;
            }

            if (current < text.Length)
            {
                formatted.Spans.Add(new Span
                {
                    Text = text[current..],
                    TextColor = normalText
                });
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
