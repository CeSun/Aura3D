using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

public class TextureGpuState : IResourceGpuState
{
    private WeakReference<Aura3D.Core.Resources.Texture> texture;

    public bool IsAlive => texture.TryGetTarget(out _);
    public ulong Version => GetResource().Version;
    public ulong SyncedVersion { get; protected set; }

    public virtual uint TextureId { get; protected set; }

    internal TextureGpuState(Aura3D.Core.Resources.Texture texture)
    {
        this.texture = new WeakReference<Aura3D.Core.Resources.Texture>(texture);
    }

    public virtual void Destroy(GL gl)
    {
        DestroyTexture(gl);
    }

    public virtual void Upload(GL gl)
    {
        var texture = GetResource();

        DestroyTexture(gl);

        TextureId = gl.GenTexture();

        gl.BindTexture(TextureTarget.Texture2D, TextureId);

        ApplyTextureParameters(gl, texture);
        UploadTextureStorage(gl, texture);

        gl.BindTexture(TextureTarget.Texture2D, 0);
        SyncedVersion = texture.Version;
    }

    protected Aura3D.Core.Resources.Texture GetResource()
    {
        if (texture.TryGetTarget(out var value))
            return value;

        throw new InvalidOperationException("The CPU resource has already been collected.");
    }

    protected void DestroyTexture(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
            TextureId = 0;
        }
    }

    protected void ApplyTextureParameters(GL gl, Aura3D.Core.Resources.Texture texture)
    {
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)texture.WrapS.ToGlWrap());
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)texture.WrapT.ToGlWrap());
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)texture.MagFilter.ToGlFilter());
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)texture.MinFilter.ToGlFilter());
    }

    protected virtual unsafe void UploadTextureStorage(GL gl, Aura3D.Core.Resources.Texture texture)
    {
        if (texture.IsHdr == true)
        {
            var hdrData = texture.AsHdrData();
            if (hdrData.IsEmpty)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, texture.ToGlInternalFormat(), texture.Width, texture.Height, 0, texture.ColorFormat.ToGlFormat(), GLEnum.Float, null);
            }
            else
            {
                fixed (float* p = hdrData)
                {
                    gl.TexImage2D(GLEnum.Texture2D, 0, texture.ToGlInternalFormat(), texture.Width, texture.Height, 0, texture.ColorFormat.ToGlFormat(), GLEnum.Float, p);
                }
            }
        }
        else
        {
            var ldrData = texture.AsLdrData();
            if (ldrData.IsEmpty)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, texture.ToGlInternalFormat(), texture.Width, texture.Height, 0, texture.ColorFormat.ToGlFormat(), GLEnum.UnsignedByte, null);
            }
            else
            {
                fixed (byte* p = ldrData)
                {
                    gl.TexImage2D(GLEnum.Texture2D, 0, texture.ToGlInternalFormat(), texture.Width, texture.Height, 0, texture.ColorFormat.ToGlFormat(), GLEnum.UnsignedByte, p);
                }
            }
        }
    }
}
