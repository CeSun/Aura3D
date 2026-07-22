using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;
/// <summary>
/// Represents the blinn phong pipeline type.
/// </summary>
public class BlinnPhongPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    /// <summary>
    /// Gets the supports csm.
    /// </summary>
    public override bool SupportsCSM => true;

    /// <summary>
    /// Initializes a new instance of the blinn phong pipeline type.
    /// </summary>
    public BlinnPhongPipeline(Scene scene) : base(scene)
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

        var basePass = (LightPass)new LightPass(this).SetOutput(baseRenderTarget);
        LightLimitChangedEvent += basePass.UpdateLightNumLimit;
        RegisterRenderPass(basePass, RenderPassGroup.EveryCamera);


        var translucentPass = (TranslucentPass)new TranslucentPass(this, baseRenderTarget).SetOutput(baseRenderTarget);
        RegisterRenderPass(translucentPass, RenderPassGroup.EveryCamera);
        LightLimitChangedEvent += translucentPass.UpdateLightNumLimit;

        // Particle pass
        RegisterRenderPass(new ParticlePass(this).SetOutput(baseRenderTarget), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, baseRenderTarget.GetTexture("Color")).SetOutput(gammaOutput), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new FxaaPass(this, gammaOutput.GetTexture("Color")).SetOutput(CameraOutput), RenderPassGroup.EveryCamera);

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
    /// Performs the after camera render operation.
    /// </summary>
    public override void AfterCameraRender(Camera camera)
    {


    }

    /// <summary>
    /// Performs the after render operation.
    /// </summary>
    public override void AfterRender()
    {
        
    }


    /// <summary>
    /// Performs the before render operation.
    /// </summary>
    public override void BeforeRender()
    {

    }

    /// <summary>
    /// Creates the instance.
    /// </summary>
    public static RenderPipeline CreateInstance(Scene scene) => new BlinnPhongPipeline(scene);
}
