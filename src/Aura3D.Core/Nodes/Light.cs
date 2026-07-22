using Silk.NET.OpenGLES;
using System.Drawing;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the light type.
/// </summary>
public abstract class Light : Node
{
    /// <summary>
    /// Gets or sets the cast shadow.
    /// </summary>
    public bool CastShadow { get; set; } = false; // 是否投射阴影

    /// <summary>
    /// Gets or sets the light color.
    /// </summary>
    public Color LightColor { get; set; } = Color.White; // 光源颜色

}
