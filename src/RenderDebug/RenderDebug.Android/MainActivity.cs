using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Aura3D.Pipeline.PBR;
using RenderDebug;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;

namespace Example.Test.Android;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
    Scene scene = null;
    protected override void OnRun()
    {

        ControlRenderTarget controlRenderTarget = new ControlRenderTarget();
        Camera.ControlRenderTarget = controlRenderTarget;
        Scene scene = new Scene(scene => new PBRDeferredPipeline(scene));

        TestView? testView = null;

        var view = Silk.NET.Windowing.Window.GetView(ViewOptions.Default with { API = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0))});

        scene = new Scene(scene => new Aura3D.Core.Renderers.BlinnPhongPipeline(scene), new Aura3D.Core.Renderers.PipelineSettings());

        view.Load += () =>
        {
            controlRenderTarget.Width = (uint)(view.Size.X);
            controlRenderTarget.Height = (uint)(view.Size.Y);
            controlRenderTarget.FrameBufferId = 0;

            scene.RenderPipeline.Initialize(str =>
            {
                view.GLContext.TryGetProcAddress(str, out var p);
                return p;
            });

            var inputContext = view.CreateInput();
            testView = new TestView(scene, inputContext, name => Assets.Open($"Example/Assets/{name}"));

            testView.OnInit();



        };

        view.Render += (delta) =>
        {

            controlRenderTarget.Width = (uint)(view.Size.X);
            controlRenderTarget.Height = (uint)(view.Size.Y);
            scene.RenderPipeline.DefaultFramebuffer = (uint)0;

            scene.RenderPipeline.Render();

            scene.Update(delta);


            testView.OnUpdate(delta);


        };

        view.Run();
    }



    void AddNode<T>(T node) where T : Node
    {
        scene.AddNode(node);
    }
}