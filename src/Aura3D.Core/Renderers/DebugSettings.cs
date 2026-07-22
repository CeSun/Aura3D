namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the debug settings type.
/// </summary>
public class DebugSettings
{
    /// <summary>
    /// Gets or sets the enable.
    /// </summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// Gets or sets the show bounding box.
    /// </summary>
    public bool ShowBoundingBox { get; set; } = false;

    /// <summary>
    /// Gets or sets the show directional light.
    /// </summary>
    public bool ShowDirectionalLight { get; set; } = false;

    /// <summary>
    /// Gets or sets the show point light.
    /// </summary>
    public bool ShowPointLight { get; set; } = false;

    /// <summary>
    /// Gets or sets the show spot light.
    /// </summary>
    public bool ShowSpotLight { get; set; } = false;

    /// <summary>
    /// Gets or sets the show camera.
    /// </summary>
    public bool ShowCamera { get; set; } = false;

    /// <summary>
    /// Gets or sets the show bone.
    /// </summary>
    public bool ShowBone { get; set; } = false;

    /// <summary>
    /// Gets or sets the show particle bounds.
    /// </summary>
    public bool ShowParticleBounds { get; set; } = false;
}
