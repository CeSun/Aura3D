using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

public class CubeTexture : BaseTexture<CubeTexture>, IGpuResource, ICubeTexture, IClone<CubeTexture>
{
    public bool NeedsUpload { get; set; } = true;
    public uint TextureId { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public List<byte>[] Data { get; set; } = [[], [], [], [], [], []];

    public unsafe void Upload(GL gl)
    {
        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);

        for (int i = 0; i < 6; i++)
        {
            unsafe
            {
                fixed (void* p = CollectionsMarshal.AsSpan(Data[i]))
                {
                    gl.TexImage2D(GLEnum.TextureCubeMapPositiveX + i, 0, GLInternalFormat, Width, Height, 0, GlFormat, GLEnum.UnsignedByte, p);
                }

            }

        }

        gl.TexParameter(GLEnum.TextureCubeMap, TextureParameterName.TextureWrapR, (int)GlWarpR);

        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)GlWarpS);

        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)GlWarpT);

        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)GlMagFilter);

        gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)GlMinFilter);

    }

    public void Destroy(GL gl)
    {
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
            TextureId = 0;
        }
    }

    public CubeTexture Clone()
    {
        return new CubeTexture
        {
            TextureId = 0,
            Width = Width,
            Height = Height,
            Data = Data,
            WrapS = WrapS,
            WrapT = WrapT,
            WrapR = WrapR,
            MinFilter = MinFilter,
            MagFilter = MagFilter,
            ColorFormat = ColorFormat,
            IsGammaSpace = IsGammaSpace,
        };
    }

    public CubeTexture DeepClone()
    {
        var texture = Clone();
        if (Data != null)
        {
            int i = 0;
            foreach(var singleFace in Data)
            {
                var newFace = new List<byte>(singleFace);
                texture.Data[i++] = newFace;
            }
        }
        return texture;
    }

    public TextureWrapMode WrapR { get; set; } = TextureWrapMode.ClampToEdge;

    protected GLEnum GlWarpR => WrapR switch
    {
        TextureWrapMode.Repeat => GLEnum.Repeat,
        TextureWrapMode.MirroredRepeat => GLEnum.MirroredRepeat,
        TextureWrapMode.ClampToEdge => GLEnum.ClampToEdge,
        TextureWrapMode.ClampToBorder => GLEnum.ClampToBorder,
        _ => GLEnum.False
    };

}
