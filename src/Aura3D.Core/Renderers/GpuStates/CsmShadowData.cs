using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the csm shadow data type.
/// </summary>
public class CsmShadowData : IRuntimeGpuState
{
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public ulong Version { get; set; } = 1;
    /// <summary>
    /// Gets or sets the synced version.
    /// </summary>
    public ulong SyncedVersion { get; private set; }

    /// <summary>
    /// Gets or sets the cascade matrices.
    /// </summary>
    public Matrix4x4[] CascadeMatrices { get; set; } = [];

    /// <summary>
    /// Gets or sets the cascade split depths.
    /// </summary>
    public float[] CascadeSplitDepths { get; set; } = [];

    /// <summary>
    /// Gets or sets the texture array id.
    /// </summary>
    public uint TextureArrayId { get; set; }

    /// <summary>FBO ID。</summary>
    public uint FboId { get; set; }

    /// <summary>
    /// Gets or sets the resolution.
    /// </summary>
    public int Resolution { get; set; }

    /// <summary>
    /// Gets or sets the cascade count.
    /// </summary>
    public int CascadeCount { get; set; }

    /// <summary>
    /// Uploads the associated data.
    /// </summary>
    public void Upload(GL gl)
    {
        // 由 ShadowMapPass 直接创建，无需额外上传步骤
        SyncedVersion = Version;
    }

    /// <summary>
    /// Destroys the associated data.
    /// </summary>
    public void Destroy(GL gl)
    {
        if (TextureArrayId != 0) { gl.DeleteTexture(TextureArrayId); TextureArrayId = 0; }
        if (FboId != 0) { gl.DeleteFramebuffer(FboId); FboId = 0; }
        SyncedVersion = 0;
    }
}
