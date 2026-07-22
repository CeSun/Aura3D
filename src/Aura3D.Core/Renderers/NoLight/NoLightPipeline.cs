using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the no light pipeline type.
/// </summary>
public class NoLightPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    /// <summary>
    /// Initializes a new instance of the no light pipeline type.
    /// </summary>
    public NoLightPipeline(Scene scene) : base(scene)
    {
        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        var noLightPass = new NoLightPass(this);

        RegisterRenderPass(new BackgroundPass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);
        RegisterRenderPass(noLightPass.SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        // Particle pass
        RegisterRenderPass(new ParticlePass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, baseRenderTarget.GetTexture("Color")).SetOutput(gammaOutput), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new FxaaPass(this, gammaOutput.GetTexture("Color")).SetOutput(CameraOutput), RenderPassGroup.EveryCamera);

        RegisterDebugPass(baseRenderTarget);
    }

    /// <summary>
    /// Creates the instance.
    /// </summary>
    public static RenderPipeline CreateInstance(Scene scene) => new NoLightPipeline(scene);

    /// <summary>
    /// Performs the before camera render operation.
    /// </summary>
    public override void BeforeCameraRender(Camera camera)
    {
        base.BeforeCameraRender(camera);
        if (gl == null)
            return;
        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.Width, camera.Height);
    }
}
