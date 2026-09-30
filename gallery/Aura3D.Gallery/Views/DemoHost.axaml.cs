using Aura3D.Gallery.Assets;
using Aura3D.Gallery.Localization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aura3D.Gallery;

/// <summary>
/// 管线下拉框的一项：<see cref="PipelineKind"/> 加显示名。枚举本身没法在 XAML 里绑定显示名，
/// 所以宿主提供一个可绑的小包装。标签每次读都取当前语言，切语言时由宿主重挂条目刷新。
/// </summary>
public sealed record PipelineOption(PipelineKind Kind)
{
    /// <summary>下拉框里显示的管线名。</summary>
    public string Label => Kind.DisplayName();
}

/// <summary>
/// 单个功能页的宿主：按固定生命周期装配演示页控件、显示资产加载进度、
/// 并在切换页面或切换管线时干净地拆掉重来。演示页本身不需要写任何导航样板。
/// </summary>
public sealed partial class DemoHost : UserControl
{
    private List<PipelineOption> pipelineOptions = BuildPipelineOptions();

    private static List<PipelineOption> BuildPipelineOptions() =>
        [.. PipelineCatalog.All.Select(kind => new PipelineOption(kind))];

    private DemoDescriptor? descriptor;
    private Demo? demo;
    private DemoContext? context;

    private bool suppressPipelineEvent;

    /// <summary>
    /// 创建宿主。XAML 里的 <c>&lt;ex:DemoHost/&gt;</c> 只能走无参构造，
    /// 因此资产提供器由外壳在建好之后注入，LoadAsync 之前必须给。
    /// </summary>
    public DemoHost()
    {
        InitializeComponent();

        PipelineBox.ItemsSource = pipelineOptions;
    }

    /// <summary>资产提供器，全部功能页共用。</summary>
    public IAssetProvider Assets { get; set; } = null!;

    /// <summary>当前功能页标识，供深链同步与状态显示读取。</summary>
    public string? CurrentId => descriptor?.Id;

    /// <summary>当前页的场景是否已经组装完成，逐页验证脚本用这个判断「页面活了」。</summary>
    public bool SceneBuilt => demo?.SceneBuilt == true;

    /// <summary>
    /// 进入一个功能页：先拆掉上一页，再走「建页 → 接视图 → 取资产 → 建场景」。
    /// </summary>
    /// <param name="target">功能页描述。</param>
    /// <param name="pipeline">覆盖描述里声明的默认管线。</param>
    public async Task LoadAsync(DemoDescriptor target, PipelineKind? pipeline = null)
    {
        Teardown();

        descriptor = target;

        context = new DemoContext(
            Assets,
            pipeline ?? target.DefaultPipeline,
            kind => _ = LoadAsync(target, kind))
        {
            AllowPipelineSwitching = !target.LockPipeline,
        };

        demo = target.Create(context);
        demo.Faulted += PostError;

        ShowProgress(Strings.Keys.Status_Prepare.Format(target.Title.T()));

        try
        {
            // 管线要在页面进视觉树之前绑：视图的首帧就按当时的工厂定死管线，而浏览器上进树后
            // 第一帧远早于资产下载完成，等到 Attach 再改已经没人读了。
            demo.BindView();

            PageSlot.Content = demo;

            var kind = context.RequestedPipeline;

            suppressPipelineEvent = true;
            // 下拉项的标签是绑出来的：换语言后要把 ItemsSource 重挂一遍，控件才会重新取 Label。
            pipelineOptions = BuildPipelineOptions();
            PipelineBox.ItemsSource = pipelineOptions;
            PipelineBox.SelectedItem = pipelineOptions.FirstOrDefault(o => o.Kind == kind);
            PipelineBox.IsVisible = context.AllowPipelineSwitching;
            suppressPipelineEvent = false;

            var batch = new AssetBatch(Assets, target.Assets);

            batch.Progress += p => Dispatcher.UIThread.Post(() =>
            {
                Progress.Value = p.TotalBytes > 0 ? Math.Clamp((double)p.DownloadedBytes / p.TotalBytes, 0, 1) : 0;

                ProgressText.Text = p.TotalAssets <= 1
                    ? Strings.Keys.Status_Fetch.Format(p.Key, p.DownloadedBytes / 1024)
                    : Strings.Keys.Status_FetchProgress.Format(
                        p.Key, p.DownloadedBytes / 1024, p.TotalBytes / 1024, p.CompletedAssets, p.TotalAssets);
            });

            await demo.LoadAssetsAsync(batch);

            // Attach 挂的是首帧回调：BuildScene 落在视图的第一次更新回调上，
            // 资产没就绪就订阅会让页面拿还没取到的模型与贴图去建场景，所以排在下载之后。
            // 管线的装配不在这里，见上面的 BindView。
            demo.Attach();

            HideProgress();

            StatusText.Text =
                $"{target.Group.T()} · {target.Title.T()} · {target.SizeLabel} · {kind.DisplayName()}";
        }
        catch (Exception e)
        {
            HideProgress();
            PostError(Strings.Keys.Error_InitFailed.Format(target.Title.T(), e.Message));

            global::System.Console.WriteLine($"[aura3d-gallery] {target.Id} init failed {e}");
        }
    }

    private void OnPipelineChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressPipelineEvent || context == null || descriptor == null)
            return;

        if (PipelineBox.SelectedItem is PipelineOption option && option.Kind != context.RequestedPipeline)
            _ = LoadAsync(descriptor, option.Kind);
    }

    private void PostError(string message) => Dispatcher.UIThread.Post(() =>
    {
        ErrorText.Text = message;
        ErrorOverlay.IsVisible = true;
    });

    private void Teardown()
    {
        if (demo != null)
        {
            demo.Faulted -= PostError;
            demo.Detach();
            demo.Unload();
        }

        context?.DisposePerViewResources();

        PageSlot.Content = null;
        demo = null;
        context = null;

        ProgressOverlay.IsVisible = false;
        ErrorOverlay.IsVisible = false;
        StatusText.Text = string.Empty;
    }

    private void ShowProgress(string text)
    {
        Progress.Value = 0;
        ProgressText.Text = text;
        ProgressOverlay.IsVisible = true;
    }

    private void HideProgress() => ProgressOverlay.IsVisible = false;
}
