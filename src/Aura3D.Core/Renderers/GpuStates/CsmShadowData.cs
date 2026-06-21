using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// CSM（级联阴影贴图）运行时数据。
/// 由 ShadowMapPass 创建和填充，各光照 Pass 读取。
/// </summary>
public class CsmShadowData : IRuntimeGpuState
{
    public ulong Version { get; set; } = 1;
    public ulong SyncedVersion { get; private set; }

    /// <summary>CSM 级联的 lightViewProj 矩阵数组。</summary>
    public Matrix4x4[] CascadeMatrices { get; set; } = [];

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
        SyncedVersion = Version;
    }

    public void Destroy(GL gl)
    {
        if (TextureArrayId != 0) { gl.DeleteTexture(TextureArrayId); TextureArrayId = 0; }
        if (FboId != 0) { gl.DeleteFramebuffer(FboId); FboId = 0; }
        SyncedVersion = 0;
    }
}
