using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

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
/// CSM（级联阴影贴图）运行时数据。作为 IGpuState 缓存在方向光节点上，
/// 由 ShadowMapPass 创建和填充，各光照 Pass 读取。
/// </summary>
public class CsmShadowData : IGpuState
{
    /// <summary>CSM 级联的 lightViewProj 矩阵数组。</summary>
    public System.Numerics.Matrix4x4[] CascadeMatrices { get; set; } = [];

    /// <summary>CSM 级联分割深度（相机空间），长度 = CascadeCount + 1。</summary>
    public float[] CascadeSplitDepths { get; set; } = [];

    /// <summary>2D 纹理数组 ID。</summary>
    public uint TextureArrayId { get; set; }

    /// <summary>FBO ID。</summary>
    public uint FboId { get; set; }

    /// <summary>贴图分辨率。</summary>
    public int Resolution { get; set; }

    /// <summary>级联数量。</summary>
    public int CascadeCount { get; set; }

    public void Upload(GL gl)
    {
        // 由 ShadowMapPass 直接创建，无需额外上传步骤
    }

    public void Destroy(GL gl)
    {
        if (TextureArrayId != 0) { gl.DeleteTexture(TextureArrayId); TextureArrayId = 0; }
        if (FboId != 0) { gl.DeleteFramebuffer(FboId); FboId = 0; }
    }
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
