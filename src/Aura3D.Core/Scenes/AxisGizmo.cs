using System.Drawing;

namespace Aura3D.Core.Scenes;

/// <summary>
/// Represents the axis gizmo type.
/// </summary>
public class AxisGizmo
{
    /// <summary>
    /// Gets or sets the enable.
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// Gets or sets the axis length.
    /// </summary>
    public float AxisLength { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the arrowhead size.
    /// </summary>
    public float ArrowheadSize { get; set; } = 0.15f;
}
