using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the directional light type.
/// </summary>
public class DirectionalLight : Light
{
    /// <summary>
    /// Initializes a new instance of the directional light type.
    /// </summary>
    public DirectionalLight()
    {
        // ShadowMapRenderTarget = new RenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
    }

    /// <summary>
    /// Gets or sets the shadow config.
    /// </summary>
    public DirectionalLightShadowMapConfig ShadowConfig { get; set; } = new()
    {
        Width = 50,
        Height = 50,
        NearPlane = 0.1f,
        FarPlane = 50
    };

    /// <summary>
    /// Gets or sets the irradiance.
    /// </summary>
    public float Irradiance { get; set; } = 80000;

    /// <summary>
    /// Gets the intensity.
    /// </summary>
    public float Intensity => Irradiance * 0.00001f;
}

/// <summary>
/// Represents the directional light shadow map config type.
/// </summary>
public class DirectionalLightShadowMapConfig
{
    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the near plane.
    /// </summary>
    public float NearPlane { get; set; }

    /// <summary>
    /// Gets or sets the far plane.
    /// </summary>
    public float FarPlane { get; set; }
}
