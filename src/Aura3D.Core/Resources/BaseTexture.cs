using Aura3D.Core.Serialization;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Resources;

/// <summary>
/// 纹理基类，提供纹理的通用属性和方法
/// </summary>
/// <typeparam name="T">纹理类型</typeparam>
public abstract class BaseTexture<T> where T : BaseTexture<T>
{
    /// <summary>
    /// 是否为 HDR 纹理
    /// </summary>
    [AuraField(since: 1)]
    public bool IsHdr { get; set; } = false;
    /// <summary>
    /// S 方向环绕模式
    /// </summary>
    [AuraField(since: 1)]
    public TextureWrapMode WrapS { get; set; } = TextureWrapMode.ClampToEdge;

    /// <summary>
    /// T 方向环绕模式
    /// </summary>
    [AuraField(since: 1)]
    public TextureWrapMode WrapT { get; set; } = TextureWrapMode.ClampToEdge;

    /// <summary>
    /// 缩小过滤模式
    /// </summary>
    [AuraField(since: 1)]
    public TextureFilterMode MinFilter { get; set; } = TextureFilterMode.Linear;

    /// <summary>
    /// 放大过滤模式
    /// </summary>
    [AuraField(since: 1)]
    public TextureFilterMode MagFilter { get; set; } = TextureFilterMode.Linear;
    /// <summary>
    /// 颜色格式
    /// </summary>
    [AuraField(since: 1)]
    public ColorFormat ColorFormat { get; set; }

    /// <summary>
    /// 是否在伽马空间
    /// </summary>
    [AuraField(since: 1)]
    public bool IsGammaSpace { get; set; } = false;


    /// <summary>
    /// 设置 S 方向环绕模式
    /// </summary>
    /// <param name="mode">环绕模式</param>
    /// <returns>当前纹理对象</returns>
    public T SetWarpS(TextureWrapMode mode)
    {
        WrapS = mode;
        return (T)this;
    }

    /// <summary>
    /// 设置 T 方向环绕模式
    /// </summary>
    /// <param name="mode">环绕模式</param>
    /// <returns>当前纹理对象</returns>
    public T SetWarpT(TextureWrapMode mode)
    {
        WrapT = mode;
        return (T)this;
    }

    /// <summary>
    /// 设置缩小过滤模式
    /// </summary>
    /// <param name="mode">过滤模式</param>
    /// <returns>当前纹理对象</returns>
    public T SetMinFilter(TextureFilterMode mode)
    {
        MinFilter = mode;
        return (T)this;
    }

    /// <summary>
    /// 设置放大过滤模式
    /// </summary>
    /// <param name="mode">过滤模式</param>
    /// <returns>当前纹理对象</returns>
    public T SetMagFilter(TextureFilterMode mode)
    {
        MagFilter = mode;
        return (T)this;
    }

    /// <summary>
    /// 设置颜色格式
    /// </summary>
    /// <param name="format">颜色格式</param>
    /// <returns>当前纹理对象</returns>
    public T SetColorFormat(ColorFormat format)
    {
        ColorFormat = format;
        return (T)this;
    }


    /// <summary>
    /// 设置是否在伽马空间
    /// </summary>
    /// <param name="isGamma">是否在伽马空间</param>
    /// <returns>当前纹理对象</returns>
    public T SetIsGammaSpace(bool isGamma)
    {
        IsGammaSpace = isGamma;
        return (T)this;
    }
    protected InternalFormat GLInternalFormat => IsHdr switch
    {
        true when ColorFormat == ColorFormat.RGB => InternalFormat.Rgb16f,
        true when ColorFormat == ColorFormat.RGBA => InternalFormat.Rgba16f,
        false when ColorFormat == ColorFormat.RGB => IsGammaSpace ? InternalFormat.Srgb8 : InternalFormat.Rgb8,
        false when ColorFormat == ColorFormat.RGBA => IsGammaSpace ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8,
        _ => InternalFormat.Rgb8
    };

    protected GLEnum GlFormat => ColorFormat switch
    {
        ColorFormat.RGB => GLEnum.Rgb,
        ColorFormat.RGBA => GLEnum.Rgba,
        _ => GLEnum.False
    };
    protected GLEnum GlWarpS => WrapS switch
    {
        TextureWrapMode.Repeat => GLEnum.Repeat,
        TextureWrapMode.MirroredRepeat => GLEnum.MirroredRepeat,
        TextureWrapMode.ClampToEdge => GLEnum.ClampToEdge,
        TextureWrapMode.ClampToBorder => GLEnum.ClampToBorder,
        _ => GLEnum.False
    };

    protected GLEnum GlWarpT => WrapT switch
    {
        TextureWrapMode.Repeat => GLEnum.Repeat,
        TextureWrapMode.MirroredRepeat => GLEnum.MirroredRepeat,
        TextureWrapMode.ClampToEdge => GLEnum.ClampToEdge,
        TextureWrapMode.ClampToBorder => GLEnum.ClampToBorder,
        _ => GLEnum.False
    };

    protected GLEnum GlMinFilter => MinFilter switch
    {
        TextureFilterMode.Nearest => GLEnum.Nearest,
        TextureFilterMode.Linear => GLEnum.Linear,
        _ => GLEnum.False
    };


    protected GLEnum GlMagFilter => MagFilter switch
    {
        TextureFilterMode.Nearest => GLEnum.Nearest,
        TextureFilterMode.Linear => GLEnum.Linear,
        _ => GLEnum.False
    };


}
