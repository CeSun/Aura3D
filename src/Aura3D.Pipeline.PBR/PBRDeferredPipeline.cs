using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Renderers.Common;
using Aura3D.Core.Scenes;

using Aura3D.Pipeline.PBR.Common;

namespace Aura3D.Pipeline.PBR;

public class PBRDeferredPipeline : PBRPipelineBase, IRenderPipelineCreateInstance
{
    /// <inheritdoc />
    public override bool SupportsCSM => true;

    public PBRDeferredPipeline(Scene scene) : base(scene)
    {
        var gBuffer = RegisterRenderTarget("GBuffer")
            .AddTexture("BaseColor", TextureFormat.Rgba8)
            .AddTexture("NormalRoughness", TextureFormat.Rgba8)
            .AddTexture("MetallicEmissive", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba32f)
            .SetDepthTexture(Settings.DepthFormat);

        var backgroundRenderTarget = RegisterRenderTarget("BackgroundRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba32f)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba32f)
            .SetDepthTexture(Settings.DepthFormat);

        var shadowPass = new ShadowMapPass(this);
        RegisterRenderPass(shadowPass, RenderPassGroup.Once);

        RegisterRenderPass(new IrradianceMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PrefilteredEnvironmentMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new BasePass(this).SetOutput(gBuffer), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new IBLAmbientPass(this, gBuffer).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new DirectionalLightingPass(this, gBuffer).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new SpotLightingPass(this, gBuffer).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PointLightingPass(this, gBuffer).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new BackgroundPass(this).SetOutput(backgroundRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new CopyPass(this, baseRenderTarget.GetTexture("Color")).SetOutput(backgroundRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentIBLAmbientPass(this, gBuffer).SetOutput(backgroundRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentPass(this, gBuffer).SetOutput(backgroundRenderTarget), RenderPassGroup.EveryCamera);

        // Particle pass
        RegisterRenderPass(new ParticlePass(this).SetOutput(backgroundRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new ToneMappingPass(this, backgroundRenderTarget.GetTexture("Color")).SetOutput(gammaOutput), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, gammaOutput.GetTexture("Color")).SetOutput(backgroundRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new FxaaPass(this, backgroundRenderTarget.GetTexture("Color")).SetOutput(CameraOutput), RenderPassGroup.EveryCamera);

        RegisterDebugPass(backgroundRenderTarget);
    }

    public static RenderPipeline CreateInstance(Scene scene) => new PBRDeferredPipeline(scene);

    public override void BeforeCameraRender(Camera camera)
    {
        base.BeforeCameraRender(camera);
        if (gl == null)
            return;

        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.Width, camera.Height);
    }
}
