using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Aura3D.Core.Renderers;

namespace Aura3D.Core;

/// <summary>
/// Represents the point cloud pipeline type.
/// </summary>
public class PointCloudPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    /// <summary>
    /// Initializes a new instance of the point cloud pipeline type.
    /// </summary>
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

    /// <summary>
    /// Performs the before camera render operation.
    /// </summary>
    public override void BeforeCameraRender(Camera camera)
    {
        if (gl == null)
            return;
        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.Width, camera.Height);
    }

    /// <summary>
    /// Creates the instance.
    /// </summary>
    public static RenderPipeline CreateInstance(Scene scene)
        => new PointCloudPipeline(scene);
}
