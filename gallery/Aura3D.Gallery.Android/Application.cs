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
            return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .WithAura3DGalleryFonts();
        }
    }
}
