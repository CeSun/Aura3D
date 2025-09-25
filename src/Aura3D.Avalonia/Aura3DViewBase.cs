using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.Rendering;
using System.Diagnostics;
using Aura3D.Core.Resources;
using Aura3D.Core.Renderers;

namespace Aura3D.Avalonia;

public abstract class Aura3DViewBase : global::Avalonia.OpenGL.Controls.OpenGlControlBase, ICustomHitTest
{
    public Scene? Scene { get; protected set; }

    Stopwatch Stopwatch;

    int fb = 0;

    protected bool isSizeChanged = true;
    public Aura3DViewBase()
    {
        Stopwatch = new Stopwatch();
    }

    public Func<Scene, RenderPipeline> CreateRenderPipeline = scene => new BlinnPhongPipeline(scene);

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        Scene = new Scene(CreateRenderPipeline);

        Scene.RenderPipeline.Initialize(gl.GetProcAddress);

        Stopwatch.Start();

        OnSceneInitialized();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (Scene == null)
            return;

        var deltaTime = Stopwatch.Elapsed.TotalSeconds;

        Scene.RenderPipeline.DefaultFramebuffer = (uint)fb;


        if (isSizeChanged == true)
        {
            uint width = (uint)Bounds.Width;
            uint height = (uint)Bounds.Height;

            if (VisualRoot != null)
            {
                width = (uint)(Bounds.Width * VisualRoot.RenderScaling);
                height = (uint)(Bounds.Height * VisualRoot.RenderScaling);
            }

            foreach (var renderTarget in Scene.ControlRenderTargets)
            {
                renderTarget.Width = width;
                renderTarget.Height = height;
            }

            isSizeChanged = false;
        }

        if (this.fb != fb)
        {
            this.fb = fb;
            foreach(var renderTarget in Scene.ControlRenderTargets)
            {
                renderTarget.FrameBufferId = (uint)fb;
            }
        }

        Scene.RenderPipeline.Render();

        Scene.Update(deltaTime);

        OnSceneUpdated(deltaTime);

        Stopwatch.Restart();

        RequestNextFrameRendering();
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

    public void AddNode<T>(T node) where T : Node
    {
        Scene.AddNode(node);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        isSizeChanged = true;
    }

    public bool HitTest(Point point)
    {
        if (point.X < 0 || point.Y < 0 || point.X > Bounds.Width || point.Y > Bounds.Height)
            return false;
        return true;
    }
}
