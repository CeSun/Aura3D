using System.Drawing;

namespace Aura3D.Core.Scenes;

/// <summary>
/// Represents the grid type.
/// </summary>
public class Grid
{
    /// <summary>
    /// Gets or sets the enable.
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    public float Size { get; set; } = 10.0f;

    /// <summary>
    /// Gets or sets the divisions.
    /// </summary>
    public int Divisions { get; set; } = 10;

    /// <summary>
    /// Gets or sets the line color.
    /// </summary>
    public Color LineColor { get; set; } = Color.FromArgb(255, 80, 80, 80);

    /// <summary>
    /// Gets or sets the center line color.
    /// </summary>
    public Color CenterLineColor { get; set; } = Color.FromArgb(255, 60, 60, 60);
}
