using System.Globalization;
using System.Numerics;

namespace Aura3D.Core.Exceptions;

internal static class SpatialErrors
{
    private const string OctreeMaxDepthMessage =
        "The octree maximum depth cannot be negative.";

    private const string OctreeSizeMessage =
        "The octree size must be positive and contain only finite values.";

    private const string ObjectBoundingBoxNullMessage =
        "The object's bounding box cannot be null.";

    private const string ObjectBoundingBoxInvalidMessage =
        "The object's bounding box contains a non-finite value.";

    private const string ObjectNotInOctreeMessage =
        "The object is not registered in the octree.";

    private const string ObjectBoundingBoxNullDuringUpdateMessage =
        "The object's bounding box became null while updating the octree.";

    private const string BoundingBoxVectorInvalidMessage =
        "The bounding-box minimum and maximum values must be finite.";

    private const string BoundingBoxOrderInvalidMessage =
        "The bounding-box minimum must not exceed its maximum. Tolerance: {0}. Invalid axes: {1}.";

    private const string BoundingBoxTransformInvalidMessage =
        "The matrix transform produced a non-finite bounding-box value. Matrix: {0}.";

    private const string PointCollectionInvalidMessage =
        "The point collection contains a non-finite value.";

    private const string PointCollectionEmptyMessage =
        "The point collection cannot be empty.";

    private const string BoundingBoxCollectionContainsNullMessage =
        "The bounding-box collection cannot contain null elements.";

    private const string BoundingBoxCollectionEmptyMessage =
        "The bounding-box collection cannot be empty.";

    public static ArgumentOutOfRangeException OctreeMaxDepth(string paramName) =>
        new(paramName, OctreeMaxDepthMessage);

    public static ArgumentException OctreeSize(string paramName) =>
        new(OctreeSizeMessage, paramName);

    public static ArgumentException ObjectBoundingBoxNull(string paramName) =>
        new(ObjectBoundingBoxNullMessage, paramName);

    public static ArgumentException ObjectBoundingBoxInvalid(string paramName) =>
        new(ObjectBoundingBoxInvalidMessage, paramName);

    public static KeyNotFoundException ObjectNotInOctree() =>
        new(ObjectNotInOctreeMessage);

    public static InvalidOperationException ObjectBoundingBoxNullDuringUpdate() =>
        new(ObjectBoundingBoxNullDuringUpdateMessage);

    public static ArgumentException BoundingBoxVectorInvalid(string paramName) =>
        new(BoundingBoxVectorInvalidMessage, paramName);

    public static ArgumentException BoundingBoxOrderInvalid(float tolerance, string axes) =>
        new(Format(BoundingBoxOrderInvalidMessage, tolerance, axes));

    public static InvalidOperationException BoundingBoxTransformInvalid(Matrix4x4 matrix) =>
        new(Format(BoundingBoxTransformInvalidMessage, matrix));

    public static ArgumentException PointCollectionInvalid(string paramName) =>
        new(PointCollectionInvalidMessage, paramName);

    public static InvalidOperationException PointCollectionEmpty() =>
        new(PointCollectionEmptyMessage);

    public static ArgumentException BoundingBoxCollectionContainsNull(string paramName) =>
        new(BoundingBoxCollectionContainsNullMessage, paramName);

    public static InvalidOperationException BoundingBoxCollectionEmpty() =>
        new(BoundingBoxCollectionEmptyMessage);

    private static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
