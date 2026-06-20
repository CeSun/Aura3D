using Silk.NET.OpenGLES;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// 纹理类，支持2D纹理的加载、上传和渲染
/// </summary>
public class Texture : BaseTexture<Texture>, IClone<Texture>
{
    /// <summary>
    /// 从颜色创建纯色纹理
    /// </summary>
    /// <param name="color">颜色</param>
    /// <returns>纯色纹理</returns>
    public static Texture CreateFromColor(Color color)
    {
        var texture = new Resources.Texture();
        texture.SetLdrData(new List<byte> 
        { 
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
        }, 2, 2);
        texture.SetIsGammaSpace(false);
        texture.SetColorFormat(ColorFormat.RGBA);
        texture.MagFilter = TextureFilterMode.Nearest;
        texture.MinFilter = TextureFilterMode.Nearest;
        texture.WrapS = TextureWrapMode.Repeat;
        texture.WrapT = TextureWrapMode.Repeat;

        return texture;


    }
    public uint Width { get; set; }

    public uint Height { get; set; }

    public List<byte> LdrData { get; set; } = [];

    public List<float> HdrData { get; set; } = [];

    public Texture SetLdrData(List<byte> data, uint width, uint height)
    {
        LdrData = data;
        Width = width;
        Height = height;
        IsHdr = false;
        HdrData = [];
        return this;
    }

    public Texture SetHdrData(List<float> data, uint width, uint height)
    {
        HdrData = data;
        Width = width;
        Height = height;
        IsHdr = true;
        LdrData = [];
        return this;
    }

    public Texture Clone()
    {
        return new Texture
        {
            Width = Width,
            Height = Height,
            LdrData = LdrData,
            HdrData = HdrData,
            IsHdr = IsHdr,
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

    internal InternalFormat GetGLInternalFormat()
    {
        return GLInternalFormat;
    }

    internal GLEnum GetGlFormat()
    {
        return GlFormat;
    }

    internal GLEnum GetGlWarpS()
    {
        return GlWarpS;
    }

    internal GLEnum GetGlWarpT()
    {
        return GlWarpT;
    }

    internal GLEnum GetGlMagFilter()
    {
        return GlMagFilter;
    }

    internal GLEnum GetGlMinFilter()
    {
        return GlMinFilter;
    }
}

/// <summary>
/// 颜色格式枚举
/// </summary>
public enum ColorFormat
{
    RGB = 0,
    RGBA = 1,
}

/// <summary>
/// 纹理环绕模式枚举
/// </summary>
public enum TextureWrapMode
{
    /// <summary>
    /// 重复
    /// </summary>
    Repeat = 0,
    /// <summary>
    /// 镜像重复
    /// </summary>
    MirroredRepeat = 1,
    /// <summary>
    /// 钳制到边缘
    /// </summary>
    ClampToEdge = 2,
    /// <summary>
    /// 钳制到边界颜色
    /// </summary>
    ClampToBorder = 3,
}

/// <summary>
/// 纹理过滤模式枚举
/// </summary>
public enum TextureFilterMode
{
    /// <summary>
    /// 最近邻过滤
    /// </summary>
    Nearest = 0,
    /// <summary>
    /// 线性过滤
    /// </summary>
    Linear = 1,
}
