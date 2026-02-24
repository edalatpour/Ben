// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

using Ben.Services;

namespace Ben;

public partial class App : Application
{
    private readonly AppShell _appShell;

    public App(AppShell appShell, IAlertService alertService)
    {
        InitializeComponent();

        _appShell = appShell;

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

    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_appShell);
    }
}