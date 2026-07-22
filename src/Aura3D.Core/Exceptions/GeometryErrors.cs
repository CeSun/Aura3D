using System.Globalization;

namespace Aura3D.Core.Exceptions;

internal static class GeometryErrors
{
    private const string MinimumSegmentCountMessage =
        "The '{0}' value must be greater than or equal to {1}.";

    private const string TriangleIndexCountMessage =
        "The index count must be a multiple of three.";

    public static ArgumentOutOfRangeException MinimumSegmentCount(
        string paramName,
        int minimum,
        int actualValue) =>
        new(paramName, actualValue, Format(MinimumSegmentCountMessage, paramName, minimum));

    public static ArgumentException TriangleIndexCount(string paramName) =>
        new(TriangleIndexCountMessage, paramName);

    private static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
