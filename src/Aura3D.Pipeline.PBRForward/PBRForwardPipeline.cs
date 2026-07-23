using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Renderers.Common;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using System.Numerics;

using Aura3D.Pipeline.PBR.Common;

namespace Aura3D.Pipeline.PBRForward;

public class PBRForwardPipeline : PBRPipelineBase, IRenderPipelineCreateInstance
{
    /// <inheritdoc />
    public override bool SupportsCSM => true;

    public PBRForwardPipeline(Scene scene) : base(scene)
    {
        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba32f)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba32f)
            .SetDepthTexture(Settings.DepthFormat);

        RegisterRenderPass(new ShadowMapPass(this), RenderPassGroup.Once);

        RegisterRenderPass(new IrradianceMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PrefilteredEnvironmentMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new BackgroundPass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PBRForwardIBLAmbientPass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PBRForwardLightingPass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentIBLAmbientPass(this, baseRenderTarget).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentPass(this, baseRenderTarget).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new ParticlePass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new ToneMappingPass(this, baseRenderTarget.GetTexture("Color")).SetOutput(gammaOutput), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, gammaOutput.GetTexture("Color")).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new FxaaPass(this, baseRenderTarget.GetTexture("Color")).SetOutput(CameraOutput), RenderPassGroup.EveryCamera);

        RegisterDebugPass(baseRenderTarget);
    }

    public static RenderPipeline CreateInstance(Scene scene) => new PBRForwardPipeline(scene);

    public override void BeforeCameraRender(Camera camera)
    {
        base.BeforeCameraRender(camera);
        if (gl == null)
            return;

        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.Width, camera.Height);
    }
}
