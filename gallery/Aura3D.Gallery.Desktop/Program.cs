using System;
using Aura3D.Gallery;
using Aura3D.Gallery.Fonts;
using Avalonia;

namespace Aura3D.Gallery.Desktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.StartupArgs = args;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .WithAura3DGalleryFonts()
            .LogToTrace()
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.OpenGl],
            });
}
