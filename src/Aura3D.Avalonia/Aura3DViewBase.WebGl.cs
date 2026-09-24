#if WEBGL_HOST
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aura3D.Avalonia.WebGL;
using SkiaSharp;

namespace Aura3D.Avalonia;

/// <summary>
/// Browser（WebGL2）自持分支，与 <c>Aura3DViewBase.OpenGl.cs</c> 编译进同一个 browser 程序集。
/// Avalonia.Browser 的 <c>WebGlContext</c> 不支持共享上下文与 GPU 互操作
/// （<c>CanCreateSharedContext=false</c>、无 <c>ICompositionGpuInterop</c> 特性），
/// <c>OpenGlControlBase</c> 在浏览器上永远初始化失败；本分支不建自己的上下文，而是借
/// 合成器已持有的 WebGL2 上下文：帧驱动用渲染优先级定时器 + <see cref="Render"/> 提交的
/// 自定义绘制操作（运行在渲染线程、GL 上下文 current），管线经
/// <see cref="WebGlGlesSession.GetProcAddress"/>（emscripten GLES shim → WebGL2）出图，
/// 输出纹理经 Skia lease 零拷贝合成。归属判定与 iOS ANGLE 分支同构（见 <see cref="DecideBackendPath"/>）。
/// </summary>
public abstract partial class Aura3DViewBase
{
    private enum BackendPath
    {
        /// <summary>还没读到合成器后端信息，下一帧再判。</summary>
        Undecided,

        /// <summary>Avalonia 合成器为 GL（WebGL2）：本分支借其上下文出图。</summary>
        WebGl,

        /// <summary>OpenGlControlBase 拿到了 GL 互操作（未来 Avalonia 支持时）：交回其出图。</summary>
        OpenGl,
    }

    private WebGlGlesSession? _webGlSession;
    private WebGlLeaseOperation? _leaseOperation;
    private DispatcherTimer? _frameTimer;
    private BackendPath _backendPath;
    private bool _zeroBoundsTraced;
    private Compositor? _compositor;

    partial void RequestNextFrameCore()
    {
        if (_backendPath == BackendPath.OpenGl)
        {
            base.RequestNextFrameRendering();
            return;
        }

        // RenderFrameCore 可能在渲染线程回调链上请求下一帧，切回 UI 线程调度。
        // 注意 OpenGlControlBase 用 new 遮蔽了 InvalidateVisual，把它转给 GL 调度；
        // GL 初始化失败后那个方法永久空转，故这里必须显式按 Visual 调用。
        if (Dispatcher.UIThread.CheckAccess())
            ((Visual)this).InvalidateVisual();
        else
            Dispatcher.UIThread.Post(() => ((Visual)this).InvalidateVisual(), DispatcherPriority.Render);
    }

    /// <summary>
    /// Browser 的帧回调运行在合成器渲染线程（wasm worker），与桌面端（回调即 UI 线程）不同；
    /// 场景事件切回 UI 线程触发，页面处理器才能安全读写控件。
    /// </summary>
    partial void DispatchSceneEvent(Action callback)
    {
        if (Dispatcher.UIThread.CheckAccess())
            callback();
        else
            Dispatcher.UIThread.Post(callback, DispatcherPriority.Render);
    }

    /// <summary>GL 上下文就绪即 OpenGlControlBase 拿到了互操作，WebGL 分支退出。</summary>
    partial void OnGlContextReady()
    {
        _backendPath = BackendPath.OpenGl;
        _frameTimer?.Stop();

        global::System.Console.WriteLine(
            "[aura3d-webgl] GL context ready, rendering driven by OpenGlControlBase");
    }

    /// <summary>
    /// 模拟一次上下文丢失（测试页用）。GL 模式下只失效句柄、由 Avalonia 重建上下文；
    /// WebGL 模式下句柄判为失效但不删除，下一帧在同一 WebGL2 上下文上重建。
    /// </summary>
    public void SimulateContextLost()
    {
        if (_backendPath == BackendPath.OpenGl)
            OnOpenGlLost();
        else
            ContextLostCore();
    }

    public override void Render(DrawingContext context)
    {
        if (_backendPath != BackendPath.OpenGl && _webGlSession is not { IsFailed: true })
        {
            _leaseOperation ??= new WebGlLeaseOperation(this);
            context.Custom(_leaseOperation);
        }

        base.Render(context);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 基类会取 Compositor 并请求一次 GL 帧：浏览器上 OpenGlControlBase 会静默初始化失败。
        base.OnAttachedToVisualTree(e);

        _compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        _backendPath = BackendPath.Undecided;
        EnsureFrameTimer();

        // 与 iOS 分支不同：定时器立即启动而非判定后启动。浏览器上首帧自定义绘制操作
        // 可能因布局未完成（Bounds 为 0）被合成器裁剪掉，判定发生在绘制操作内，
        // 若等判定成功再启动定时器会互相等待。判定为 OpenGl 路径时定时器会停掉。
        _frameTimer?.Start();

        RequestNextFrameCore();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 基类 DoCleanup：浏览器上 GL 从未初始化，基类不做任何事。
        base.OnDetachedFromVisualTree(e);

        _frameTimer?.Stop();

        // 与桌面端 OnOpenGlDeinit 语义一致：分离即归还显存。但 WebGL2 上下文归合成器所有、
        // 线程亲和于渲染 worker，UI 线程不能直接发 GL 调用——把释放投递为合成器更新任务
        // 在渲染线程执行（与 OpenGlControlBase 内部用法一致）。拿不到 Compositor 时退化为
        // 仅失效句柄（GL 对象随上下文存活，重新挂载后按 ContextRestored 路径重建，
        // 不再引用旧句柄）。场景与节点保留。
        if (_webGlSession is { } session)
        {
            _webGlSession = null;

            if (_compositor is { } compositor)
            {
                try
                {
                    compositor.RequestCompositionUpdate(() =>
                    {
                        if (!session.ReleaseOnRenderThread(DetachAndReleaseGpu))
                            ContextLostCore();
                    });
                }
                catch
                {
                    // 合成器已停（应用关闭等）：只失效句柄，不发 GL 调用。
                    ContextLostCore();
                }
            }
            else
            {
                ContextLostCore();
            }
        }

        _compositor = null;
        _backendPath = BackendPath.Undecided;
    }

    /// <summary>
    /// 定时器与 Tick 处理只建一次：重挂载时若再次 += 会让每帧重复请求多帧。
    /// 建好不启动，判定为 WebGL 后端后才开始驱动。
    /// </summary>
    private void EnsureFrameTimer()
    {
        if (_frameTimer is not null)
            return;

        _frameTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _frameTimer.Tick += (_, _) =>
        {
            if (AutoRequestNextFrameRendering)
                RequestNextFrameCore();
        };
    }

    /// <summary>
    /// 读一次 Skia lease 的合成器后端来判定归属。拿不到 lease（软件合成回退）时保持未判定、
    /// 下一帧再试：判定前不能创建会话。浏览器上 GL 后端即 WebGL2 承载的 Skia。
    /// </summary>
    private bool DecideBackendPath(ImmediateDrawingContext drawingContext)
    {
        var feature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
            return false;

        using var lease = feature.Lease();
        if (lease.GrContext is not { } gr)
            return false;

        _backendPath = gr.Backend == GRBackend.OpenGL ? BackendPath.WebGl : BackendPath.OpenGl;

        global::System.Console.WriteLine(
            $"[aura3d-webgl] compositor backend={gr.Backend}, path={_backendPath}");

        if (_backendPath == BackendPath.WebGl)
            _frameTimer?.Start();

        return true;
    }

    internal void RenderWebGlFrame(ImmediateDrawingContext drawingContext)
    {
        if (_backendPath == BackendPath.Undecided && !DecideBackendPath(drawingContext))
            return;

        if (_backendPath != BackendPath.WebGl)
            return;

        if (Bounds.Width < 1 || Bounds.Height < 1)
        {
            if (!_zeroBoundsTraced)
            {
                _zeroBoundsTraced = true;
                global::System.Console.WriteLine(
                    $"[aura3d-webgl] bounds {Bounds.Width}x{Bounds.Height}, frame skipped");
            }
            return;
        }
        _zeroBoundsTraced = false;

        _webGlSession ??= new WebGlGlesSession();
        var session = _webGlSession;

        try
        {
            var (width, height, _) = ComputePixelSize();

            // 与桌面端 OnOpenGlInit 同样的顺序：先把输出尺寸落到 RenderSurface，再创建场景。
            // RenderFrameCore 内部是先 EnsureScene 后 UpdateRenderSurfaceSize，若这里不预置，
            // 首帧会以默认尺寸创建管线（尺寸同步要到第二帧才生效）。
            UpdateRenderSurfaceSize();

            // 绘制操作与 Compositor 服务器任务同在渲染线程串行执行，无需 iOS 那样的跨线程锁。
            if (!session.EnsureOutput(width, height))
                return;

            RenderFrameCore(WebGlGlesSession.GetProcAddress, session.OutputFrameBufferId);

            session.FinishFrame();

            session.Compose(drawingContext, Bounds.Width, Bounds.Height);
        }
        catch (Exception ex)
        {
            session.FailFromOwner(ex.ToString());
        }
    }

    private sealed class WebGlLeaseOperation : ICustomDrawOperation
    {
        private readonly Aura3DViewBase _owner;

        public WebGlLeaseOperation(Aura3DViewBase owner) => _owner = owner;

        public Rect Bounds => new(0, 0, _owner.Bounds.Width, _owner.Bounds.Height);

        public bool HitTest(Point p) => true;

        public bool Intersects(Rect rect) => true;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context) => _owner.RenderWebGlFrame(context);
    }
}
#endif
