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
            // iOS 上 Aura3DView 走 Aura3D.Avalonia 内置的 ANGLE(Metal) 后端，
            // 应用侧无需任何平台特判；宿主 App 渲染器不再被强制为 OpenGL。
            var result = base.CustomizeAppBuilder(builder)
                .WithInterFont();

            // 验证辅助（仅存在于本工程）：AURA_IOS_OPENGL=1 把渲染模式设为 OpenGl(EAGL)，
            // 用于在模拟器上验证 Aura3D 回退到 OpenGlControlBase 的那条路径。
            var forceGl = !string.IsNullOrEmpty(NSProcessInfo.ProcessInfo.Environment?["AURA_IOS_OPENGL"]?.ToString());
            return forceGl
                ? result.With(new iOSPlatformOptions { RenderingMode = [iOSRenderingMode.OpenGl] })
                : result;
        }

        private bool _navScheduled;

        public AppDelegate()
        {
            // 验证辅助（仅存在于本工程）：AURA_PAGE=<菜单标题片段> 启动时直接跳转到该页，
            // 用于模拟器截图回归。.NET 10 统一绑定下生命周期方法不可覆写，改用通知观察。
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
