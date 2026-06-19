using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers.Common;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aura3D.Core.Renderers;
using Aura3D.Core;

namespace Aura3D.Pipeline.PBR;

public class PBRDeferredPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    /// <inheritdoc />
    public override bool SupportsCSM => true;

    public Texture DefaultBaseColor { get; private set; }

    public Texture DefaultNormal { get; private set; }

    public Texture DefaultMetallicRoughness { get; private set; }

    public Texture DefaultEmissive { get; private set; }

    public Texture DefaultOcclusion { get; private set; }

    public CubeTexture DefaultIblAmbientCubeTexture
    {
        get
        {
            if (_defaultIblAmbientCubeTexture == null)
            {
                var texture = Texture.CreateFromColor(Color.White);
                var cube = HDRIToCubeTextureConverter.ConvertFromTexture(texture, 16);
                _defaultIblAmbientCubeTexture = cube;
                EnsureUploaded(cube);
            }
            return _defaultIblAmbientCubeTexture;
        }
    }

    private CubeTexture? _defaultIblAmbientCubeTexture = null;

    public Texture BrdfLutTexture;

    public PBRDeferredPipeline(Scene scene) : base(scene)
    {
        using (var ms = new MemoryStream(ShaderResource.lut))
        {
            BrdfLutTexture = Core.TextureLoader.LoadHdrTexture(ms);
        }

        var shadowPass = new ShadowMapPass(this);
        RegisterRenderPass(shadowPass, RenderPassGroup.Once);

        RegisterRenderPass(new IrradianceMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PrefilteredEnvironmentMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new BasePass(this).SetOutPutRenderTarget("GBuffer"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new IBLAmbientPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new DirectionalLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new SpotLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PointLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new BackgroundPass(this).SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new CopyPass(this, "BaseRenderTarget", "Color").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentIBLAmbientPass(this, "GBuffer").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentPass(this, "GBuffer").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        // Particle pass
        RegisterRenderPass(new ParticlePass(this).SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new ToneMappingPass(this, "BackgroundRenderTarget", "Color").SetOutPutRenderTarget("GammaOutput"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, "GammaOutput", "Color").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new FxaaPass(this, "BackgroundRenderTarget", "Color"), RenderPassGroup.EveryCamera);

        // 调试绘制通道（方向轴、网格等），最后渲染以覆盖在所有内容之上
        RegisterRenderPass(new DebugDrawPass(this, "BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderTarget("GBuffer")
            .AddTexture("BaseColor", TextureFormat.Rgba8)
            .AddTexture("NormalRoughness", TextureFormat.Rgba8)
            .AddTexture("MetallicEmissive", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);


        RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba32f)
            .SetDepthTexture(Settings.DepthFormat);

        RegisterRenderTarget("BackgroundRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba32f)
            .SetDepthTexture(Settings.DepthFormat);


        RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba32f)
            .SetDepthTexture(Settings.DepthFormat);

        DefaultBaseColor = Texture.CreateFromColor(Color.White);


        DefaultNormal = Texture.CreateFromColor(Color.FromArgb(128, 128, 255));


        DefaultMetallicRoughness = Texture.CreateFromColor(Color.FromArgb(0, 127, 0));


        DefaultEmissive = Texture.CreateFromColor(Color.Black);

        DefaultOcclusion = Texture.CreateFromColor(Color.White);

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

    public override void Setup()
    {
        if (gl == null)
            return;
        EnsureUploaded(DefaultBaseColor);
        EnsureUploaded(DefaultNormal);
        EnsureUploaded(DefaultMetallicRoughness);
        EnsureUploaded(DefaultEmissive);
        EnsureUploaded(DefaultOcclusion);
        EnsureUploaded(BrdfLutTexture);
    }

    public override void Destroy()
    {
        base.Destroy();
    }
}
