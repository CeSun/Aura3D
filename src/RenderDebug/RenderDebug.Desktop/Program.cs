// See https://aka.ms/new-console-template for more information
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Aura3D.Core.Renderers;
using RenderDebug;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Aura3D.Pipeline.PBR;

var window = Window.Create(WindowOptions.Default);
RenderSurface renderSurface = new RenderSurface();
Scene scene = new Scene(scene => new BlinnPhongPipeline(scene), new PipelineSettings(), renderSurface);


TestView? testView = null;

window.Load += () =>
{
    renderSurface.Width = (uint)(window.Size.X);
    renderSurface.Height = (uint)(window.Size.Y);
    renderSurface.FrameBufferId = 0;

    scene.RenderPipeline.Initialize(str =>
    {
        window.GLContext.TryGetProcAddress(str, out var p);
        return p;
    });

    var inputContext = window.CreateInput();

    testView = new TestView(scene, inputContext, name => File.OpenRead($"../../../../../../example/Example/Assets/{name}"));

    testView.OnInit();

  
};


window.Render += (delta) =>
{
    if (window.WindowState == WindowState.Minimized)
        return;

    renderSurface.Width = (uint)(window.Size.X);
    renderSurface.Height = (uint)(window.Size.Y);
    renderSurface.FrameBufferId = 0;

    scene.RenderPipeline.Render();

    scene.Update(delta);


    testView.OnUpdate(delta);


};

window.Run();
