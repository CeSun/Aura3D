using Silk.NET.OpenGLES;

namespace Aura3D.Core.Resources;

public abstract class BaseTexture<T> where T : BaseTexture<T>
{
    public bool IsHdr { get; set; } = false;
    public TextureWrapMode WrapS { get; set; } = TextureWrapMode.ClampToEdge;

    public TextureWrapMode WrapT { get; set; } = TextureWrapMode.ClampToEdge;

    public TextureFilterMode MinFilter { get; set; } = TextureFilterMode.Linear;

    public TextureFilterMode MagFilter { get; set; } = TextureFilterMode.Linear;
    public ColorFormat ColorFormat { get; set; }

    public bool IsGammaSpace { get; set; } = false;


    public T SetWarpS(TextureWrapMode mode)
    {
        WrapS = mode;
        return (T)this;
    }

    public T SetWarpT(TextureWrapMode mode)
    {
        WrapT = mode;
        return (T)this;
    }

    public T SetMinFilter(TextureFilterMode mode)
    {
        MinFilter = mode;
        return (T)this;
    }

    public T SetMagFilter(TextureFilterMode mode)
    {
        MagFilter = mode;
        return (T)this;
    }

    public T SetColorFormat(ColorFormat format)
    {
        ColorFormat = format;
        return (T)this;
    }


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
