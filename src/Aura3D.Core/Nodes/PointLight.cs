using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.Maths;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Nodes;

public class PointLight : Light
{
    public PointLight()
    {
        ShadowMapRenderTarget = new CubeRenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
    }


    public ShadowConfig ShadowConfig = new ShadowConfig
    {
        NearPlane = 1,
        FarPlane = 100
    };

    public float AttenuationRadius { get; set; } = 10f; // 光照衰减半径

    public CubeRenderTarget ShadowMapRenderTarget { get; private set; }

    public override List<IGpuResource> GetGpuResources()
    {
        return [ShadowMapRenderTarget];
    }

}


public struct ShadowConfig
{
    public float NearPlane { get; set; }

    public float FarPlane { get; set; }

}
