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
