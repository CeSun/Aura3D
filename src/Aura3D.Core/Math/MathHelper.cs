using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Math;

/// <summary>
/// Represents the math helper type.
/// </summary>
public static class MathHelper
{
    /// <summary>
    /// Defines the degree to rad value.
    /// </summary>
    public const float DegreeToRad = MathF.PI / 180;
    /// <summary>
    /// Defines the rad to degree value.
    /// </summary>
    public const float RadToDegree = 180 / MathF.PI;

    /// <summary>
    /// Performs the degree to radians operation.
    /// </summary>
    public static float DegreeToRadians(this float degree)
    {
        return degree * DegreeToRad;
    }
    /// <summary>
    /// Performs the radians to degree operation.
    /// </summary>
    public static float RadiansToDegree(this float radians)
    {
        return radians * RadToDegree;
    }

    /// <summary>
    /// Performs the to euler angles operation.
    /// </summary>
    public static Vector3 ToEulerAngles(this Quaternion q)
    {
        float pitch = MathF.Asin(2 * (q.W * q.X - q.Y * q.Z));
        float yaw = MathF.Atan2(2 * (q.W * q.Y + q.X * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y));
        float roll = MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.X * q.X + q.Z * q.Z));

        return new Vector3(pitch, yaw, roll);
    }


    /// <summary>
    /// Performs the scale operation.
    /// </summary>
    public static Vector3 Scale(this Matrix4x4 matrix)
    {
        var Vector1 = new Vector3()
        {
            X = matrix.M11,
            Y = matrix.M21,
            Z = matrix.M31,
        };
        var Vector2 = new Vector3()
        {
            X = matrix.M12,
            Y = matrix.M22,
            Z = matrix.M32,
        };
        var Vector3 = new Vector3()
        {
            X = matrix.M13,
            Y = matrix.M23,
            Z = matrix.M33,
        };
        return new Vector3
        {
            X = Vector1.Length() / 1.0f,
            Y = Vector2.Length() / 1.0f,
            Z = Vector3.Length() / 1.0f
        };
    }
    /// <summary>
    /// Performs the rotation operation.
    /// </summary>
    public static Quaternion Rotation(this Matrix4x4 matrix) => Quaternion.CreateFromRotationMatrix(matrix.RotationMatrix4x4());

    /// <summary>
    /// Performs the rotation matrix4x4 operation.
    /// </summary>
    public static Matrix4x4 RotationMatrix4x4(this Matrix4x4 matrix)
    {
        var vector1 = new Vector3()
        {
            X = matrix.M11,
            Y = matrix.M21,
            Z = matrix.M31,
        };
        vector1 = Vector3.Normalize(vector1);
        var vector2 = new Vector3()
        {
            X = matrix.M12,
            Y = matrix.M22,
            Z = matrix.M32,
        };
        vector2 = Vector3.Normalize(vector2);
        var vector3 = new Vector3()
        {
            X = matrix.M13,
            Y = matrix.M23,
            Z = matrix.M33,
        };
        vector3 = Vector3.Normalize(vector3);

        return new Matrix4x4
        {
            M11 = vector1.X,
            M21 = vector1.Y,
            M31 = vector1.Z,
            M12 = vector2.X,
            M22 = vector2.Y,
            M32 = vector2.Z,
            M13 = vector3.X,
            M23 = vector3.Y,
            M33 = vector3.Z,
        };

    }

    /// <summary>
    /// Performs the xyz operation.
    /// </summary>
    public static Vector3 XYZ(this Vector4 vector4)
    {
        return new Vector3(vector4.X, vector4.Y, vector4.Z);
    }

    /// <summary>
    /// Performs the xy operation.
    /// </summary>
    public static Vector2 XY(this Vector4 vector4)
    {
        return new Vector2(vector4.X, vector4.Y);
    }

    /// <summary>
    /// Performs the inverse operation.
    /// </summary>
    public static Matrix4x4 Inverse(this Matrix4x4 m)
    {
        Matrix4x4.Invert(m, out var r);
        return r;
    }

    /// <summary>
    /// Performs the forward vector operation.
    /// </summary>
    public static Vector3 ForwardVector(this Matrix4x4 m)
    {
        var vector = new Vector3(0, 0, -1);

        return Vector3.Transform(vector, m.RotationMatrix4x4());
    }

    /// <summary>
    /// Performs the right vector operation.
    /// </summary>
    public static Vector3 RightVector(this Matrix4x4 m)
    {
        var vector = new Vector3(1, 0, 0);
        return Vector3.Transform(vector, m.RotationMatrix4x4());
    }

    /// <summary>
    /// Performs the up vector operation.
    /// </summary>
    public static Vector3 UpVector(this Matrix4x4 m)
    {
        var vector = new Vector3(0, 1, 0);
        return Vector3.Transform(vector, m.RotationMatrix4x4());
    }

    /// <summary>
    /// Performs the to color operation.
    /// </summary>
    public static Color ToColor(this Vector4 vector4)
    {
        return Color.FromArgb((int)(vector4.W * 255), (int)(vector4.X * 255), (int)(vector4.Y * 255), (int)(vector4.Z * 255));
    }

    /// <summary>
    /// Performs the to vector4 operation.
    /// </summary>
    public static Vector4 ToVector4(this Color color)
    {
        return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }
}

/// <summary>
/// Represents the matrix helper type.
/// </summary>
public static class MatrixHelper
{
    /// <summary>
    /// Creates the transform.
    /// </summary>
    public static Matrix4x4 CreateTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {

        // 1. 预计算四元数常用项（减少重复计算）
        float x = rotation.X, y = rotation.Y, z = rotation.Z, w = rotation.W;
        float xx = x * x, yy = y * y, zz = z * z;
        float xy = x * y, xz = x * z, yz = y * z;
        float wx = w * x, wy = w * y, wz = w * z;

        // 2. 预取缩放值
        float sx = scale.X, sy = scale.Y, sz = scale.Z;

        // 3. 直接构建最终矩阵（核心：旋转×缩放 合并计算 + 平移）
        Matrix4x4 result = default;

        // 旋转 + 缩放 合并行（无乘法，直接公式）
        result.M11 = (1 - 2 * (yy + zz)) * sx;
        result.M12 = (2 * (xy + wz)) * sx;
        result.M13 = (2 * (xz - wy)) * sx;
        result.M14 = 0;

        result.M21 = (2 * (xy - wz)) * sy;
        result.M22 = (1 - 2 * (xx + zz)) * sy;
        result.M23 = (2 * (yz + wx)) * sy;
        result.M24 = 0;

        result.M31 = (2 * (xz + wy)) * sz;
        result.M32 = (2 * (yz - wx)) * sz;
        result.M33 = (1 - 2 * (xx + yy)) * sz;
        result.M34 = 0;

        // 平移（直接赋值）
        result.M41 = position.X;
        result.M42 = position.Y;
        result.M43 = position.Z;
        result.M44 = 1;

        return result;
    }


    /// <summary>
    /// Performs the extract planes operation.
    /// </summary>
    public static void ExtractPlanes(Matrix4x4 viewProj, Span<Plane> planes)
    {
        // 从视图投影矩阵中提取裁剪平面
        Vector4 col1 = new Vector4(viewProj.M11, viewProj.M21, viewProj.M31, viewProj.M41);
        Vector4 col2 = new Vector4(viewProj.M12, viewProj.M22, viewProj.M32, viewProj.M42);
        Vector4 col3 = new Vector4(viewProj.M13, viewProj.M23, viewProj.M33, viewProj.M43);
        Vector4 col4 = new Vector4(viewProj.M14, viewProj.M24, viewProj.M34, viewProj.M44);


        planes[0] = NormalizePlane(new Plane(new Vector3(col4.X + col1.X, col4.Y + col1.Y, col4.Z + col1.Z), col4.W + col1.W)); // Left
        planes[1] = NormalizePlane(new Plane(new Vector3(col4.X - col1.X, col4.Y - col1.Y, col4.Z - col1.Z), col4.W - col1.W)); // Right
        planes[2] = NormalizePlane(new Plane(new Vector3(col4.X + col2.X, col4.Y + col2.Y, col4.Z + col2.Z), col4.W + col2.W)); // Bottom
        planes[3] = NormalizePlane(new Plane(new Vector3(col4.X - col2.X, col4.Y - col2.Y, col4.Z - col2.Z), col4.W - col2.W)); // Top
        planes[4] = NormalizePlane(new Plane(new Vector3(col4.X + col3.X, col4.Y + col3.Y, col4.Z + col3.Z), col4.W + col3.W)); // Near
        planes[5] = NormalizePlane(new Plane(new Vector3(col4.X - col3.X, col4.Y - col3.Y, col4.Z - col3.Z), col4.W - col3.W)); // Far

    }

    /// <summary>
    /// Performs the normalize plane operation.
    /// </summary>
    private static Plane NormalizePlane(Plane p)
    {
        float length = p.Normal.Length();
        return new Plane(p.Normal / length, p.D / length);
    }
}
