using Ben.Views;

namespace Ben;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(DailyHostPage), typeof(DailyHostPage));
    }
}

