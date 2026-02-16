// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

using Ben.Services;
using Ben.ViewModels;

namespace Ben;

public partial class App : Application
{

    public App()
    {
        InitializeComponent();

        Services = new ServiceCollection()
            .AddTransient<DailyViewModel>()
            .AddTransient<IAlertService, AlertService>()
            .BuildServiceProvider();

        Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
        {
#if WINDOWS
            handler.PlatformView.Activate();

            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(handler.PlatformView);
            AppWindow appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(windowHandle));
            appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));
#endif
        });

    }

    public IServiceProvider Services { get; }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

}