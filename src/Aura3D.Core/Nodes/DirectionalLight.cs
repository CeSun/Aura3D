using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Nodes;

public class DirectionalLight : Light
{
    public DirectionalLight()
    {
        ShadowMapRenderTarget = new RenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
    }

    public DirectionalLightShadowMapConfig ShadowConfig = new DirectionalLightShadowMapConfig
    {
        Width = 50,
        Height = 50,
        NearPlane = 0.1f,
        FarPlane = 50
    };

    public RenderTarget ShadowMapRenderTarget { get; private set; }

    public override List<IGpuResource> GetGpuResources()
    {
        return [ShadowMapRenderTarget];
    }
}

public class DirectionalLightShadowMapConfig
{
    public int Width { get; set; }

    public int Height { get; set; }

    public float NearPlane { get; set; }

    public float FarPlane { get; set; }
}