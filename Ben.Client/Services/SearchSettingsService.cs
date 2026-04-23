using System.Text.Json;

namespace Ben.Services;

public sealed class SearchSettingsService
{
    private const int MaxRecentSearches = 20;
    private static readonly string SettingsFilePath = Path.Combine(FileSystem.AppDataDirectory, "search-settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task SaveRecentSearchAsync(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        SearchSettings settings = await LoadAsync();
        string normalized = searchText.Trim();

        settings.RecentSearches.RemoveAll(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        settings.RecentSearches.Insert(0, normalized);

        if (settings.RecentSearches.Count > MaxRecentSearches)
        {
            settings.RecentSearches = settings.RecentSearches
                .Take(MaxRecentSearches)
                .ToList();
        }

        await SaveAsync(settings);
    }

    async Task<SearchSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new SearchSettings();
            }

            await using FileStream stream = File.OpenRead(SettingsFilePath);
            SearchSettings? settings = await JsonSerializer.DeserializeAsync<SearchSettings>(stream);
            return settings ?? new SearchSettings();
        }
        catch
        {
            return new SearchSettings();
        }
    }

    async Task SaveAsync(SearchSettings settings)
    {
        string directory = Path.GetDirectoryName(SettingsFilePath) ?? FileSystem.AppDataDirectory;
        Directory.CreateDirectory(directory);

        await using FileStream stream = File.Create(SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    sealed class SearchSettings
    {
        public List<string> RecentSearches { get; set; } = [];
    }
}