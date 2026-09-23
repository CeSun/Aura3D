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
            // T4 A/B：true = ANGLE 宿主（默认 Metal 合成模式）；false = Aura3DView 基线（强制 OpenGL）。
            const bool useAngleHost = true;
            const bool useRealMainView = false;

            if (!useRealMainView)
            {
                Example.App.RootViewFactory = useAngleHost
                    ? AngleHostPage.BuildAnglePage
                    : AngleHostPage.BuildGlBaselinePage;
            }

            if (useAngleHost)
            {
                return base.CustomizeAppBuilder(builder)
                    .WithInterFont();
            }

            return base.CustomizeAppBuilder(builder)
                .With(new iOSPlatformOptions { RenderingMode = [iOSRenderingMode.OpenGl] })
                .WithInterFont();
        }
    }
}
