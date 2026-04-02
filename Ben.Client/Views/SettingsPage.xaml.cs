using Ben.Services;
using Ben.ViewModels;

namespace Ben.Views;

public partial class SettingsPage : ContentPage
{
    private static readonly string[] PreviewColorKeys =
    {
        "Paper",
        "WritingPaper",
        "Ink",
        "Line",
        "Accent",
        "Link",
        "UserText"
    };

    private string _originalTheme = "Green";
    private string _selectedTheme = "Green";
    private readonly ThemeService _themeService;
    private readonly DailyViewModel _dailyViewModel;
    private readonly List<ThemeOption> _availableThemes = new()
    {
        new ThemeOption { Name = "Red", DisplayName = "Red" },
        new ThemeOption { Name = "Orange", DisplayName = "Orange" },
        new ThemeOption { Name = "Yellow", DisplayName = "Yellow" },
        new ThemeOption { Name = "Green", DisplayName = "Green" },
        new ThemeOption { Name = "Blue", DisplayName = "Blue" },
        new ThemeOption { Name = "Purple", DisplayName = "Purple" },
        new ThemeOption { Name = "Gray", DisplayName = "Gray" },
    };

    public List<ThemeOption> AvailableThemes => _availableThemes;
    public DailyViewModel AuthViewModel => _dailyViewModel;

    public SettingsPage()
    {
        InitializeComponent();
        _themeService = IPlatformApplication.Current!.Services.GetService<ThemeService>()!;
        _dailyViewModel = IPlatformApplication.Current!.Services.GetRequiredService<DailyViewModel>();
        BindingContext = this;

        // Store the original theme so we can revert if user cancels
        _originalTheme = _themeService.CurrentTheme;
        _selectedTheme = _originalTheme;

        // Set the picker to the current theme
        var currentIndex = _availableThemes.FindIndex(t => t.Name == _originalTheme);
        ThemeColorPicker.SelectedIndex = currentIndex >= 0 ? currentIndex : 3; // Default to Green

        ApplyThemePreview(_selectedTheme);
    }

    private async void OnLoginStatusTapped(object sender, TappedEventArgs e)
    {
        await _dailyViewModel.ToggleAuthenticationAsync();
    }

    private void OnThemeColorSelected(object sender, EventArgs e)
    {
        if (ThemeColorPicker.SelectedItem is ThemeOption selectedTheme)
        {
            _selectedTheme = selectedTheme.Name;

            ApplyThemePreview(_selectedTheme);
        }
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        // Just go back without changing anything
        Navigation.PopAsync();
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        _themeService.SetTheme(_selectedTheme);
        Navigation.PopAsync();
    }

    private void ApplyThemePreview(string themeName)
    {
        foreach (var resourceKey in PreviewColorKeys)
        {
            var themeColor = _themeService.GetThemeColor(themeName, resourceKey);
            if (themeColor != null)
            {
                Resources[resourceKey] = themeColor;
            }
        }
    }
}

public class ThemeOption
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

