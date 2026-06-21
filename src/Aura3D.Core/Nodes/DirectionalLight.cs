using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 方向光节点，模拟无限远处的平行光源，常用于模拟太阳光。
/// </summary>
public class DirectionalLight : Light
{
    /// <summary>
    /// 初始化 <see cref="DirectionalLight"/> 类的新实例。
    /// </summary>
    public DirectionalLight()
    {
        // ShadowMapRenderTarget = new RenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
    }

    /// <summary>
    /// 阴影贴图配置。
    /// </summary>
    public DirectionalLightShadowMapConfig ShadowConfig = new DirectionalLightShadowMapConfig
    {
        Width = 50,
        Height = 50,
        NearPlane = 0.1f,
        FarPlane = 50
    };

    /// <summary>
    /// 获取或设置辐照度（单位：勒克斯）。
    /// </summary>
    public float Irradiance { get; set; } = 80000;

    /// <summary>
    /// 获取光照强度。
    /// </summary>
    public float Intensity => Irradiance * 0.00001f;
}

/// <summary>
/// 方向光阴影贴图配置。
/// </summary>
public class DirectionalLightShadowMapConfig
{
    /// <summary>
    /// 获取或设置阴影贴图宽度。
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 获取或设置阴影贴图高度。
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 获取或设置近裁剪面。
    /// </summary>
    public float NearPlane { get; set; }

    /// <summary>
    /// 获取或设置远裁剪面。
    /// </summary>
    public float FarPlane { get; set; }
}
