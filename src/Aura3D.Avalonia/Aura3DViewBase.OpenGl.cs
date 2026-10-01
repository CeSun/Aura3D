using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Threading;
using Aura3D.Avalonia.Angle;

namespace Aura3D.Avalonia;

/// <summary>
/// Avalonia GL 宿主侧：渲染上下文由 <c>OpenGlControlBase</c> 提供，本文件只做回调转发，
/// 主体流程全部在共享的 <see cref="Aura3DViewBase"/> 中。
/// 桌面/Android 上这是唯一后端。iOS 上与 <c>Aura3DViewBase.Angle.cs</c> 的自持 ANGLE
/// 后端共存：宿主 App 显式设成 <c>iOSRenderingMode.OpenGl</c> 时走这里；默认的 Metal 合成器下
/// Avalonia 不提供 GL 互操作，<c>OpenGlControlBase</c> 会静默初始化失败，由 ANGLE 后端接管
/// （归属判定见该文件的 <c>BackendPath</c>）。
/// macOS 上宿主设 <see cref="MacAngleBackend.Enabled"/>（形态 B）时走接管门：原生 GL 上下文
/// 本可成功创建，但驱动连硬件点图元都剔除，故意让初始化失败（异常被 OpenGlControlBase 吞掉并
/// 永久判败，与 iOS Metal 合成器下的自然失败同路径），由 <c>Aura3DViewBase.MacAngle.cs</c>
/// 的自持 ANGLE 会话接管。
/// Browser 上 <c>WebGlContext</c> 同样不提供共享上下文/GPU 互操作，本回调永不触发，
/// 由 <c>Aura3DViewBase.WebGl.cs</c> 的自持 WebGL2 分支接管（结构与 iOS ANGLE 分支同构）。
/// </summary>
public abstract partial class Aura3DViewBase : global::Avalonia.OpenGL.Controls.OpenGlControlBase
{
    protected override void OnOpenGlInit(GlInterface gl)
    {
        if (MacAngleBackend.IsActive)
        {
            // 有意抛出：OpenGlControlBase 会吞掉异常并把 GL 初始化永久判为失败，
            // 之后它的帧回调不再触发，3D 视口由 MacAngle 分支全权接管。
            Console.WriteLine("[aura3d-macangle] takeover enabled, native OpenGlControlBase disabled");
            throw new InvalidOperationException("ANGLE takeover enabled on macOS");
        }

        base.OnOpenGlInit(gl);

        OnGlContextReady();

        UpdateRenderSurfaceSize();

        try
        {
            EnsureScene(gl.GetProcAddress);
        }
        catch (Exception e)
        {
            // OpenGlControlBase 会吞掉本回调的异常并把初始化永久判为失败（画面全白且无日志），
            // 必须在这里自行留痕。
            global::System.Console.WriteLine("[aura3d-gl] OnOpenGlInit failed: " + e);
            throw;
        }
    }

    protected override void OnOpenGlLost()
    {
        if (MacAngleBackend.IsActive)
            return;

        base.OnOpenGlLost();

        ContextLostCore();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (MacAngleBackend.IsActive)
            return;

        RenderFrameCore(gl.GetProcAddress, (uint)fb);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (MacAngleBackend.IsActive)
            return;

        base.OnOpenGlDeinit(gl);

        DetachAndReleaseGpu();
    }

    /// <summary>
    /// GL 上下文真正就绪时调用。桌面端无副作用；iOS 用它作为「宿主 App 开了 OpenGL 渲染模式」的
    /// 权威信号，据此让 ANGLE 后端退出。
    /// </summary>
    partial void OnGlContextReady();

#if !ANGLE_HOST && !WEBGL_HOST
    partial void RequestNextFrameCore()
    {
        if (MacAngleBackend.IsActive)
        {
            // 接管状态下 OpenGlControlBase 的帧请求已失效（初始化判败），改走合成帧请求。
            if (Dispatcher.UIThread.CheckAccess())
                ((Visual)this).InvalidateVisual();
            else
                Dispatcher.UIThread.Post(() => ((Visual)this).InvalidateVisual(), DispatcherPriority.Render);
            return;
        }

        base.RequestNextFrameRendering();
    }

    // 桌面端原生路径的渲染回调与 UI 线程同线程，事件内联触发，行为与拆分前完全一致；
    // macOS 接管（形态 B）下渲染发生在合成器渲染线程（同 iOS），场景事件必须切回 UI 线程。
    partial void DispatchSceneEvent(Action callback)
    {
        if (MacAngleBackend.IsActive)
        {
            if (Dispatcher.UIThread.CheckAccess())
                callback();
            else
                Dispatcher.UIThread.Post(callback, DispatcherPriority.Render);
            return;
        }

        callback();
    }

    /// <summary>
    /// 模拟一次上下文丢失（测试页用）：句柄判为失效但不删除，走与真实丢失相同的恢复路径。
    /// </summary>
    public void SimulateContextLost()
    {
        if (MacAngleBackend.IsActive)
        {
            ContextLostCore();
            return;
        }

        OnOpenGlLost();
    }
#endif
}
