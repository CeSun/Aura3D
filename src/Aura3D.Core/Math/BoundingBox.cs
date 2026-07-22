using System;
using System.Collections.Generic;
using System.Numerics;
using System.Collections;

namespace Aura3D.Core.Math;

/// <summary>
/// Represents the bounding box type.
/// </summary>
public class BoundingBox : IEquatable<BoundingBox>
{
    /// <summary>
    /// Defines the default epsilon value.
    /// </summary>
    public const float DefaultEpsilon = 1e-6f;

    /// <summary>
    /// Gets the min.
    /// </summary>
    public Vector3 Min { get; }

    /// <summary>
    /// Gets the max.
    /// </summary>
    public Vector3 Max { get; }

    // 线程安全的惰性计算字段
    private readonly Lazy<Vector3> _lazySize;
    private readonly Lazy<Vector3> _lazyCenter;

    /// <summary>
    /// Initializes a new instance of the bounding box type.
    /// </summary>
    public BoundingBox(Vector3 min, Vector3 max)
    {
        // 校验无效浮点数
        if (IsInvalidVector(min) || IsInvalidVector(max))
        {
            throw Aura3D.Core.Exceptions.SpatialErrors.BoundingBoxVectorInvalid(
                IsInvalidVector(min) ? nameof(min) : nameof(max));
        }

        // 检查是否超出容差范围（避免浮点精度误判）
        bool xInvalid = min.X - max.X > DefaultEpsilon;
        bool yInvalid = min.Y - max.Y > DefaultEpsilon;
        bool zInvalid = min.Z - max.Z > DefaultEpsilon;

        if (xInvalid || yInvalid || zInvalid)
        {
            var invalidAxes = $"{(xInvalid ? "X " : "")}{(yInvalid ? "Y " : "")}{(zInvalid ? "Z " : "")}".Trim();
            throw Aura3D.Core.Exceptions.SpatialErrors.BoundingBoxOrderInvalid(DefaultEpsilon, invalidAxes);
        }

        // 主动修正微小精度误差，保证 Min <= Max
        Min = new Vector3(
            MathF.Min(min.X, max.X),
            MathF.Min(min.Y, max.Y),
            MathF.Min(min.Z, max.Z)
        );
        Max = new Vector3(
            MathF.Max(min.X, max.X),
            MathF.Max(min.Y, max.Y),
            MathF.Max(min.Z, max.Z)
        );

        // 惰性初始化 Size 和 Center（线程安全）
        _lazySize = new Lazy<Vector3>(() => Max - Min);
        _lazyCenter = new Lazy<Vector3>(() => (Min + Max) / 2f);
    }

    /// <summary>
    /// Gets the size.
    /// </summary>
    public Vector3 Size => _lazySize.Value;

    /// <summary>
    /// Gets the center.
    /// </summary>
    public Vector3 Center => _lazyCenter.Value;

    /// <summary>
    /// Performs the intersects operation.
    /// </summary>
    public bool Intersects(BoundingBox other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // 分离轴定理 + 浮点精度容差
        return !(other.Min.X - DefaultEpsilon > Max.X ||
                 other.Max.X + DefaultEpsilon < Min.X ||
                 other.Min.Y - DefaultEpsilon > Max.Y ||
                 other.Max.Y + DefaultEpsilon < Min.Y ||
                 other.Min.Z - DefaultEpsilon > Max.Z ||
                 other.Max.Z + DefaultEpsilon < Min.Z);
    }

    /// <summary>
    /// Performs the contains operation.
    /// </summary>
    public bool Contains(Vector3 point)
    {
        if (IsInvalidVector(point))
            return false;

        // 容差范围内的包含判断
        return point.X >= Min.X - DefaultEpsilon && point.X <= Max.X + DefaultEpsilon &&
               point.Y >= Min.Y - DefaultEpsilon && point.Y <= Max.Y + DefaultEpsilon &&
               point.Z >= Min.Z - DefaultEpsilon && point.Z <= Max.Z + DefaultEpsilon;
    }

    /// <summary>
    /// Performs the contains operation.
    /// </summary>
    public bool Contains(BoundingBox other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Contains(other.Min) && Contains(other.Max);
    }

    /// <summary>
    /// Transforms the associated data.
    /// </summary>
    public BoundingBox Transform(Matrix4x4 matrix)
    {
        // 生成包围盒的 8 个顶点
        Span<Vector3> corners = stackalloc Vector3[8]
        {
            new(Min.X, Min.Y, Min.Z),
            new(Max.X, Min.Y, Min.Z),
            new(Min.X, Max.Y, Min.Z),
            new(Max.X, Max.Y, Min.Z),
            new(Min.X, Min.Y, Max.Z),
            new(Max.X, Min.Y, Max.Z),
            new(Min.X, Max.Y, Max.Z),
            new(Max.X, Max.Y, Max.Z)
        };

        Vector3 transformedMin = new(float.MaxValue);
        Vector3 transformedMax = new(float.MinValue);

        foreach (var corner in corners)
        {
            // 齐次坐标变换（w=1）
            Vector4 homogeneous = new(corner, 1f);
            Vector4 transformed = Vector4.Transform(homogeneous, matrix);

            // 校验变换结果有效性
            if (IsInvalidVector(transformed))
            {
                throw Aura3D.Core.Exceptions.SpatialErrors.BoundingBoxTransformInvalid(matrix);
            }

            // 齐次除法（处理投影矩阵）
            if (MathF.Abs(transformed.W) > DefaultEpsilon)
            {
                transformed.X /= transformed.W;
                transformed.Y /= transformed.W;
                transformed.Z /= transformed.W;
            }

            Vector3 vec = new(transformed.X, transformed.Y, transformed.Z);
            transformedMin = Vector3.Min(transformedMin, vec);
            transformedMax = Vector3.Max(transformedMax, vec);
        }

        return new BoundingBox(transformedMin, transformedMax);
    }

    /// <summary>
    /// Creates the from points.
    /// </summary>
    public static BoundingBox CreateFromPoints(IEnumerable<Vector3> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool hasPoint = false;

        foreach (var p in points)
        {
            // 校验点有效性
            if (IsInvalidVector(p))
            {
                throw Aura3D.Core.Exceptions.SpatialErrors.PointCollectionInvalid(nameof(points));
            }

            hasPoint = true;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        if (!hasPoint)
        {
            throw Aura3D.Core.Exceptions.SpatialErrors.PointCollectionEmpty();
        }

        return new BoundingBox(min, max);
    }

    /// <summary>
    /// Creates the merged.
    /// </summary>
    public static BoundingBox CreateMerged(IEnumerable<BoundingBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool hasBox = false;

        foreach (var box in boxes)
        {
            if (box is null)
            {
                throw Aura3D.Core.Exceptions.SpatialErrors.BoundingBoxCollectionContainsNull(nameof(boxes));
            }

            hasBox = true;
            min = Vector3.Min(min, box.Min);
            max = Vector3.Max(max, box.Max);
        }

        if (!hasBox)
        {
            throw Aura3D.Core.Exceptions.SpatialErrors.BoundingBoxCollectionEmpty();
        }

        return new BoundingBox(min, max);
    }

    /// <summary>
    /// Determines whether invalid vector.
    /// </summary>
    public static bool IsInvalidVector(Vector3 vec)
    {
        return float.IsNaN(vec.X) || float.IsNaN(vec.Y) || float.IsNaN(vec.Z) ||
               float.IsInfinity(vec.X) || float.IsInfinity(vec.Y) || float.IsInfinity(vec.Z);
    }

    /// <summary>
    /// Determines whether invalid vector.
    /// </summary>
    public static bool IsInvalidVector(Vector4 vec)
    {
        return float.IsNaN(vec.X) || float.IsNaN(vec.Y) || float.IsNaN(vec.Z) || float.IsNaN(vec.W) ||
               float.IsInfinity(vec.X) || float.IsInfinity(vec.Y) || float.IsInfinity(vec.Z) || float.IsInfinity(vec.W);
    }

    /// <summary>
    /// Performs the expand operation.
    /// </summary>
    public BoundingBox Expand(float amount)
    {
        var expand = new Vector3(amount);
        return new BoundingBox(Min - expand, Max + expand);
    }

    /// <summary>
    /// Performs the equals operation.
    /// </summary>
    public bool Equals(BoundingBox? other)
    {
        if (other is null)
            return false;

        // 容差范围内的相等判断
        bool minEqual = MathF.Abs(Min.X - other.Min.X) < DefaultEpsilon &&
                        MathF.Abs(Min.Y - other.Min.Y) < DefaultEpsilon &&
                        MathF.Abs(Min.Z - other.Min.Z) < DefaultEpsilon;

        bool maxEqual = MathF.Abs(Max.X - other.Max.X) < DefaultEpsilon &&
                        MathF.Abs(Max.Y - other.Max.Y) < DefaultEpsilon &&
                        MathF.Abs(Max.Z - other.Max.Z) < DefaultEpsilon;

        return minEqual && maxEqual;
    }


    /// <summary>
    /// Determines whether box inside frustum.
    /// </summary>
    public bool IsBoxInsideFrustum(Span<Plane> planes)
    {
        // 生成 AABB 的 8 个顶点
        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = new Vector3(Min.X, Min.Y, Min.Z);
        corners[1] = new Vector3(Max.X, Min.Y, Min.Z);
        corners[2] = new Vector3(Min.X, Max.Y, Min.Z);
        corners[3] = new Vector3(Max.X, Max.Y, Min.Z);
        corners[4] = new Vector3(Min.X, Min.Y, Max.Z);
        corners[5] = new Vector3(Max.X, Min.Y, Max.Z);
        corners[6] = new Vector3(Min.X, Max.Y, Max.Z);
        corners[7] = new Vector3(Max.X, Max.Y, Max.Z);

        // 遍历六个平面
        foreach (var plane in planes)
        {
            bool allOutside = true;

            foreach (var corner in corners)
            {
                // 点到平面的距离
                float dist = Plane.DotCoordinate(plane, corner);

                if (dist >= 0)
                {
                    // 至少一个点在平面内侧
                    allOutside = false;
                    break;
                }
            }

            if (allOutside)
            {
                // 所有点都在平面外 → 整个盒子在视锥体外
                return false;
            }
        }

        // 所有平面都通过测试 → 在视锥体内或相交
        return true;
    }

    /// <summary>
    /// Performs the equals operation.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return Equals(obj as BoundingBox);
    }

    /// <summary>
    /// Gets the hash code.
    /// </summary>
    public override int GetHashCode()
    {
        // 按容差取整后计算哈希，保证精度范围内的相等性
        return HashCode.Combine(
            MathF.Round(Min.X / DefaultEpsilon),
            MathF.Round(Min.Y / DefaultEpsilon),
            MathF.Round(Min.Z / DefaultEpsilon),
            MathF.Round(Max.X / DefaultEpsilon),
            MathF.Round(Max.Y / DefaultEpsilon),
            MathF.Round(Max.Z / DefaultEpsilon)
        );
    }

    /// <summary>
    /// Determines whether two values are equal.
    /// </summary>
    public static bool operator ==(BoundingBox? left, BoundingBox? right)
    {
        return EqualityComparer<BoundingBox>.Default.Equals(left, right);
    }


    /// <summary>
    /// Determines whether two values are not equal.
    /// </summary>
    public static bool operator !=(BoundingBox? left, BoundingBox? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Performs the to string operation.
    /// </summary>
    public override string ToString()
    {
        return $"BoundingBox(Min=({Min.X:F6}, {Min.Y:F6}, {Min.Z:F6}), " +
               $"Max=({Max.X:F6}, {Max.Y:F6}, {Max.Z:F6}))";
    }

}
