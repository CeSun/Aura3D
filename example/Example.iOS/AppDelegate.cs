using System;
using Avalonia;
using Avalonia.Threading;
using Avalonia.iOS;
using CommunityToolkit.Mvvm.Messaging;
using Example;
using Example.ViewModels;
using Foundation;
using UIKit;

namespace Example.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the 
    // User Interface of the application, as well as listening (and optionally responding) to 
    // application events from iOS.
    [Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            // On iOS, Aura3DView uses the ANGLE(Metal) backend built into Aura3D.Avalonia,
            // so the app needs no platform-specific handling; the host app renderer is no longer forced to OpenGL.
            var result = base.CustomizeAppBuilder(builder)
                .WithInterFont();

            // Verification helper (exists only in this project): AURA_IOS_OPENGL=1 forces the rendering mode to OpenGl(EAGL),
            // which is used to verify on the simulator that Aura3D falls back to OpenGlControlBase.
            var forceGl = !string.IsNullOrEmpty(NSProcessInfo.ProcessInfo.Environment?["AURA_IOS_OPENGL"]?.ToString());
            return forceGl
                ? result.With(new iOSPlatformOptions { RenderingMode = [iOSRenderingMode.OpenGl] })
                : result;
        }

        private bool _navScheduled;

        public AppDelegate()
        {
            // Verification helper (exists only in this project): AURA_PAGE=&lt;menu title fragment&gt; jumps straight to that page at startup,
            // used for simulator screenshot regression. Under the .NET 10 unified bindings the lifecycle methods cannot be overridden, so a notification observer is used instead.
            UIApplication.Notifications.ObserveDidBecomeActive((_, _) =>
            {
                if (_navScheduled)
                    return;
                var target = NSProcessInfo.ProcessInfo.Environment?["AURA_PAGE"]?.ToString();
                if (string.IsNullOrEmpty(target))
                    return;
                _navScheduled = true;

                Dispatcher.UIThread.Post(() =>
                {
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        if (global::Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes
                                .ISingleViewApplicationLifetime { MainView: { DataContext: MainViewViewModel vm } })
                        {
                            foreach (var menu in vm.Menus)
                            {
                                if (menu.Title!.Contains(target, StringComparison.OrdinalIgnoreCase))
                                {
                                    WeakReferenceMessenger.Default.Send(menu, "JumpTo");
                                    break;
                                }
                            }
                        }
                    };
                    timer.Start();
                });
            });
        }
    }
}
