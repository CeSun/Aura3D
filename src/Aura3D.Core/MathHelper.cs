using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Math;

public static class MathHelper
{
    public static float DegreeToRadians(this float degree)
    {
        return (MathF.PI / 180) * degree;
    }
    public static float RadiansToDegree(this float radians)
    {
        return radians / (MathF.PI / 180);
    }

    public static Vector3 ToEulerAngles(this Quaternion q)
    {
        float pitch = MathF.Asin(2 * (q.W * q.X - q.Y * q.Z));

        float yaw = MathF.Atan2(2 * (q.W * q.Y + q.X * q.Z),
                                      1 - 2 * (q.X * q.X + q.Y * q.Y));

        float roll = MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.X * q.X + q.Z * q.Z));

        return new Vector3(pitch, yaw, roll);
    }


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
    public static Quaternion Rotation(this Matrix4x4 matrix) => Quaternion.CreateFromRotationMatrix(matrix.RotationMatrix4x4());

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

    public static Vector3 XYZ(this Vector4 vector4)
    {
        return new Vector3(vector4.X, vector4.Y, vector4.Z);
    }

    public static Vector2 XY (this Vector4 vector4)
    {
        return new Vector2(vector4.X, vector4.Y);
    }

    public static Matrix4x4 Inverse(this Matrix4x4 m)
    {
        Matrix4x4.Invert(m, out var r);
        return r;
    }

    public static Vector3 ForwardVector(this Matrix4x4 m)
    {
        var vector = new Vector3(0, 0, -1);

        return Vector3.Transform(vector, m.RotationMatrix4x4());
    }

    public static Vector3 RightVector(this Matrix4x4 m)
    {
        var vector = new Vector3(1, 0, 0);
        return Vector3.Transform(vector, m.RotationMatrix4x4());
    }
    public static Vector3 UpVector(this Matrix4x4 m)
    {
        var vector = new Vector3(0, 1, 0);
        return Vector3.Transform(vector, m.RotationMatrix4x4());
    }
    public static Color ToColor(this Vector4 vector4)
    {
        return Color.FromArgb((int)(vector4.W * 255), (int)(vector4.X * 255), (int)(vector4.Y * 255), (int)(vector4.Z * 255));
    }
    public static Vector4 ToVector4(this Color color)
    {
        return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }
}

public static class MatrixHelper
{
    public static Matrix4x4 CreateTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {

        var positionMatrix = Matrix4x4.CreateTranslation(position);
        var rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
        var scaleMatrix = Matrix4x4.CreateScale(scale);
        return scaleMatrix * rotationMatrix * positionMatrix;
    }
}
