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
ControlRenderTarget controlRenderTarget = new ControlRenderTarget();
Camera.ControlRenderTarget = controlRenderTarget;
Scene scene = new Scene(scene => new BlinnPhongPipeline(scene), new PipelineSettings());


TestView? testView = null;

window.Load += () =>
{
    controlRenderTarget.Width = (uint)(window.Size.X);
    controlRenderTarget.Height = (uint)(window.Size.Y);
    controlRenderTarget.FrameBufferId = 0;

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

    controlRenderTarget.Width = (uint)(window.Size.X);
    controlRenderTarget.Height = (uint)(window.Size.Y);
    scene.RenderPipeline.DefaultFramebuffer = (uint)0;

    scene.RenderPipeline.Render();

    scene.Update(delta);


    testView.OnUpdate(delta);


};

window.Run();
