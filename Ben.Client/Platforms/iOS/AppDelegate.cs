using Foundation;
using Microsoft.Identity.Client;

namespace Ben;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool OpenUrl(UIKit.UIApplication app, NSUrl url, NSDictionary options)
	{
		AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(url);
		return true;
	}
}
