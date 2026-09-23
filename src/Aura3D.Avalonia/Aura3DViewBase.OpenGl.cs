#if !ANGLE_HOST
using Avalonia.OpenGL;

namespace Aura3D.Avalonia;

/// <summary>
/// 桌面/Android/Browser 后端：渲染上下文由 Avalonia 的 <c>OpenGlControlBase</c> 提供，
/// 本文件只做回调转发，主体流程全部在共享的 <see cref="Aura3DViewBase"/> 中。
/// </summary>
public abstract partial class Aura3DViewBase : global::Avalonia.OpenGL.Controls.OpenGlControlBase
{
    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        UpdateRenderSurfaceSize();

        EnsureScene(gl.GetProcAddress);
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

    partial void RequestNextFrameCore() => base.RequestNextFrameRendering();

    // 桌面端渲染回调与 UI 线程同线程，事件内联触发，行为与拆分前完全一致。
    partial void DispatchSceneEvent(Action callback) => callback();

    /// <summary>
    /// 模拟一次上下文丢失（测试页用）：句柄判为失效但不删除，走与真实丢失相同的恢复路径。
    /// </summary>
    public void SimulateContextLost() => OnOpenGlLost();
}
#endif
