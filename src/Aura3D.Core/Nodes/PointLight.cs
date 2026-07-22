using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.Maths;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the point light type.
/// </summary>
public class PointLight : Light
{
    /// <summary>
    /// Initializes a new instance of the point light type.
    /// </summary>
    public PointLight()
    {
    }


    /// <summary>
    /// Gets or sets the shadow config.
    /// </summary>
    public ShadowConfig ShadowConfig { get; set; } = new()
    {
        NearPlane = 1,
        FarPlane = 100
    };

    /// <summary>
    /// Gets or sets the attenuation radius.
    /// </summary>
    public float AttenuationRadius { get; set; } = 10f; // 光照衰减半径

    /// <summary>
    /// Gets or sets the soft ratio.
    /// </summary>
    public float SoftRatio { get; set; } = 0.9f; // 阴影柔化半径

    /// <summary>
    /// Gets or sets the luminous intensity.
    /// </summary>
    public float LuminousIntensity { get; set; } = 1000;

    /// <summary>
    /// Gets the intensity.
    /// </summary>
    public float Intensity => LuminousIntensity * 0.001f;


}


/// <summary>
/// Represents the shadow config type.
/// </summary>
public struct ShadowConfig
{
    /// <summary>
    /// Gets or sets the near plane.
    /// </summary>
    public float NearPlane { get; set; }

    /// <summary>
    /// Gets or sets the far plane.
    /// </summary>
    public float FarPlane { get; set; }

}
