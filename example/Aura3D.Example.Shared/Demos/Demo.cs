using Aura3D.Avalonia;
using Aura3D.Examples.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Irihi.Lingua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aura3D.Examples;

/// <summary>
/// 一个功能演示的静态描述：标题、分组、所需资产与建页工厂。宿主只认这张表，
/// 因此新增一页只需要在 <see cref="DemoRegistry.All"/> 里登记一项，不需要改导航、深链或宿主代码。
/// 标题/分组/说明存的是文案键而不是成品文本：显示点每次读都按当前语言取值，
/// 切语言时不需要重建这张表。
/// </summary>
/// <param name="Id">深链用的稳定标识（<c>?page=&lt;Id&gt;</c>），改名会破坏既有链接。</param>
/// <param name="Title">导航里显示的标题（文案键）。</param>
/// <param name="Group">导航分组名（文案键）。</param>
/// <param name="Summary">一句话说明这一页演示什么，显示在页头（文案键）。</param>
/// <param name="Assets">该页所需资产集合。</param>
/// <param name="Create">建页工厂。</param>
/// <param name="DefaultPipeline">进入该页时使用的管线。</param>
/// <param name="LockPipeline">页面上不出现管线切换。演示页如果依赖某条管线的专属特性
/// （点云、卡通材质扩展、只覆盖某个 pass 的自定义着色器），锁死管线才不会切出误导性的黑屏。</param>
/// <param name="DesktopOnly">浏览器端不出现该页（依赖 Assimp 原生库或超大资产）。</param>
public sealed record DemoDescriptor(
    string Id,
    LinguaKey Title,
    LinguaKey Group,
    LinguaKey Summary,
    Assets.AssetSet Assets,
    Func<DemoContext, Demo> Create,
    PipelineKind DefaultPipeline = PipelineKind.BlinnPhong,
    bool LockPipeline = false,
    bool DesktopOnly = false)
{
    /// <summary>所需字节数合计。</summary>
    public long TotalBytes => Assets.TotalBytes;

    /// <summary>
    /// 人类可读的体积描述，供导航条目显示，让浏览器的使用者在点开重页前知道要下载多少。
    /// 取值走文案表，所以每次读都是当前语言的那份。
    /// </summary>
    public string SizeLabel => Assets.Items.Count switch
    {
        0 => Strings.Keys.Size_None.T(),
        1 => Strings.Keys.Size_ItemsOne.Format(TotalBytes / 1024.0 / 1024.0),
        _ => Strings.Keys.Size_Items.Format(Assets.Items.Count, TotalBytes / 1024.0 / 1024.0),
    };
}

/// <summary>
/// 功能演示的基类，本身就是一个 Avalonia 控件：每一页都是一个 AXAML 用户控件，
/// 视口（<c>kit:DemoView</c>）与检视面板（<c>kit:InspectorPanel</c>）写在页面自己的 XAML 里，
/// 参数行通过绑定或 XAML 事件处理接到下面这些生命周期上。
/// <list type="number">
///   <item><description>构造 + <c>InitializeComponent</c>，纯 UI 线程，不碰 GPU；</description></item>
///   <item><description><see cref="LoadAssetsAsync"/>，可等待的资产取用与解码，进度上报给宿主遮罩；</description></item>
///   <item><description><see cref="BuildScene"/>，在渲染线程的首帧回调里同步组装场景；</description></item>
///   <item><description>逐帧 <see cref="Update"/>；离开时 <see cref="Unload"/>。</description></item>
/// </list>
/// 之所以把资源准备放在渲染线程之外：浏览器端下载是异步的，而场景初始化回调跑在渲染线程上，
/// 在里面等网络会把合成器整个卡死。
/// </summary>
public abstract class Demo : UserControl
{
    private bool sceneBuilt;

    /// <summary>宿主环境与视图/场景入口。</summary>
    protected DemoContext Context { get; }

    protected Demo(DemoContext context)
    {
        Context = context;

        // 页面 XAML 里的 {Binding} 直接读写演示页实例，所以每一页的 DataContext 都是自己。
        // 这里必须是显式赋值而不是改默认值：外壳也带自己的 DataContext，而继承值优先于默认值。
        DataContext = this;
    }

    /// <summary>
    /// 演示页出错的回调（场景构建失败等）。宿主订阅后显示错误条。
    /// </summary>
    internal event Action<string>? Faulted;

    /// <summary>
    /// 把页面声明的视图接到宿主环境上：找到 <see cref="Aura3DView"/>、按当前要求的管线
    /// 装配、挂上首帧回调。XAML 里没放视图就是配置错误，直接抛出来让页面显示错误条。
    /// </summary>
    internal void Attach()
    {
        var view = this.GetLogicalDescendants().OfType<Aura3DView>().FirstOrDefault();

        if (view == null)
            throw new InvalidOperationException(Strings.Keys.Error_NoDemoView.Format(GetType().Name));

        Context.AttachView(view);

        view.SceneUpdated += OnSceneUpdated;
        view.ContextRestored += (_, _) => ContextRestored();

        ViewAttached(view);
    }

    /// <summary>
    /// 取用并解码该页所需资产。实现里一律用 <paramref name="assets"/> 上的 helper，
    /// 它们负责进度上报，并按 Key 复用同一份已下载字节与已解码结果。
    /// </summary>
    public virtual Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <summary>
    /// 组装场景。此时 <see cref="DemoContext.Scene"/> 已存在，本方法跑在渲染线程回调上：
    /// 只做 CPU 侧的节点/几何/材质声明，不要直接调 GL，也不要在这里等网络或解大图。
    /// </summary>
    public abstract void BuildScene();

    /// <summary>
    /// 视图已接上宿主。用于订阅 <c>ObjectPicked</c> 一类视图事件，
    /// 以及按需调整 <c>AutoRequestNextFrameRendering</c>。
    /// </summary>
    public virtual void ViewAttached(Aura3DView view)
    {
    }

    /// <summary>
    /// 逐帧更新。<paramref name="deltaTime"/> 为距上一帧的秒数。
    /// </summary>
    public virtual void Update(double deltaTime)
    {
    }

    /// <summary>
    /// 上下文丢失后重新就绪：GPU 句柄已换新，场景与节点仍是原实例，
    /// 演示页只需要恢复自己缓存了 GL 对象引用的那部分。
    /// </summary>
    public virtual void ContextRestored()
    {
    }

    /// <summary>
    /// 离开该页。宿主负责归还显存与销毁视图，这里只解除订阅与计时器，不要碰 GL。
    /// </summary>
    public virtual void Unload()
    {
    }

    /// <summary>当前页的场景是否已经组装完成，逐页验证脚本用它判断「页面活了」。</summary>
    internal bool SceneBuilt => sceneBuilt;

    internal void Detach()
    {
        if (Context.View != null)
            Context.View.SceneUpdated -= OnSceneUpdated;
    }

    private void OnSceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        // 场景组装只能落在这个渲染线程回调上：Scene 要到首次回调才存在，
        // 而它一旦建出来，同一回调里声明几何与纹理就是安全的。
        if (!sceneBuilt)
        {
            sceneBuilt = true;

            try
            {
                BuildScene();
            }
            catch (Exception exception)
            {
                Faulted?.Invoke(Strings.Keys.Error_SceneBuildFailed.Format(exception.Message));

                global::System.Console.WriteLine(
                    $"[aura3d-example] scene build failed {exception}");

                return;
            }
        }

        Update(e.DeltaTime);
    }
}
