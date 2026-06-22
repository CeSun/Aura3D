using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.Maths;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 点光源节点，向所有方向均匀发射光线，光照强度随距离衰减。
/// </summary>
public class PointLight : Light
{
    /// <summary>
    /// 初始化 <see cref="PointLight"/> 类的新实例。
    /// </summary>
    public PointLight()
    {
    }


    /// <summary>
    /// 阴影配置。
    /// </summary>
    public ShadowConfig ShadowConfig { get; set; } = new()
    {
        NearPlane = 1,
        FarPlane = 100
    };

    /// <summary>
    /// 获取或设置光照衰减半径。
    /// </summary>
    public float AttenuationRadius { get; set; } = 10f; // 光照衰减半径

    /// <summary>
    /// 获取或设置阴影柔化半径比例。
    /// </summary>
    public float SoftRatio { get; set; } = 0.9f; // 阴影柔化半径

    /// <summary>
    /// 获取或设置发光强度（单位：坎德拉）。
    /// </summary>
    public float LuminousIntensity { get; set; } = 1000;

    /// <summary>
    /// 获取光照强度。
    /// </summary>
    public float Intensity => LuminousIntensity * 0.001f;


}


/// <summary>
/// 阴影配置结构体。
/// </summary>
public struct ShadowConfig
{
    /// <summary>
    /// 获取或设置近裁剪面。
    /// </summary>
    public float NearPlane { get; set; }

    /// <summary>
    /// 获取或设置远裁剪面。
    /// </summary>
    public float FarPlane { get; set; }

}
