using Aura3D.Core.Serialization;
using Silk.NET.OpenGLES;
using System.Drawing;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 光源基类，定义所有光源节点的公共属性。
/// </summary>
public abstract class Light : Node
{
    /// <summary>
    /// 获取或设置一个值，指示该光源是否投射阴影。
    /// </summary>
    [AuraField(since: 1)]
    public bool CastShadow { get; set; } = false; // 是否投射阴影

    /// <summary>
    /// 获取或设置光源颜色。
    /// </summary>
    [AuraField(since: 1)]
    public Color LightColor { get; set; } = Color.White; // 光源颜色
}
