using Aura3D.Examples;
using Aura3D.Examples.Fonts;
using Avalonia;
using Avalonia.iOS;
using Foundation;
using UIKit;

namespace Example.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // iOS 上 Aura3DView 走 Aura3D.Avalonia 内置的 ANGLE(Metal) 后端，应用侧无需任何平台特判，
        // 宿主 App 渲染器也不再被强制为 OpenGL。
        var result = base.CustomizeAppBuilder(builder).WithInterFont().WithAura3DExampleFonts();

        // 验证辅助（仅存在于本工程）：AURA_IOS_OPENGL=1 把渲染模式设为 OpenGl(EAGL)，
        // 用于在模拟器上验证 Aura3D 回退到 OpenGlControlBase 的那条路径。
        var forceGl = NSProcessInfo.ProcessInfo.Environment?["AURA_IOS_OPENGL"] != null;

        return forceGl
            ? result.With(new iOSPlatformOptions { RenderingMode = [iOSRenderingMode.OpenGl] })
            : result;
    }
}
