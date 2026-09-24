using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Rendering;
using System.Diagnostics;
using Aura3D.Core.Resources;
using Aura3D.Core.Renderers;
using Aura3D.Core;
using Avalonia.VisualTree;

namespace Aura3D.Avalonia;

/// <summary>
/// Avalonia 3D 渲染控件的基类，负责管理场景生命周期、渲染循环以及节点操作。
/// 主体流程与平台无关：场景创建、管线初始化、逐帧 Update/Render 与 GPU 资源释放/重建均在
/// 本文件中实现；平台差异只存在于两个后端 partial：
/// 桌面/Android 走 <c>Aura3DViewBase.OpenGl.cs</c>（Avalonia OpenGlControlBase），
/// iOS 走 <c>Aura3DViewBase.Angle.cs</c>（自持 ANGLE Metal 上下文 + Skia lease 合成）。
/// 应用侧无论哪个平台都以相同签名引用 <see cref="Aura3DView"/>。
/// </summary>
public abstract partial class Aura3DViewBase : ICustomHitTest
{
    /// <summary>
    /// 获取或设置当前关联的 3D 场景。控件从未初始化、或调用了 <see cref="DestroyScene"/> 之后为 <c>null</c>。
    /// 控件从视觉树分离不会清空该属性。
    /// </summary>
    public Scene? Scene { get; protected set; }

    /// <summary>
    /// 获取当前 GPU 句柄是否处于失效状态且尚未重建。上下文丢失或控件从视觉树分离后为 <c>true</c>；
    /// 两种情况下场景、节点与 CPU 资源均保持不变，GPU 对象会在下次渲染时按需重建。
    /// </summary>
    public bool IsContextLost { get; private set; }

    Stopwatch Stopwatch;

    int fb = 0;

    /// <summary>
    /// 每帧渲染结束后是否自动请求下一帧。设为 <c>false</c> 后需手动调用
    /// <see cref="RequestNextFrameRendering"/> 驱动渲染。
    /// </summary>
    public bool AutoRequestNextFrameRendering { get; set; } = true;

    protected bool isSizeChanged = true;

    /// <summary>
    /// 初始化 <see cref="Aura3DViewBase"/> 类的新实例。
    /// </summary>
    public Aura3DViewBase()
    {
        Stopwatch = new Stopwatch();

        // 控件加载后自动获取键盘焦点，确保按键事件无需先点击即可响应
        Focusable = true;
        Loaded += (s, e) => Focus();
        PointerEntered += (s, e) => Focus();
    }

    /// <summary>
    /// 创建渲染管线的委托，默认使用 <see cref="BlinnPhongPipeline"/>。
    /// </summary>
    public Func<Scene, RenderPipeline> CreateRenderPipeline = scene => new BlinnPhongPipeline(scene);

    /// <summary>
    /// 渲染管线的用户可配置设置。需在渲染上下文初始化前设置，构造时配置在管线创建后修改不会再生效。
    /// </summary>
    public PipelineSettings PipelineSettings { get; set; } = new PipelineSettings();

    /// <summary>
    /// 确保场景存在并与当前渲染上下文绑定。首次创建时触发 <see cref="OnSceneInitialized"/>，
    /// 复用已有场景时触发 <see cref="OnContextRestored"/>。
    /// </summary>
    private void EnsureScene(Func<string, nint> getProcAddress)
    {
        if (Scene == null)
        {
            Scene = new Scene(CreateRenderPipeline, PipelineSettings, renderSurface);

            Scene.RenderPipeline.Initialize(getProcAddress);

            Stopwatch.Restart();

            IsContextLost = false;

            DispatchSceneEvent(OnSceneInitialized);

            return;
        }

        if (!Scene.RenderPipeline.IsInitialized)
        {
            // 上下文已重建（控件重新挂载或宿主替换了上下文）：场景、节点与材质均为原实例。
            Scene.RenderPipeline.Initialize(getProcAddress);

            Stopwatch.Restart();

            IsContextLost = false;

            DispatchSceneEvent(OnContextRestored);
        }
    }

    /// <summary>
    /// 上下文丢失的共享处理：只失效 GPU 句柄，不执行任何 GL 调用；
    /// 场景、节点与 CPU 资源保持不变，下一帧按 <see cref="EnsureScene"/> 路径重建。
    /// </summary>
    private protected void ContextLostCore()
    {
        if (Scene == null || Scene.RenderPipeline.IsDestroyed)
            return;

        Scene.RenderPipeline.HandleContextLost();

        IsContextLost = true;

        // 强制下一帧重新绑定输出帧缓冲，旧句柄可能已属于丢失的上下文。
        fb = -1;

        DispatchSceneEvent(OnContextLost);

        // 若控件当前空闲，需要一次渲染回调驱动恢复流程。
        RequestNextFrameCore();
    }
    /// <summary>
    /// 控件分离时的共享处理：上下文仍有效，真正删除 GL 对象归还显存，随后按上下文丢失路径失效句柄。
    /// </summary>
    private protected void DetachAndReleaseGpu()
    {
        if (Scene == null)
            return;

        // 此刻上下文尚未销毁，可以真正删除 GL 对象以释放显存，而不是仅仅失效句柄。
        if (!Scene.RenderPipeline.IsDestroyed)
            Scene.RenderPipeline.ReleaseGpuResources();

        // 清空 gl 引用，使重新挂载时能再次 Initialize；重复失效是无副作用的空操作。
        Scene.RenderPipeline.HandleContextLost();

        IsContextLost = true;
        fb = -1;

        Stopwatch.Stop();

        DispatchSceneEvent(OnContextLost);
    }

    /// <summary>
    /// 释放场景在 GPU 上分配的全部对象以归还显存，场景、节点与 CPU 资源全部保留，
    /// 下次渲染时按需重建。GL 调用只能在渲染线程执行，因此实际释放发生在下一帧渲染开始时。
    /// </summary>
    public void ReleaseGpuResources()
    {
        if (Scene == null)
            return;

        releaseGpuResourcesRequested = true;

        RequestNextFrameRendering();
    }

    /// <summary>
    /// 销毁当前场景及其全部 GPU 资源，并清空 <see cref="Scene"/>。销毁在下一帧渲染开始时执行，
    /// 若控件仍在渲染，同一帧会自动创建新场景并重新触发 <see cref="OnSceneInitialized"/>。
    /// </summary>
    public void DestroyScene()
    {
        if (Scene == null)
            return;

        destroySceneRequested = true;

        RequestNextFrameRendering();
    }

    private void ApplyDestroyScene()
    {
        if (Scene == null)
            return;

        if (!Scene.RenderPipeline.IsDestroyed)
            Scene.RenderPipeline.Destroy();

        var destroyedScene = Scene;

        Scene = null;
        fb = -1;
        IsContextLost = false;

        Stopwatch.Stop();

        // 事件参数携带已销毁的场景引用，异步派发时不得再读 Scene。
        DispatchSceneEvent(() => OnSceneDestroyed(destroyedScene));
    }

    private RenderSurface renderSurface = new RenderSurface();

    private bool releaseGpuResourcesRequested;

    private bool destroySceneRequested;

    /// <summary>
    /// 计算当前应分配的输出缓冲像素尺寸（逻辑尺寸 × RenderScaling，至少 1×1）。
    /// </summary>
    private protected (uint Width, uint Height, float Scale) ComputePixelSize()
    {
        uint width = (uint)Bounds.Width;
        uint height = (uint)Bounds.Height;
        float scale = 1.0f;

        width = Math.Max(width, 1);
        height = Math.Max(height, 1);
        if (this.GetPresentationSource() is { } source)
        {
            scale = (float)source.RenderScaling;
            width = (uint)(Bounds.Width * scale);
            height = (uint)(Bounds.Height * scale);
        }

        width = Math.Max(width, 1);
        height = Math.Max(height, 1);

        return (width, height, scale);
    }

    private void UpdateRenderSurfaceSize()
    {
        if (isSizeChanged == true)
        {
            var (width, height, scale) = ComputePixelSize();

            renderSurface.Width = width;
            renderSurface.Height = height;
            renderSurface.Scale = scale;

            isSizeChanged = false;
        }
    }

    /// <summary>
    /// 平台无关的单帧驱动：处理挂起的销毁/释放请求，确保场景就绪，更新并渲染一帧。
    /// 由两个渲染后端在渲染回调（渲染线程）中调用。
    /// </summary>
    /// <param name="getProcAddress">当前渲染上下文的 GL 入口点解析委托。</param>
    /// <param name="frameBufferId">输出帧缓冲句柄（默认帧缓冲为 0）。</param>
    protected void RenderFrameCore(Func<string, nint> getProcAddress, uint frameBufferId)
    {
        if (destroySceneRequested)
        {
            destroySceneRequested = false;
            releaseGpuResourcesRequested = false;

            ApplyDestroyScene();
        }

        // 管线被显式销毁后不再渲染，也不会自动重建场景。
        if (Scene is { RenderPipeline.IsDestroyed: true })
            return;

        if (releaseGpuResourcesRequested)
        {
            releaseGpuResourcesRequested = false;

            Scene?.RenderPipeline.ReleaseGpuResources();
        }

        EnsureScene(getProcAddress);

        var deltaTime = Stopwatch.Elapsed.TotalSeconds;

        Stopwatch.Restart();

        UpdateRenderSurfaceSize();

        if (this.fb != (int)frameBufferId)
        {
            this.fb = (int)frameBufferId;
            renderSurface.FrameBufferId = frameBufferId;
        }

        var scene = Scene!;

        // Update first: process animation + dirty octree nodes,
        // so Render() culls with up-to-date bounding boxes.
        scene.Update(deltaTime);

        scene.RenderPipeline.Render();

        DispatchSceneEvent(() => OnSceneUpdated(deltaTime));

        if (AutoRequestNextFrameRendering)
        {
            RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// 请求再调度一次渲染帧。遮蔽 <c>OpenGlControlBase</c> 同名方法并转发到当前后端，
    /// 保证应用侧在任一平台以相同签名驱动渲染。
    /// </summary>
    public new void RequestNextFrameRendering() => RequestNextFrameCore();

    /// <summary>
    /// 各平台后端把"再来一帧"的意图翻译成自己的调度机制。
    /// </summary>
    partial void RequestNextFrameCore();

    /// <summary>
    /// 当 GPU 句柄全部失效时调用：上下文丢失或控件从视觉树分离均会触发。此时不能执行任何 GL 调用；
    /// 场景、节点与 CPU 资源保持不变，恢复后按需重建。
    /// </summary>
    protected virtual void OnContextLost()
    {
    }

    /// <summary>
    /// 当渲染管线在新上下文中重新就绪后调用。控件重新挂载或宿主替换了上下文都会触发。
    /// <see cref="Scene"/> 与其中的节点保持不变，不会再次触发 <see cref="OnSceneInitialized"/>。
    /// </summary>
    protected virtual void OnContextRestored()
    {
    }

    /// <summary>
    /// 各平台后端把场景事件翻译成自己的线程语义：桌面端内联触发；
    /// iOS 宿主帧回调在渲染线程，事件必须切回 UI 线程，页面处理器才能安全访问控件。
    /// </summary>
    partial void DispatchSceneEvent(Action callback);

    protected abstract void OnSceneInitialized();

    protected abstract void OnSceneDestroyed(Scene scene);

    protected abstract void OnSceneUpdated(double deltaTime);

    /// <summary>
    /// 向场景中添加指定节点。
    /// </summary>
    /// <typeparam name="T">节点类型。</typeparam>
    /// <param name="node">要添加的节点。</param>
    public void AddNode<T>(T node) where T : Node
    {
        Scene?.AddNode(node);
    }

    /// <summary>
    /// 从场景中移除指定节点。
    /// </summary>
    /// <param name="node">要移除的节点。</param>
    public void Remove(Node node)
    {
        Scene?.RemoveNode(node);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        isSizeChanged = true;
    }

    /// <summary>
    /// 对指定点进行命中测试，判断其是否位于控件边界内。
    /// </summary>
    /// <param name="point">要测试的点。</param>
    /// <returns>如果点在边界内，则为 <c>true</c>；否则为 <c>false</c>。</returns>
    public bool HitTest(Point point)
    {
        if (point.X < 0 || point.Y < 0 || point.X > Bounds.Width || point.Y > Bounds.Height)
            return false;
        return true;
    }

    /// <summary>
    /// 获取场景的主相机。
    /// </summary>
    public Camera MainCamera => Scene.MainCamera;

    /// <summary>
    /// 获取或设置是否启用点击拾取物体功能。默认为 true。
    /// 设置为 false 后，点击将不会触发 <see cref="ObjectPicked"/> 事件。
    /// </summary>
    public bool EnablePicking { get; set; } = true;

    /// <summary>
    /// 当在视图中点击拾取到物体时触发。
    /// </summary>
    public event EventHandler<ObjectPickedEventArgs>? ObjectPicked;

    /// <summary>
    /// 在指定屏幕坐标处执行射线拾取，返回所有命中结果。
    /// </summary>
    /// <param name="x">相对于控件的 X 坐标（像素）。</param>
    /// <param name="y">相对于控件的 Y 坐标（像素）。</param>
    /// <returns>按距离排序的命中结果列表。</returns>
    public List<PickResult> PickAt(double x, double y)
    {
        if (Scene == null)
            return [];

        var source = this.GetPresentationSource();
        float scale = source != null ? (float)source.RenderScaling : 1.0f;

        return Scene.Pick((float)x * scale, (float)y * scale);
    }

    /// <summary>
    /// 在指定屏幕坐标处拾取最近的物体。
    /// </summary>
    /// <param name="x">相对于控件的 X 坐标（像素）。</param>
    /// <param name="y">相对于控件的 Y 坐标（像素）。</param>
    /// <returns>最近的命中结果，无命中时返回 null。</returns>
    public PickResult? PickClosestAt(double x, double y)
    {
        if (Scene == null)
            return null;

        var source = this.GetPresentationSource();
        float scale = source != null ? (float)source.RenderScaling : 1.0f;

        return Scene.PickClosest((float)x * scale, (float)y * scale);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!EnablePicking || Scene == null)
            return;

        var position = e.GetPosition(this);
        var result = PickClosestAt(position.X, position.Y);

        if (result != null)
        {
            var args = new ObjectPickedEventArgs(result);
            ObjectPicked?.Invoke(this, args);
        }
    }
}
