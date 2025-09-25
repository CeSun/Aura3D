using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Nodes;

public class SpotLight : Light
{
    public SpotLight()
    {
        ShadowMapRenderTarget = new RenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
    }

    public ShadowConfig ShadowConfig = new ShadowConfig
    {
        NearPlane = 1,
        FarPlane = 100
    };

    public RenderTarget ShadowMapRenderTarget { get; private set; }
    public float InnerConeAngleDegree { get; set; } = 10;
    public float OuterAngleDegree { get; set; } = 15;

    public float AttenuationRadius { get; set; } = 10f; // 光照衰减半径

    public override List<IGpuResource> GetGpuResources()
    {
        return [ShadowMapRenderTarget];
    }
}
