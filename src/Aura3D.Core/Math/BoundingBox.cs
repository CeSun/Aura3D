using System;
using System.Collections.Generic;
using System.Numerics;
using System.Collections;

namespace Aura3D.Core.Math;

/// <summary>
/// 三维轴对齐包围盒（AABB），提供相交、包含、变换、合并等核心功能
/// </summary>
public class BoundingBox : IEquatable<BoundingBox>
{
    /// <summary>
    /// 浮点精度容差（可根据业务场景调整）
    /// </summary>
    public const float DefaultEpsilon = 1e-6f;

    /// <summary>
    /// 包围盒最小值（左下后）
    /// </summary>
    public Vector3 Min { get; }

    /// <summary>
    /// 包围盒最大值（右上前）
    /// </summary>
    public Vector3 Max { get; }

    // 线程安全的惰性计算字段
    private readonly Lazy<Vector3> _lazySize;
    private readonly Lazy<Vector3> _lazyCenter;

    /// <summary>
    /// 初始化包围盒（自动修正浮点精度误差）
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <exception cref="ArgumentException">当 Min 超出 Max 容差范围时抛出</exception>
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
    /// 包围盒尺寸（Max - Min）
    /// </summary>
    public Vector3 Size => _lazySize.Value;

    /// <summary>
    /// 包围盒中心点
    /// </summary>
    public Vector3 Center => _lazyCenter.Value;

    /// <summary>
    /// 判断是否与另一个包围盒相交（考虑浮点精度）
    /// </summary>
    /// <param name="other">另一个包围盒</param>
    /// <returns>相交返回 true，否则 false</returns>
    /// <exception cref="ArgumentNullException">other 为 null 时抛出</exception>
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
    /// 判断是否包含指定点（考虑浮点精度）
    /// </summary>
    /// <param name="point">待判断的点</param>
    /// <returns>包含返回 true，否则 false</returns>
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
    /// 判断是否完全包含另一个包围盒（考虑浮点精度）
    /// </summary>
    /// <param name="other">另一个包围盒</param>
    /// <returns>完全包含返回 true，否则 false</returns>
    /// <exception cref="ArgumentNullException">other 为 null 时抛出</exception>
    public bool Contains(BoundingBox other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Contains(other.Min) && Contains(other.Max);
    }

    /// <summary>
    /// 对包围盒执行矩阵变换（支持投影矩阵的齐次除法）
    /// </summary>
    /// <param name="matrix">变换矩阵</param>
    /// <returns>变换后的新包围盒</returns>
    /// <exception cref="InvalidOperationException">变换结果无效时抛出</exception>
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
    /// 从点集合创建包围盒
    /// </summary>
    /// <param name="points">点集合</param>
    /// <returns>包含所有点的最小包围盒</returns>
    /// <exception cref="ArgumentNullException">points 为 null 时抛出</exception>
    /// <exception cref="InvalidOperationException">points 为空集合时抛出</exception>
    /// <exception cref="ArgumentException">points 包含无效点时抛出</exception>
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
    /// 合并多个包围盒为一个新包围盒
    /// </summary>
    /// <param name="boxes">包围盒集合</param>
    /// <returns>包含所有输入包围盒的最小包围盒</returns>
    /// <exception cref="ArgumentNullException">boxes 为 null 时抛出</exception>
    /// <exception cref="InvalidOperationException">boxes 为空集合时抛出</exception>
    /// <exception cref="ArgumentException">boxes 包含 null 元素时抛出</exception>
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
    /// 检查向量是否包含 NaN 或 Infinity
    /// </summary>
    public static bool IsInvalidVector(Vector3 vec)
    {
        return float.IsNaN(vec.X) || float.IsNaN(vec.Y) || float.IsNaN(vec.Z) ||
               float.IsInfinity(vec.X) || float.IsInfinity(vec.Y) || float.IsInfinity(vec.Z);
    }

    /// <summary>
    /// 检查四维向量是否包含 NaN 或 Infinity
    /// </summary>
    public static bool IsInvalidVector(Vector4 vec)
    {
        return float.IsNaN(vec.X) || float.IsNaN(vec.Y) || float.IsNaN(vec.Z) || float.IsNaN(vec.W) ||
               float.IsInfinity(vec.X) || float.IsInfinity(vec.Y) || float.IsInfinity(vec.Z) || float.IsInfinity(vec.W);
    }

    /// <summary>
    /// 将包围盒沿所有轴扩展指定量，返回新的包围盒。
    /// </summary>
    /// <param name="amount">扩展量（正值扩大，负值收缩）。</param>
    /// <returns>扩展后的新包围盒。</returns>
    public BoundingBox Expand(float amount)
    {
        var expand = new Vector3(amount);
        return new BoundingBox(Min - expand, Max + expand);
    }

    /// <summary>
    /// 比较两个包围盒是否相等（考虑浮点精度）
    /// </summary>
    /// <param name="other">另一个包围盒</param>
    /// <returns>相等返回 true，否则 false</returns>
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
    /// 比较对象是否与当前包围盒相等
    /// </summary>
    /// <param name="obj">待比较的对象</param>
    /// <returns>相等返回 true，否则 false</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as BoundingBox);
    }

    /// <summary>
    /// 获取哈希码（适配浮点精度）
    /// </summary>
    /// <returns>哈希码</returns>
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
    /// 相等运算符重载
    /// </summary>
    public static bool operator ==(BoundingBox? left, BoundingBox? right)
    {
        return EqualityComparer<BoundingBox>.Default.Equals(left, right);
    }


    /// <summary>
    /// 不等运算符重载
    /// </summary>
    public static bool operator !=(BoundingBox? left, BoundingBox? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// 格式化包围盒为字符串
    /// </summary>
    /// <returns>可读的字符串表示</returns>
    public override string ToString()
    {
        return $"BoundingBox(Min=({Min.X:F6}, {Min.Y:F6}, {Min.Z:F6}), " +
               $"Max=({Max.X:F6}, {Max.Y:F6}, {Max.Z:F6}))";
    }

}
