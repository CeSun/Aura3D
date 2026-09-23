using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Media;
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
            // 探针：走平台默认（Metal）渲染模式；T3 阶段验证 ANGLE 渲染的 MTLTexture 经 lease 上屏。
            Example.App.RootViewFactory = () => new AngleMetalLeaseProbe().BuildPage();

            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }
    }
}
