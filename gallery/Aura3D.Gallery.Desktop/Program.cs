using System;
using System.IO;
using Aura3D.Avalonia;
using Aura3D.Avalonia.Angle;
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

        // macOS 形态 B：原生 GL-on-Metal 驱动连硬件点图元都剔除（ALIASED_POINT_SIZE_RANGE=[0,0]），
        // 检出 ANGLE macOS 切片时把 3D 视口切到自持 ANGLE(Metal) 会话；未检出时保持原生 GL。
        MacAngleBackend.Enabled = OperatingSystem.IsMacOS()
            && File.Exists(Path.Combine(AppContext.BaseDirectory, "libEGL.dylib"));

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
