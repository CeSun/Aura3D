using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aura3D.Examples;
using Aura3D.Examples.Fonts;
using Avalonia;
using Avalonia.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App.StartupArgs = args;

        // wwwroot/main.js 把 globalThis.location.href 作为唯一参数传进来，资产根就从它推导；
        // 这样 Example 与任何外部浏览器宿主一样，不需要在 csproj 里配资产路径。
        App.HostConfig = new AssetHostConfig(IsWeb: true, AssetBaseUri(args));

        return BuildAvaloniaApp()
            .WithInterFont()
            .WithAura3DExampleFonts()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();

    private static Uri AssetBaseUri(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (Uri.TryCreate(arg, UriKind.Absolute, out var page) &&
                (page.Scheme == Uri.UriSchemeHttp || page.Scheme == Uri.UriSchemeHttps))
            {
                return new Uri(page, "assets/");
            }
        }

        throw new InvalidOperationException(
            "Browser host received no page URL: main.js must boot with runMain(name, [globalThis.location.href]); the asset root URL is derived from it.");
    }
}
