using Aura3D.Gallery.Assets;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using System;
using System.Collections.Generic;

namespace Aura3D.Gallery;

/// <summary>
/// 宿主在启动时告诉共享工程「资产从哪来」。四个平台只有这一处差异，
/// 其余（目录、加载、渲染、参数面板）完全共用同一套代码。
/// </summary>
/// <param name="IsWeb">是否运行在浏览器里。</param>
/// <param name="WebBaseUri">浏览器端资产根 URL，例如 <c>https://host/assets/</c>。</param>
/// <param name="RootOverride">桌面/移动端指定的资产目录；<c>null</c> 表示自动探测。</param>
public sealed record AssetHostConfig(bool IsWeb, Uri? WebBaseUri = null, string? RootOverride = null);

/// <summary>
/// 示例应用入口。主题与外壳样式在 <c>App.axaml</c> 里声明，
/// 这里只做平台接线：资产来源、深链首屏、桌面窗口或单视图。
/// </summary>
public sealed partial class App : Application
{
    /// <summary>
    /// 宿主在 <c>BuildAvaloniaApp</c> 阶段调用，设定资产来源。
    /// </summary>
    public static AssetHostConfig HostConfig { get; set; } = new(IsWeb: false);

    /// <summary>
    /// 宿主传入的启动参数（深链 <c>--page=</c> 等）。
    /// </summary>
    public static IReadOnlyList<string> StartupArgs { get; set; } = Array.Empty<string>();

    /// <summary>当前外壳。</summary>
    public static MainShell? Shell { get; private set; }

    /// <inheritdoc />
    public override void Initialize()
    {
        // Application 的 XAML 由加载器直接读（不像控件那样有 InitializeComponent 生成）。
        global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

        RequestedThemeVariant = ThemeVariant.Default;
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        // 语言要在建外壳之前定下：目录条目和页头文案都在构造时从文案表取值。
        Localization.AppLanguage.ApplyStartup(StartupArgs);

        var shell = new MainShell(CreateAssetProvider(), DeepLink.Resolve(StartupArgs));

        Shell = shell;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(shell);
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = shell;

        base.OnFrameworkInitializationCompleted();
    }

    private static IAssetProvider CreateAssetProvider()
    {
        if (HostConfig.IsWeb)
        {
            var baseUri = HostConfig.WebBaseUri ?? new Uri("assets/", UriKind.Relative);

            return new WebAssetProvider(baseUri, new AssetCache());
        }

        return new FileAssetProvider(HostConfig.RootOverride);
    }
}
