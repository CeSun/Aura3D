using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Core.Serialization;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 聚光灯节点，在锥形范围内发射光线，支持内外锥角与衰减控制。
/// </summary>
[AuraChunk(chunkType: AuraChunkType.SpotLight, chunkVersion: 1)]
public partial class SpotLight : Light
{
    /// <summary>
    /// 初始化 <see cref="SpotLight"/> 类的新实例。
    /// </summary>
    public SpotLight()
    {
    }

    /// <summary>
    /// 阴影配置。
    /// </summary>
    [AuraField(since: 1)]
    public ShadowConfig ShadowConfig = new ShadowConfig
    {
        NearPlane = 1,
        FarPlane = 100
    };

    /// <summary>
    /// 获取或设置内锥角（度数）。
    /// </summary>
    [AuraField(since: 1)]
    public float InnerConeAngleDegree { get; set; } = 10;

    /// <summary>
    /// 获取或设置外锥角（度数）。
    /// </summary>
    [AuraField(since: 1)]
    public float OuterAngleDegree { get; set; } = 15;

    /// <summary>
    /// 获取或设置发光强度（单位：坎德拉）。
    /// </summary>
    [AuraField(since: 1)]
    public float LuminousIntensity { get; set; } = 1000;

    /// <summary>
    /// 获取光照强度。
    /// </summary>
    public float Intensity => LuminousIntensity * 0.001f;

    /// <summary>
    /// 获取或设置光照衰减半径。
    /// </summary>
    [AuraField(since: 1)]
    public float AttenuationRadius { get; set; } = 10f; // 光照衰减半径

    /// <summary>
    /// 获取或设置阴影柔化半径比例。
    /// </summary>
    [AuraField(since: 1)]
    public float SoftRatio { get; set; } = 0.9f; // 阴影柔化半径
}
