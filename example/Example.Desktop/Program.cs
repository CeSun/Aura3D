using System;
using Aura3D.Examples;
using Aura3D.Examples.Fonts;
using Avalonia;

namespace Example.Desktop;

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
            .WithAura3DExampleFonts()
            .LogToTrace()
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.OpenGl],
            });
}
