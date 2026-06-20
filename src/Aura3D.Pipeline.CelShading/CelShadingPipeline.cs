using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using Aura3D.Core;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public class CelShadingPipeline : RenderPipeline, IRenderPipelineCreateInstance
{

    public CelShadingPipeline(Scene scene) : base(scene)
    {
        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        var shadowMapPass = new ShadowMapPass(this);
        RegisterRenderPass(shadowMapPass, RenderPassGroup.Once);


        RegisterRenderPass(new BackgroundPass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        var basePass = (CelLightPass)new CelLightPass(this).SetOutput(baseRenderTarget);
		LightLimitChangedEvent += basePass.UpdateLightNumLimit;
        RegisterRenderPass(basePass, RenderPassGroup.EveryCamera);

        var outlinePass = (OutlinePass)new OutlinePass(this).SetOutput(baseRenderTarget);
        RegisterRenderPass(outlinePass, RenderPassGroup.EveryCamera);


        var translucentPass = (CelTranslucentPass)new CelTranslucentPass(this).SetOutput(baseRenderTarget);
        RegisterRenderPass(translucentPass, RenderPassGroup.EveryCamera);
		LightLimitChangedEvent += translucentPass.UpdateLightNumLimit;


        // Particle pass
        RegisterRenderPass(new ParticlePass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, baseRenderTarget.GetTexture("Color")).SetOutput(gammaOutput), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new FxaaPass(this, gammaOutput.GetTexture("Color")).SetOutput(CameraOutput), RenderPassGroup.EveryCamera);

        // Debug draw pass
        RegisterRenderPass(new DebugDrawPass(this, baseRenderTarget).SetOutput(CameraOutput), RenderPassGroup.EveryCamera);
    }

    public override void BeforeCameraRender(Camera camera)
    {
        if (gl == null)
            return;
        SortMeshes(Meshes, camera);
        gl.Viewport(0, 0, camera.Width, camera.Height);

    }


    public override void AfterCameraRender(Camera camera)
    {


    }

    public override void AfterRender()
    {
        
    }


    public override void BeforeRender()
    {

    }

    public static RenderPipeline CreateInstance(Scene scene) => new CelShadingPipeline(scene);
}
