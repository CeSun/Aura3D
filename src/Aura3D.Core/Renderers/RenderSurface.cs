namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the render surface type.
/// </summary>
public class RenderSurface
{
    /// <summary>
    /// Gets or sets the frame buffer id.
    /// </summary>
    public uint FrameBufferId { get; set; }

    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the scale.
    /// </summary>
    public float Scale { get; set; } = 1f;
}
