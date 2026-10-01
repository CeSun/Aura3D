using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Aura3D.Avalonia.Angle;

#pragma warning disable AVA1700 // ISkiaSharpApiLeaseFeature 为 Avalonia 标注的 Unstable API

namespace Aura3D.Avalonia;

/// <summary>
/// 形态 B 的 macOS 分支：宿主设 <see cref="MacAngleBackend.Enabled"/> 后，3D 视口不再由
/// <c>OpenGlControlBase</c> 驱动（其原生上下文在 OnOpenGlInit 里被有意判败，见
/// <c>Aura3DViewBase.OpenGl.cs</c> 的接管门），改由自持 ANGLE(Metal) 会话出图。
/// 帧调度与合成结构和 iOS 的 <c>Aura3DViewBase.Angle.cs</c> 同构：渲染优先级定时器请求合成帧，
/// <see cref="Render"/> 提交的自定义绘制操作在合成阶段租借 Skia 上下文完成「渲一帧 + 合成」。
/// 与 iOS 的唯一差别在会话内部：GL 合成器下输出 IOSurface 经 CGL 导入（见
/// <c>MacAngleGlesSession.Compose</c>），而非 MTLTexture 直通。
/// </summary>
public abstract partial class Aura3DViewBase
{
    private MacAngleGlesSession? _macAngleSession;
    private MacAngleLeaseOperation? _macLeaseOperation;
    private DispatcherTimer? _macFrameTimer;
    private bool _macZeroBoundsTraced;

    /// <summary>
    /// 定时器与 Tick 处理只建一次：重挂载时若再次 += 会让每帧重复请求多帧。
    /// 建好不启动，接管成立后才开始驱动。
    /// </summary>
    private void EnsureMacFrameTimer()
    {
        if (_macFrameTimer is not null)
            return;

        _macFrameTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _macFrameTimer.Tick += (_, _) =>
        {
            if (AutoRequestNextFrameRendering)
                RequestNextFrameMac();
        };
    }

    /// <summary>
    /// 接管状态下用 InvalidateVisual 请求合成帧：OpenGlControlBase 的帧请求在初始化判败后失效，
    /// 不能复用（与 iOS 的 <c>RequestNextFrameCore</c> 同理）。渲染线程回调链上请求时切回 UI 线程。
    /// </summary>
    private void RequestNextFrameMac()
    {
        if (Dispatcher.UIThread.CheckAccess())
            ((Visual)this).InvalidateVisual();
        else
            Dispatcher.UIThread.Post(() => ((Visual)this).InvalidateVisual(), DispatcherPriority.Render);
    }

    public override void Render(DrawingContext context)
    {
        if (MacAngleBackend.IsActive)
        {
            _macLeaseOperation ??= new MacAngleLeaseOperation(this);
            context.Custom(_macLeaseOperation);
        }

        base.Render(context);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!MacAngleBackend.IsActive)
            return;

        EnsureMacFrameTimer();
        RequestNextFrameMac();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _macFrameTimer?.Stop();

        if (_macAngleSession is not { } session)
            return;

        _macAngleSession = null;

        // 与帧渲染互斥，会等待进行中的那一帧；分离即销毁上下文，语义与桌面端 OnOpenGlDeinit 一致。
        if (!session.Release(DetachAndReleaseGpu))
            ContextLostCore();
    }

    internal void RenderMacAngleFrame(ImmediateDrawingContext drawingContext)
    {
        if (Bounds.Width < 1 || Bounds.Height < 1)
        {
            if (!_macZeroBoundsTraced)
            {
                _macZeroBoundsTraced = true;
                Console.WriteLine($"[aura3d-macangle] bounds {Bounds.Width}x{Bounds.Height}, frame skipped");
            }
            return;
        }
        _macZeroBoundsTraced = false;

        _macAngleSession ??= new MacAngleGlesSession();
        var session = _macAngleSession;

        try
        {
            var (width, height, _) = ComputePixelSize();

            lock (session.SyncRoot)
            {
                if (!session.EnsureOutput((uint)width, (uint)height))
                    return;

                RenderFrameCore(MacAngleGlesSession.GetProcAddress, session.OutputFrameBufferId);

                session.FinishFrame();

                session.Compose(drawingContext, Bounds.Width, Bounds.Height);
            }
        }
        catch (Exception ex)
        {
            session.FailFromOwner(ex.ToString());
        }
    }

    private sealed class MacAngleLeaseOperation : ICustomDrawOperation
    {
        private readonly Aura3DViewBase _owner;

        public MacAngleLeaseOperation(Aura3DViewBase owner) => _owner = owner;

        public Rect Bounds => new(0, 0, _owner.Bounds.Width, _owner.Bounds.Height);

        public bool HitTest(Point p) => true;

        public bool Intersects(Rect rect) => true;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context) => _owner.RenderMacAngleFrame(context);
    }
}
