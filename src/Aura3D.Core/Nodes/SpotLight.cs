using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the spot light type.
/// </summary>
public class SpotLight : Light
{
    /// <summary>
    /// Initializes a new instance of the spot light type.
    /// </summary>
    public SpotLight()
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
    /// Gets or sets the inner cone angle degree.
    /// </summary>
    public float InnerConeAngleDegree { get; set; } = 10;

    /// <summary>
    /// Gets or sets the outer cone angle degree.
    /// </summary>
    public float OuterConeAngleDegree { get; set; } = 15;

    /// <summary>
    /// Gets or sets the luminous intensity.
    /// </summary>
    public float LuminousIntensity { get; set; } = 1000;

    /// <summary>
    /// Gets the intensity.
    /// </summary>
    public float Intensity => LuminousIntensity * 0.001f;

    /// <summary>
    /// Gets or sets the attenuation radius.
    /// </summary>
    public float AttenuationRadius { get; set; } = 10f; // 光照衰减半径

    /// <summary>
    /// Gets or sets the soft ratio.
    /// </summary>
    public float SoftRatio { get; set; } = 0.9f; // 阴影柔化半径

}
