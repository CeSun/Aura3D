using Avalonia.OpenGL;

namespace Aura3D.Avalonia;

/// <summary>
/// Avalonia GL 宿主侧：渲染上下文由 <c>OpenGlControlBase</c> 提供，本文件只做回调转发，
/// 主体流程全部在共享的 <see cref="Aura3DViewBase"/> 中。
/// 桌面/Android/Browser 上这是唯一后端。iOS 上与 <c>Aura3DViewBase.Angle.cs</c> 的自持 ANGLE
/// 后端共存：宿主 App 显式设成 <c>iOSRenderingMode.OpenGl</c> 时走这里；默认的 Metal 合成器下
/// Avalonia 不提供 GL 互操作，<c>OpenGlControlBase</c> 会静默初始化失败，由 ANGLE 后端接管
/// （归属判定见该文件的 <c>BackendPath</c>）。
/// </summary>
public abstract partial class Aura3DViewBase : global::Avalonia.OpenGL.Controls.OpenGlControlBase
{
    protected override void OnOpenGlInit(GlInterface gl)
    {
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
        base.OnOpenGlLost();

        ContextLostCore();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        RenderFrameCore(gl.GetProcAddress, (uint)fb);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        base.OnOpenGlDeinit(gl);

        DetachAndReleaseGpu();
    }

    /// <summary>
    /// GL 上下文真正就绪时调用。桌面端无副作用；iOS 用它作为「宿主 App 开了 OpenGL 渲染模式」的
    /// 权威信号，据此让 ANGLE 后端退出。
    /// </summary>
    partial void OnGlContextReady();

#if !ANGLE_HOST
    partial void RequestNextFrameCore() => base.RequestNextFrameRendering();

    // 桌面端渲染回调与 UI 线程同线程，事件内联触发，行为与拆分前完全一致。
    partial void DispatchSceneEvent(Action callback) => callback();

    /// <summary>
    /// 模拟一次上下文丢失（测试页用）：句柄判为失效但不删除，走与真实丢失相同的恢复路径。
    /// </summary>
    public void SimulateContextLost() => OnOpenGlLost();
#endif
}
