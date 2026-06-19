using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Renderers;

public class TextureGpuState : IResourceGpuState<Aura3D.Core.Resources.Texture>, Aura3D.Core.Resources.IGpuTexture
{
    private WeakReference<Aura3D.Core.Resources.Texture> texture;

    public Aura3D.Core.Resources.Texture Texture => Resource;

    public Aura3D.Core.Resources.Texture Resource
    {
        get
        {
            if (texture.TryGetTarget(out var value))
                return value;

            throw new InvalidOperationException("The CPU resource has already been collected.");
        }
    }

    public bool IsAlive => texture.TryGetTarget(out _);

    public uint TextureId { get; private set; }

    public uint Width => Resource.Width;

    public uint Height => Resource.Height;

    public TextureGpuState(Aura3D.Core.Resources.Texture texture)
    {
        this.texture = new WeakReference<Aura3D.Core.Resources.Texture>(texture);
    }

    public void Destroy(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
            TextureId = 0;
        }
    }

    public unsafe void Upload(GL gl)
    {
        var texture = Resource;

        TextureId = gl.GenTexture();

        gl.BindTexture(TextureTarget.Texture2D, TextureId);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)texture.GetGlWarpS());

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)texture.GetGlWarpT());

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)texture.GetGlMagFilter());

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)texture.GetGlMinFilter());

        if (texture.IsHdr == true)
        {
            if (texture.HdrData == null)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, texture.GetGLInternalFormat(), texture.Width, texture.Height, 0, texture.GetGlFormat(), GLEnum.Float, null);
            }
            else
            {
                fixed (void* p = CollectionsMarshal.AsSpan(texture.HdrData))
                {
                    gl.TexImage2D(GLEnum.Texture2D, 0, texture.GetGLInternalFormat(), texture.Width, texture.Height, 0, texture.GetGlFormat(), GLEnum.Float, p);
                }
            }
        }
        else
        {
            if (texture.LdrData == null)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, texture.GetGLInternalFormat(), texture.Width, texture.Height, 0, texture.GetGlFormat(), GLEnum.UnsignedByte, null);
            }
            else
            {
                fixed (void* p = CollectionsMarshal.AsSpan(texture.LdrData))
                {
                    gl.TexImage2D(GLEnum.Texture2D, 0, texture.GetGLInternalFormat(), texture.Width, texture.Height, 0, texture.GetGlFormat(), GLEnum.UnsignedByte, p);
                }
            }
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
    }
}
