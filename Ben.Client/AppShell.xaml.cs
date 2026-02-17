using Ben.Views;

namespace Ben;

public partial class AppShell : Shell
{
    // private bool _initialNavigationCompleted;

    public AppShell()
    {
        InitializeComponent();

        // // Register the planner page route
        Routing.RegisterRoute(nameof(DailyHostPage), typeof(DailyHostPage));
 
    }

}

