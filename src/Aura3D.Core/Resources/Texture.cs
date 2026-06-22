using Silk.NET.OpenGLES;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// 纹理类，支持2D纹理的加载、上传和渲染
/// </summary>
public class Texture : BaseTexture<Texture>, IClone<Texture>
{
    private List<byte> _data = [];

    /// <summary>
    /// 从颜色创建纯色纹理
    /// </summary>
    /// <param name="color">颜色</param>
    /// <returns>纯色纹理</returns>
    public static Texture CreateFromColor(Color color)
    {
        var texture = new Resources.Texture();
        texture.SetLdrData(
        [
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
            color.R, color.G, color.B, color.A,
        ], 2, 2);
        texture.SetIsGammaSpace(false);
        texture.SetColorFormat(ColorFormat.RGBA);
        texture.MagFilter = TextureFilterMode.Nearest;
        texture.MinFilter = TextureFilterMode.Nearest;
        texture.WrapS = TextureWrapMode.Repeat;
        texture.WrapT = TextureWrapMode.Repeat;

        return texture;


    }

    public ReadOnlySpan<byte> AsLdrData() => IsHdr ? [] : CollectionsMarshal.AsSpan(_data);

    public ReadOnlySpan<float> AsHdrData() => IsHdr ? MemoryMarshal.Cast<byte, float>(CollectionsMarshal.AsSpan(_data)) : [];

    public Texture SetLdrData(ReadOnlySpan<byte> data, uint width, uint height)
    {
        _data = new List<byte>(data.ToArray());
        Width = width;
        Height = height;
        IsHdr = false;
        MarkModified();
        return this;
    }

    public Texture SetHdrData(ReadOnlySpan<float> data, uint width, uint height)
    {
        _data = ConvertHdrDataToBytes(data);
        Width = width;
        Height = height;
        IsHdr = true;
        MarkModified();
        return this;
    }

    public Texture Clone()
    {
        var texture = new Texture
        {
            Width = Width,
            Height = Height,
            IsHdr = IsHdr,
            WrapS = WrapS,
            WrapT = WrapT,
            MinFilter = MinFilter,
            MagFilter = MagFilter,
            ColorFormat = ColorFormat,
            IsGammaSpace = IsGammaSpace,
        };

        texture.SetPixelBuffer(_data);
        return texture;
    }

    public Texture DeepClone()
    {
        var texture = Clone();
        texture.SetPixelBuffer(new List<byte>(_data));
        return texture;
    }

    protected void SetPixelBuffer(List<byte> data)
    {
        _data = data;
    }

    protected void ClearPixelData()
    {
        _data = [];
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
