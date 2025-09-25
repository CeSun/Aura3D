using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;
public class BlinnPhongPipeline : RenderPipeline
{

    public BlinnPhongPipeline(Scene scene)
    {
        var shadowMapPass = new ShadowMapPass(this);
        lightLimitChangedEvent += shadowMapPass.UpdateLightNumLimit;
        RegisterRenderPass(shadowMapPass, RenderPassGroup.Once);


        RegisterRenderPass(new BackgroundPass(this).SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        var basePass = (LightPass)new LightPass(this).SetOutPutRenderTarget("BaseRenderTarget");
        lightLimitChangedEvent += basePass.UpdateLightNumLimit;
        RegisterRenderPass(basePass, RenderPassGroup.EveryCamera);


        var translucentPass = (TranslucentPass)new TranslucentPass(this).SetOutPutRenderTarget("BaseRenderTarget");
        RegisterRenderPass(translucentPass, RenderPassGroup.EveryCamera);
        lightLimitChangedEvent += translucentPass.UpdateLightNumLimit;


        RegisterRenderPass(new GammaCorrectionPass(this, "BaseRenderTarget", "Color").SetOutPutRenderTarget("GammaOutput"), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new FxaaPass(this, "GammaOutput", "Color"), RenderPassGroup.EveryCamera);

        RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(TextureFormat.DepthComponent16);

        RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(TextureFormat.DepthComponent16);


    }

    public override void BeforeCameraRender(Camera camera)
    {
        if (gl == null)
            return;
        SortMeshes(Meshes, camera);
        gl.Viewport(0, 0, camera.RenderTarget.Width, camera.RenderTarget.Height);

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
}
