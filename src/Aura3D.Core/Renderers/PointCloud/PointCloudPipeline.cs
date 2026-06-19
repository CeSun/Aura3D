using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Aura3D.Core.Renderers;

namespace Aura3D.Core;

public class PointCloudPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    public PointCloudPipeline(Scene scene) : base(scene)
    {
        RegisterRenderPass(
            new BackgroundPass(this).SetOutPutRenderTarget("BaseRenderTarget"),
            RenderPassGroup.EveryCamera);

        var pointCloudPass = new PointCloudPass(this)
            .SetOutPutRenderTarget("BaseRenderTarget");
        RegisterRenderPass(pointCloudPass, RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new GammaCorrectionPass(this, "BaseRenderTarget", "Color")
                .SetOutPutRenderTarget("GammaOutput"),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new FxaaPass(this, "GammaOutput", "Color"),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new DebugDrawPass(this, "BaseRenderTarget"),
            RenderPassGroup.EveryCamera);

        RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(Settings.DepthFormat);

        RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);
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
