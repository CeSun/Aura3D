using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;
/// <summary>
/// Blinn-Phong 渲染管线，支持阴影、光照、透明度和后处理效果
/// </summary>
public class BlinnPhongPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    /// <summary>
    /// Blinn-Phong 管线支持 CSM（级联阴影贴图），为主方向光生成多级联阴影。
    /// </summary>
    public override bool SupportsCSM => true;

    /// <summary>
    /// 初始化 Blinn-Phong 渲染管线
    /// </summary>
    /// <param name="scene">场景对象</param>
    public BlinnPhongPipeline(Scene scene) : base(scene)
    {
        var shadowMapPass = new ShadowMapPass(this);
        RegisterRenderPass(shadowMapPass, RenderPassGroup.Once);

        
        RegisterRenderPass(new BackgroundPass(this).SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        var basePass = (LightPass)new LightPass(this).SetOutPutRenderTarget("BaseRenderTarget");
        LightLimitChangedEvent += basePass.UpdateLightNumLimit;
        RegisterRenderPass(basePass, RenderPassGroup.EveryCamera);


        var translucentPass = (TranslucentPass)new TranslucentPass(this).SetOutPutRenderTarget("BaseRenderTarget");
        RegisterRenderPass(translucentPass, RenderPassGroup.EveryCamera);
        LightLimitChangedEvent += translucentPass.UpdateLightNumLimit;

        // Particle pass
        RegisterRenderPass(new ParticlePass(this).SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, "BaseRenderTarget", "Color").SetOutPutRenderTarget("GammaOutput"), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new FxaaPass(this, "GammaOutput", "Color"), RenderPassGroup.EveryCamera);

        // 调试绘制通道（方向轴、网格等），最后渲染以覆盖在所有内容之上
        RegisterRenderPass(new DebugDrawPass(this, "BaseRenderTarget"), RenderPassGroup.EveryCamera);

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


    public override void AfterCameraRender(Camera camera)
    {


    }

    public override void AfterRender()
    {
        
    }


    public override void BeforeRender()
    {

    }

    /// <summary>
    /// 创建渲染管线实例的工厂方法
    /// </summary>
    /// <param name="scene">场景对象</param>
    /// <returns>新的 BlinnPhongPipeline 实例</returns>
    public static RenderPipeline CreateInstance(Scene scene) => new BlinnPhongPipeline(scene);
}
