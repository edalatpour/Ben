using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Ben;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "myapp",
    DataHost = "auth")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        // Forward the External ID / WebAuthenticator callback URI back to MAUI
        if (intent?.Data != null)
            WebAuthenticator.Default.OpenUrl(new Uri(intent.Data.ToString()!));
    }
}
