using Android.Runtime;
using Aura3D.Gallery;
using Aura3D.Gallery.Fonts;
using Avalonia;
using Avalonia.Android;

namespace Aura3D.Gallery.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            // 资产经 AndroidAsset 打进 APK 的 assets/ 目录，AssetManager 的入参是这层目录之下
            // 的路径，即清单相对路径原样传入（Link 里的 assets\ 前缀本身就是包内那层 assets/）。
            App.HostConfig = new AssetHostConfig(
                IsWeb: false,
                StreamOpener: path => Application.Context.Assets!.Open(path));

            return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .WithAura3DGalleryFonts();
        }
    }
}
