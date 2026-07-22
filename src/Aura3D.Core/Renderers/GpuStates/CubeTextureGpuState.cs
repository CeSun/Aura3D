using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the cube texture gpu state type.
/// </summary>
public class CubeTextureGpuState : IResourceGpuState
{
    private readonly WeakReference<CubeTexture> texture;

    /// <summary>
    /// Gets a value indicating whether the object is alive.
    /// </summary>
    public bool IsAlive => texture.TryGetTarget(out _);
    /// <summary>
    /// Gets the version.
    /// </summary>
    public ulong Version => GetResource().Version;
    /// <summary>
    /// Gets or sets the synced version.
    /// </summary>
    public ulong SyncedVersion { get; protected set; }

    /// <summary>
    /// Gets or sets the texture id.
    /// </summary>
    public virtual uint TextureId { get; protected set; }

    internal CubeTextureGpuState(CubeTexture texture)
    {
        this.texture = new WeakReference<CubeTexture>(texture);
    }

    /// <summary>
    /// Destroys the associated data.
    /// </summary>
    public virtual void Destroy(GL gl)
    {
        DestroyTexture(gl);
    }

    /// <summary>
    /// Uploads the associated data.
    /// </summary>
    public virtual void Upload(GL gl)
    {
        var texture = GetResource();

        DestroyTexture(gl);

        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);

        ApplyTextureParameters(gl, texture);
        UploadTextureStorage(gl, texture);

        gl.BindTexture(GLEnum.TextureCubeMap, 0);
        SyncedVersion = texture.Version;
    }

    /// <summary>
    /// Gets the resource.
    /// </summary>
    protected CubeTexture GetResource()
    {
        if (texture.TryGetTarget(out var value))
            return value;

        throw Aura3D.Core.Exceptions.RendererErrors.CpuResourceCollected(nameof(CubeTexture));
    }

    /// <summary>
    /// Destroys the texture.
    /// </summary>
    protected void DestroyTexture(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
            TextureId = 0;
        }
    }

    /// <summary>
    /// Applies the texture parameters.
    /// </summary>
    protected void ApplyTextureParameters(GL gl, CubeTexture texture)
    {
        gl.TexParameter(GLEnum.TextureCubeMap, TextureParameterName.TextureWrapR, (int)texture.WrapR.ToGlWrap());
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)texture.WrapS.ToGlWrap());
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)texture.WrapT.ToGlWrap());
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)texture.MagFilter.ToGlFilter());
        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)texture.MinFilter.ToGlFilter());
    }

    /// <summary>
    /// Uploads the texture storage.
    /// </summary>
    protected virtual unsafe void UploadTextureStorage(GL gl, CubeTexture texture)
    {
        for (int i = 0; i < 6; i++)
        {
            if (texture.IsHdr == false)
            {
                var ldrData = texture.AsLdrData(i);
                fixed (byte* p = ldrData)
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, texture.ToGlInternalFormat(), texture.Width, texture.Height, 0, texture.ColorFormat.ToGlFormat(), GLEnum.UnsignedByte, p);
                }
            }
            else
            {
                var hdrData = texture.AsHdrData(i);
                fixed (float* p = hdrData)
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, texture.ToGlInternalFormat(), texture.Width, texture.Height, 0, texture.ColorFormat.ToGlFormat(), GLEnum.Float, p);
                }
            }
        }
    }
}
