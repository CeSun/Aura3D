using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 无光照渲染管线，仅渲染基础纹理颜色，不进行光照计算
/// </summary>
public class NoLightPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    /// <summary>
    /// 初始化无光照渲染管线
    /// </summary>
    /// <param name="scene">场景对象</param>
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

        // 调试绘制通道（方向轴、网格等），最后渲染以覆盖在所有内容之上
        RegisterRenderPass(new DebugDrawPass(this, baseRenderTarget).SetOutput(CameraOutput), RenderPassGroup.EveryCamera);
    }

    /// <summary>
    /// 创建渲染管线实例的工厂方法
    /// </summary>
    /// <param name="scene">场景对象</param>
    /// <returns>新的 NoLightPipeline 实例</returns>
    public static RenderPipeline CreateInstance(Scene scene) => new NoLightPipeline(scene);

    public override void BeforeCameraRender(Camera camera)
    {
        base.BeforeCameraRender(camera);
        if (gl == null)
            return;
        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.Width, camera.Height);
    }
}
