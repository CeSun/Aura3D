using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Renderers;

public class TextureGpuState : IResourceGpuState<Aura3D.Core.Resources.Texture>, Aura3D.Core.Resources.IGpuTexture
{
    public Aura3D.Core.Resources.Texture Texture { get; }

    public Aura3D.Core.Resources.Texture Resource => Texture;

    public uint TextureId { get; private set; }

    public uint Width => Texture.Width;

    public uint Height => Texture.Height;

    public TextureGpuState(Aura3D.Core.Resources.Texture texture)
    {
        Texture = texture;
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
        TextureId = gl.GenTexture();

        gl.BindTexture(TextureTarget.Texture2D, TextureId);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)Texture.GetGlWarpS());

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)Texture.GetGlWarpT());

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)Texture.GetGlMagFilter());

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)Texture.GetGlMinFilter());

        if (Texture.IsHdr == true)
        {
            if (Texture.HdrData == null)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, Texture.GetGLInternalFormat(), Texture.Width, Texture.Height, 0, Texture.GetGlFormat(), GLEnum.Float, null);
            }
            else
            {
                fixed (void* p = CollectionsMarshal.AsSpan(Texture.HdrData))
                {
                    gl.TexImage2D(GLEnum.Texture2D, 0, Texture.GetGLInternalFormat(), Texture.Width, Texture.Height, 0, Texture.GetGlFormat(), GLEnum.Float, p);
                }
            }
        }
        else
        {
            if (Texture.LdrData == null)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, Texture.GetGLInternalFormat(), Texture.Width, Texture.Height, 0, Texture.GetGlFormat(), GLEnum.UnsignedByte, null);
            }
            else
            {
                fixed (void* p = CollectionsMarshal.AsSpan(Texture.LdrData))
                {
                    gl.TexImage2D(GLEnum.Texture2D, 0, Texture.GetGLInternalFormat(), Texture.Width, Texture.Height, 0, Texture.GetGlFormat(), GLEnum.UnsignedByte, p);
                }
            }
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
    }
}
