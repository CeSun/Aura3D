using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.Rendering;
using System.Diagnostics;
using Aura3D.Core.Resources;
using Aura3D.Core.Renderers;
using Aura3D.Core;
using Avalonia.VisualTree;

namespace Aura3D.Avalonia;

/// <summary>
/// Avalonia OpenGL 渲染控件的基类，负责管理场景生命周期、渲染循环以及节点操作。
/// </summary>
public abstract class Aura3DViewBase : global::Avalonia.OpenGL.Controls.OpenGlControlBase, ICustomHitTest
{
    /// <summary>
    /// 获取或设置当前关联的 3D 场景。
    /// </summary>
    public Scene? Scene { get; protected set; }

    Stopwatch Stopwatch;

    int fb = 0;

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
    /// 渲染管线的用户可配置设置。需在 OpenGL 初始化前设置，构造时配置在管线创建后修改不会再生效。
    /// </summary>
    public PipelineSettings PipelineSettings { get; set; } = new PipelineSettings();

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        UpdateRenderSurfaceSize();

        Scene = new Scene(CreateRenderPipeline, PipelineSettings, renderSurface);

        Scene.RenderPipeline.Initialize(gl.GetProcAddress);

        Stopwatch.Start();

        UpdateRenderSurfaceSize();

        OnSceneInitialized();
    }

    private RenderSurface renderSurface = new RenderSurface();
    private void UpdateRenderSurfaceSize()
    {
        if (isSizeChanged == true)
        {
            var source = this.GetPresentationSource();

            uint width = (uint)Bounds.Width;
            uint height = (uint)Bounds.Height;
            float scale = 1.0f;

            width = Math.Max(width, 1);
            height = Math.Max(height, 1);
            if (source != null)
            {
                scale = (float)source.RenderScaling;
                width = (uint)(Bounds.Width * scale);
                height = (uint)(Bounds.Height * scale);
            }

            renderSurface.Width = width;
            renderSurface.Height = height;
            renderSurface.Scale = scale;

            isSizeChanged = false;
        }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (Scene == null)
            return;

        var deltaTime = Stopwatch.Elapsed.TotalSeconds;

        Stopwatch.Restart();

        UpdateRenderSurfaceSize();

        if (this.fb != fb)
        {
            this.fb = fb;
            renderSurface.FrameBufferId = (uint)fb;
        }

        // Update first: process animation + dirty octree nodes,
        // so Render() culls with up-to-date bounding boxes.
        Scene.Update(deltaTime);

        Scene.RenderPipeline.Render();

        OnSceneUpdated(deltaTime);

        if (AutoRequestNextFrameRendering)
        {
            RequestNextFrameRendering();
        }

    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        base.OnOpenGlDeinit(gl);
        
        if (Scene == null) 
            return;

        Scene?.RenderPipeline.Destroy();

        OnSceneDestroyed();

        Stopwatch.Stop();

    }

    protected abstract void OnSceneInitialized();

    protected abstract void OnSceneDestroyed();

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
