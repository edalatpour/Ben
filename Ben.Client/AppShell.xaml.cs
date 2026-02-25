using Ben.Views;

namespace Ben;

public partial class AppShell : Shell
{
    // private bool _initialNavigationCompleted;

    public AppShell()
    {
        InitializeComponent();

        // Register page routes for modal navigation
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }

}

