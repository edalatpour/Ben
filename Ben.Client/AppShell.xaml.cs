namespace Ben;

using Ben.Views;

public partial class AppShell : Shell
{
    // private bool _initialNavigationCompleted;

    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(HelpPage), typeof(HelpPage));
    }

}

