using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

public class Texture : BaseTexture<Texture>, IClone<Texture>, IGpuResource, ITexture
{
    public bool NeedsUpload { get; set; } = true;
    public uint TextureId { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public List<byte>? LdrData { get; set; } = [];

    public List<float>? HdrData { get; set; } = [];

    public Texture SetLdrData(List<byte>? data, uint width, uint height)
    {
        LdrData = data;
        Width = width;
        Height = height;
        IsHdr = false;
        HdrData = [];
        return this;
    }

    public virtual void Destroy(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
            TextureId = 0;
        }
    }

    protected void setupParameters(GL gl)
    {

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GlWarpS);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GlWarpT);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GlMagFilter);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GlMinFilter);
    }


    public virtual unsafe void Upload(GL gl)
    {
        TextureId = gl.GenTexture();

        gl.BindTexture(TextureTarget.Texture2D, TextureId);

        setupParameters(gl);


        if (IsHdr == true)
        {
            if (HdrData == null)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, GLInternalFormat, Width, Height, 0, GlFormat, GLEnum.Float, null);
            }
            else
            {
                fixed (void* p = CollectionsMarshal.AsSpan(HdrData))
                {
                    gl.TexImage2D(GLEnum.Texture2D, 0, GLInternalFormat, Width, Height, 0, GlFormat, GLEnum.Float, p);
                }
            }
        }
        else
        {
            if (LdrData == null)
            {
                gl.TexImage2D(GLEnum.Texture2D, 0, GLInternalFormat, Width, Height, 0, GlFormat, GLEnum.UnsignedByte, null);
            }
            else
            {
                fixed (void* p = CollectionsMarshal.AsSpan(LdrData))
                {
                    gl.TexImage2D(GLEnum.Texture2D, 0, GLInternalFormat, Width, Height, 0, GlFormat, GLEnum.UnsignedByte, p);
                }
            }

        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
    }
    public Texture Clone()
    {
        return new Texture
        {
            TextureId = 0,
            Width = Width,
            Height = Height,
            LdrData = LdrData,
            HdrData = HdrData,
            WrapS = WrapS,
            WrapT = WrapT,
            MinFilter = MinFilter,
            MagFilter = MagFilter,
            ColorFormat = ColorFormat,
            IsGammaSpace = IsGammaSpace,
        };
    }

    public Texture DeepClone()
    {
        var texture = Clone();
        if (LdrData != null)
        {
            texture.LdrData = new List<byte>(LdrData);
        }
        if (HdrData != null)
        {
            texture.HdrData = new List<float>(HdrData);
        }
        return texture;
    }
}

public enum ColorFormat
{
    RGB = 0,
    RGBA = 1,
}

public enum TextureWrapMode
{
    Repeat = 0,
    MirroredRepeat = 1,
    ClampToEdge = 2,
    ClampToBorder = 3,
}

public enum TextureFilterMode
{
    Nearest = 0,
    Linear = 1,
}