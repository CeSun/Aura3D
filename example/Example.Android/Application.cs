using Android.Runtime;
using Aura3D.Examples;
using Aura3D.Examples.Fonts;
using Avalonia;
using Avalonia.Android;

namespace Example.Android
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
            .WithAura3DExampleFonts();
        }
    }
}
