#if ANGLE_HOST
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using Aura3D.Avalonia.Angle;

namespace Aura3D.Avalonia;

/// <summary>
/// iOS 后端：不依赖 Avalonia 的 GL 互操作，控件自持 ANGLE（Metal 后端）上下文渲染，
/// 输出纹理经 Skia lease 合成上屏。帧调度：附着后用渲染优先级定时器驱动
/// （<see cref="Aura3DViewBase.AutoRequestNextFrameRendering"/> 为 false 时只响应手动请求），
/// 每帧的实际绘制发生在 <see cref="Render"/> 提交的自定义绘制操作里（渲染线程），
/// 与桌面端 OpenGlControlBase 的回调线程语义一致。
/// </summary>
public abstract partial class Aura3DViewBase : Control
{
    private AngleGlesSession? _angleSession;
    private AngleLeaseOperation? _leaseOperation;
    private DispatcherTimer? _frameTimer;
    private bool _zeroBoundsTraced;

    partial void RequestNextFrameCore()
    {
        // RenderFrameCore 可能在渲染线程回调链上请求下一帧，切回 UI 线程调度。
        if (Dispatcher.UIThread.CheckAccess())
            InvalidateVisual();
        else
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
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

    /// <summary>
    /// 模拟一次上下文丢失（测试页用）：句柄判为失效但不删除，下一帧在同一 ANGLE 上下文上重建。
    /// </summary>
    public void SimulateContextLost() => ContextLostCore();

    public override void Render(DrawingContext context)
    {
        if (_angleSession is not { IsFailed: true })
        {
            _leaseOperation ??= new AngleLeaseOperation(this);
            context.Custom(_leaseOperation);
        }

        base.Render(context);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 定时器与 Tick 处理只建一次：重挂载时若再次 += 会让每帧重复请求多帧。
        if (_frameTimer is null)
        {
            _frameTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16),
            };
            _frameTimer.Tick += (_, _) =>
            {
                if (AutoRequestNextFrameRendering)
                    InvalidateVisual();
            };
        }
        _frameTimer.Start();

        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
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
    }

    internal void RenderAngleFrame(ImmediateDrawingContext drawingContext)
    {
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
