namespace Aura3D.Core.Exceptions;

internal static class TextureLoaderErrors
{
    private const string UnsupportedColorFormatMessage =
        "The image color format is not supported.";

    private const string CubeTextureImageCountMessage =
        "A cube texture requires exactly six images.";

    private const string CubeTextureDimensionMismatchMessage =
        "All cube-texture images must have the same dimensions.";

    private const string CubeTextureColorFormatMismatchMessage =
        "All cube-texture images must have the same color format.";

    public static NotSupportedException UnsupportedColorFormat() =>
        new(UnsupportedColorFormatMessage);

    public static ArgumentException CubeTextureImageCount() =>
        new(CubeTextureImageCountMessage);

    public static ArgumentException CubeTextureDimensionMismatch() =>
        new(CubeTextureDimensionMismatchMessage);

    public static ArgumentException CubeTextureColorFormatMismatch() =>
        new(CubeTextureColorFormatMismatchMessage);
}
