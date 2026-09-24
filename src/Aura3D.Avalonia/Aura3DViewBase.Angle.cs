#if ANGLE_HOST
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Aura3D.Avalonia.Angle;
using SkiaSharp;

namespace Aura3D.Avalonia;

/// <summary>
/// iOS 自持 ANGLE（Metal 后端）分支，与 <c>Aura3DViewBase.OpenGl.cs</c> 编译进同一个 iOS 程序集。
/// 两者谁出图由首帧的一次判定决定（见 <see cref="DecideBackendPath"/>）：Avalonia 合成器是 Metal
/// 后端时 <c>OpenGlControlBase</c> 拿不到 GL 互操作、会静默失败，本分支自持 ANGLE 上下文并经
/// Skia lease 合成；宿主 App 显式开了 <c>iOSRenderingMode.OpenGl</c> 时本分支整体退出，
/// 由 <c>OpenGlControlBase</c> 驱动，与桌面端行为一致。
/// 帧调度：判定为 ANGLE 后用渲染优先级定时器驱动
/// （<see cref="Aura3DViewBase.AutoRequestNextFrameRendering"/> 为 false 时只响应手动请求），
/// 每帧的实际绘制发生在 <see cref="Render"/> 提交的自定义绘制操作里（渲染线程）。
/// </summary>
public abstract partial class Aura3DViewBase
{
    private enum BackendPath
    {
        /// <summary>还没读到合成器后端信息，下一帧再判。</summary>
        Undecided,

        /// <summary>Avalonia 合成器为 Metal：本分支自持 ANGLE 上下文出图。</summary>
        Angle,

        /// <summary>Avalonia 合成器为 OpenGL：交回 <c>OpenGlControlBase</c> 出图。</summary>
        OpenGl,
    }

    private AngleGlesSession? _angleSession;
    private AngleLeaseOperation? _leaseOperation;
    private DispatcherTimer? _frameTimer;
    private BackendPath _backendPath;
    private bool _zeroBoundsTraced;

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
    /// iOS 的帧回调运行在合成器渲染线程，与桌面端（回调即 UI 线程）不同；
    /// 场景事件切回 UI 线程触发，页面处理器才能安全读写控件。
    /// </summary>
    partial void DispatchSceneEvent(Action callback)
    {
        if (Dispatcher.UIThread.CheckAccess())
            callback();
        else
            Dispatcher.UIThread.Post(callback, DispatcherPriority.Render);
    }

    /// <summary>GL 上下文就绪即宿主 App 开了 OpenGL 渲染模式，ANGLE 分支退出。</summary>
    partial void OnGlContextReady()
    {
        _backendPath = BackendPath.OpenGl;
        _frameTimer?.Stop();

        global::System.Console.WriteLine(
            "[aura3d-angle] GL context ready, rendering driven by OpenGlControlBase");
    }

    /// <summary>
    /// 模拟一次上下文丢失（测试页用）。GL 模式下只失效句柄、由 Avalonia 重建上下文；
    /// ANGLE 模式下句柄判为失效但不删除，下一帧在同一 ANGLE 上下文上重建。
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
        if (_backendPath != BackendPath.OpenGl && _angleSession is not { IsFailed: true })
        {
            _leaseOperation ??= new AngleLeaseOperation(this);
            context.Custom(_leaseOperation);
        }

        base.Render(context);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 基类会取 Compositor 并请求一次 GL 帧：OpenGL 模式下这条就把 OpenGlControlBase 跑起来。
        base.OnAttachedToVisualTree(e);

        _backendPath = BackendPath.Undecided;
        EnsureFrameTimer();

        RequestNextFrameCore();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 基类 DoCleanup：OpenGL 模式下会真正 OnOpenGlDeinit 并销毁上下文；
        // ANGLE 模式下 GL 从未初始化，基类不做任何事。
        base.OnDetachedFromVisualTree(e);

        _frameTimer?.Stop();

        // 与桌面端 OnOpenGlDeinit 语义一致：分离即销毁上下文，真正删除 GL 对象归还显存。
        // 上下文归属渲染线程，但每帧结束已解除 current，故此处可在 UI 线程接管；
        // Session.Release 与帧渲染互斥，会等待进行中的那一帧。
        // 场景与节点保留，重新挂载后走 ContextRestored 路径在新上下文上重建。
        if (_angleSession is { } session)
        {
            _angleSession = null;

            if (!session.Release(DetachAndReleaseGpu))
                ContextLostCore();
        }

        _backendPath = BackendPath.Undecided;
    }

    /// <summary>
    /// 定时器与 Tick 处理只建一次：重挂载时若再次 += 会让每帧重复请求多帧。
    /// 建好不启动，判定为 ANGLE 后端后才开始驱动。
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
    /// 读一次 Skia lease 的合成器后端来判定归属。拿不到 lease 时保持未判定、下一帧再试：
    /// 判定前不能创建 ANGLE 会话，否则 OpenGL 模式下会与 OpenGlControlBase 抢着出图。
    /// </summary>
    private bool DecideBackendPath(ImmediateDrawingContext drawingContext)
    {
        var feature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
            return false;

        using var lease = feature.Lease();
        if (lease.GrContext is not { } gr)
            return false;

        _backendPath = gr.Backend == GRBackend.Metal ? BackendPath.Angle : BackendPath.OpenGl;

        global::System.Console.WriteLine(
            $"[aura3d-angle] compositor backend={gr.Backend}, path={_backendPath}");

        if (_backendPath == BackendPath.Angle)
            _frameTimer?.Start();

        return true;
    }

    internal void RenderAngleFrame(ImmediateDrawingContext drawingContext)
    {
        if (_backendPath == BackendPath.Undecided && !DecideBackendPath(drawingContext))
            return;

        if (_backendPath != BackendPath.Angle)
            return;

        if (Bounds.Width < 1 || Bounds.Height < 1)
        {
            if (!_zeroBoundsTraced)
            {
                _zeroBoundsTraced = true;
                global::System.Console.WriteLine(
                    $"[aura3d-angle] bounds {Bounds.Width}x{Bounds.Height}, frame skipped");
            }
            return;
        }
        _zeroBoundsTraced = false;

        _angleSession ??= new AngleGlesSession();
        var session = _angleSession;

        try
        {
            var (width, height, _) = ComputePixelSize();

            // 整段（含合成）持锁：合成读的是输出 MTLTexture，须与 UI 线程上的 Release 销毁互斥。
            lock (session.SyncRoot)
            {
                // EnsureOutput 会把上下文 make-current 并把输出 FBO 绑定为当前，
                // 首帧的场景/管线初始化（RenderFrameCore → EnsureScene）依赖该前提。
                if (!session.EnsureOutput(width, height))
                    return;

                RenderFrameCore(AngleGlesSession.GetProcAddress, session.OutputFrameBufferId);

                session.FinishFrame();

                session.Compose(drawingContext, Bounds.Width, Bounds.Height);
            }
        }
        catch (Exception ex)
        {
            session.FailFromOwner(ex.ToString());
        }
    }

    private sealed class AngleLeaseOperation : ICustomDrawOperation
    {
        private readonly Aura3DViewBase _owner;

        public AngleLeaseOperation(Aura3DViewBase owner) => _owner = owner;

        public Rect Bounds => new(0, 0, _owner.Bounds.Width, _owner.Bounds.Height);

        public bool HitTest(Point p) => true;

        public bool Intersects(Rect rect) => true;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context) => _owner.RenderAngleFrame(context);
    }
}
#endif
