using Aura3D.Examples;
using Foundation;
using UIKit;

namespace Example.iOS;

public class Application
{
    private static void Main(string[] args)
    {
        // 验证辅助：AURA3D_DEMO_PAGE=<功能页标题片段> 让共享外壳直接进那一页，
        // 模拟器逐页截图回归不必靠像素点击导航。
        var page = NSProcessInfo.ProcessInfo.Environment?[DeepLink.EnvironmentVariable]?.ToString();

        App.StartupArgs = string.IsNullOrEmpty(page) ? args : [$"--page={page}"];

        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
