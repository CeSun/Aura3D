using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Aura3D.Core.Renderers;

namespace Aura3D.Core;

public class PointCloudPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    public PointCloudPipeline(Scene scene) : base(scene)
    {
        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        RegisterRenderPass(
            new BackgroundPass(this).SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);

        var pointCloudPass = new PointCloudPass(this)
            .SetOutput(baseRenderTarget);
        RegisterRenderPass(pointCloudPass, RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new GammaCorrectionPass(this, baseRenderTarget.GetTexture("Color"))
                .SetOutput(gammaOutput),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new FxaaPass(this, gammaOutput.GetTexture("Color")).SetOutput(CameraOutput),
            RenderPassGroup.EveryCamera);

        RegisterDebugPass(baseRenderTarget);
    }

    public override void BeforeCameraRender(Camera camera)
    {
        if (gl == null)
            return;
        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.Width, camera.Height);
    }

    public static RenderPipeline CreateInstance(Scene scene)
        => new PointCloudPipeline(scene);
}
