using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

internal static class TextureGlExtensions
{
    public static InternalFormat ToGlInternalFormat<T>(this BaseTexture<T> texture) where T : BaseTexture<T>
    {
        return texture.IsHdr switch
        {
            true when texture.ColorFormat == ColorFormat.RGB => InternalFormat.Rgb16f,
            true when texture.ColorFormat == ColorFormat.RGBA => InternalFormat.Rgba16f,
            false when texture.ColorFormat == ColorFormat.RGB => texture.IsGammaSpace ? InternalFormat.Srgb8 : InternalFormat.Rgb8,
            false when texture.ColorFormat == ColorFormat.RGBA => texture.IsGammaSpace ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8,
            _ => InternalFormat.Rgb8
        };
    }

    public static GLEnum ToGlFormat(this ColorFormat colorFormat)
    {
        return colorFormat switch
        {
            ColorFormat.RGB => GLEnum.Rgb,
            ColorFormat.RGBA => GLEnum.Rgba,
            _ => GLEnum.False
        };
    }

    public static GLEnum ToGlWrap(this Aura3D.Core.Resources.TextureWrapMode wrapMode)
    {
        return wrapMode switch
        {
            Aura3D.Core.Resources.TextureWrapMode.Repeat => GLEnum.Repeat,
            Aura3D.Core.Resources.TextureWrapMode.MirroredRepeat => GLEnum.MirroredRepeat,
            Aura3D.Core.Resources.TextureWrapMode.ClampToEdge => GLEnum.ClampToEdge,
            Aura3D.Core.Resources.TextureWrapMode.ClampToBorder => GLEnum.ClampToBorder,
            _ => GLEnum.False
        };
    }

    public static GLEnum ToGlFilter(this Aura3D.Core.Resources.TextureFilterMode filterMode)
    {
        return filterMode switch
        {
            Aura3D.Core.Resources.TextureFilterMode.Nearest => GLEnum.Nearest,
            Aura3D.Core.Resources.TextureFilterMode.Linear => GLEnum.Linear,
            _ => GLEnum.False
        };
    }
}
