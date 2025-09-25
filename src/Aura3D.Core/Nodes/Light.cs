using Silk.NET.OpenGLES;
using System.Drawing;

namespace Aura3D.Core.Nodes;

public abstract class Light : Node
{
    public bool CastShadow { get; set; } = false; // 是否投射阴影

    public Color LightColor { get; set; } = Color.White; // 光源颜色

}
