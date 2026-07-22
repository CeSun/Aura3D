using System.Globalization;

namespace Aura3D.Core.Exceptions;

internal static class ResourceErrors
{
    private const string CubeTextureFaceCountMessage =
        "A cube texture must contain exactly six faces.";

    private const string InstanceAttributeCountMismatchMessage =
        "The instance attribute contains {0} values, but the geometry contains {1} instances.";

    private const string VertexAttributeLengthMismatchMessage =
        "The vertex attribute data length ({0}) must be a multiple of its component count ({1}).";

    private const string WritableTextureColorFormatOnlyMessage =
        "WritableTexture supports only color formats.";

    public static ArgumentException CubeTextureFaceCount() =>
        new(CubeTextureFaceCountMessage);

    public static ArgumentException InstanceAttributeCountMismatch(int dataCount, int instanceCount) =>
        new(Format(InstanceAttributeCountMismatchMessage, dataCount, instanceCount));

    public static ArgumentException VertexAttributeLengthMismatch(int dataCount, int componentCount) =>
        new(Format(VertexAttributeLengthMismatchMessage, dataCount, componentCount));

    public static ArgumentOutOfRangeException WritableTextureColorFormatOnly(string paramName) =>
        new(paramName, WritableTextureColorFormatOnlyMessage);

    private static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
