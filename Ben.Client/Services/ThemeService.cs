using Microsoft.Maui.Controls;
using Ben.Resources.Themes;

namespace Ben.Services;

public class ThemeService
{
    private static readonly HashSet<string> ThemeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Red",
        "Orange",
        "Yellow",
        "Green",
        "Blue",
        "Purple",
        "Gray"
    };

    private string _currentTheme;
    private ResourceDictionary? _currentThemeDict;

    public string CurrentTheme => _currentTheme;

    public event EventHandler<string>? ThemeChanged;

    public ThemeService()
    {
        _currentTheme = Preferences.Get("SelectedTheme", "Green");
        System.Diagnostics.Debug.WriteLine($"ThemeService initialized with theme: {_currentTheme}");
    }

    /// <summary>
    /// Initializes the theme service by loading the saved theme on app startup.
    /// Call this from App.xaml.cs after the app is initialized.
    /// </summary>
    public void InitializeTheme()
    {
        SetTheme(_currentTheme, skipPrefsSave: true);
    }

    public void SetTheme(string themeName, bool skipPrefsSave = false)
    {
        try
        {
            var normalizedThemeName = NormalizeThemeName(themeName);
            ResourceDictionary newThemeDict = CreateThemeDictionary(normalizedThemeName);

            var appResources = Application.Current?.Resources;
            if (appResources == null)
            {
                System.Diagnostics.Debug.WriteLine("Application.Current.Resources is null!");
                return;
            }

            var mergedDicts = appResources.MergedDictionaries;

            var existingThemeDictionaries = mergedDicts
                .Where(IsThemeDictionary)
                .ToList();

            foreach (var existingThemeDictionary in existingThemeDictionaries)
            {
                System.Diagnostics.Debug.WriteLine($"Removing previous theme: {existingThemeDictionary.GetType().Name}");
                mergedDicts.Remove(existingThemeDictionary);
            }

            mergedDicts.Add(newThemeDict);
            _currentThemeDict = newThemeDict;

            _currentTheme = normalizedThemeName;

            if (!skipPrefsSave)
            {
                Preferences.Set("SelectedTheme", normalizedThemeName);
            }

            System.Diagnostics.Debug.WriteLine($"Theme dict has {newThemeDict.Count} resources");

            var hasInk = newThemeDict.ContainsKey("Ink");
            var hasLine = newThemeDict.ContainsKey("Line");
            System.Diagnostics.Debug.WriteLine($"Theme dict has Ink: {hasInk}, has Line: {hasLine}");

            var canGetInk = Application.Current?.Resources.TryGetValue("Ink", out var inkColor) == true;
            System.Diagnostics.Debug.WriteLine($"Can get Ink from app resources: {canGetInk}");

            System.Diagnostics.Debug.WriteLine($"Theme changed to: {normalizedThemeName}");
            ThemeChanged?.Invoke(this, normalizedThemeName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting theme to {themeName}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public Color? GetThemeColor(string themeName, string resourceKey)
    {
        var dictionary = CreateThemeDictionary(NormalizeThemeName(themeName));
        if (dictionary.TryGetValue(resourceKey, out var resource) && resource is Color color)
        {
            return color;
        }

        return null;
    }

    private static string NormalizeThemeName(string themeName)
    {
        return ThemeNames.Contains(themeName) ? themeName : "Green";
    }

    private static ResourceDictionary CreateThemeDictionary(string themeName)
    {
        return themeName switch
        {
            "Red" => new Red(),
            "Orange" => new Orange(),
            "Yellow" => new Yellow(),
            "Green" => new Green(),
            "Blue" => new Blue(),
            "Purple" => new Purple(),
            "Gray" => new Gray(),
            _ => new Green()
        };
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        return dictionary.GetType().Namespace == "Ben.Resources.Themes"
            && ThemeNames.Contains(dictionary.GetType().Name);
    }
}
