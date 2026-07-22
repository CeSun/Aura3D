using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the render pass type.
/// </summary>
public partial class RenderPass
{
    private readonly List<float> _immVerts = new();
    private uint _immVao;
    private uint _immVbo;

    /// <summary>
    /// Performs the begin operation.
    /// </summary>
    protected void Begin()
    {
        _immVerts.Clear();
    }

    /// <summary>
    /// Performs the vertex operation.
    /// </summary>
    protected void Vertex(float x, float y, float z)
    {
        _immVerts.Add(x);
        _immVerts.Add(y);
        _immVerts.Add(z);
    }

    /// <summary>
    /// Performs the vertex operation.
    /// </summary>
    protected void Vertex(Vector3 v)
    {
        _immVerts.Add(v.X);
        _immVerts.Add(v.Y);
        _immVerts.Add(v.Z);
    }

    /// <summary>
    /// Performs the line operation.
    /// </summary>
    protected void Line(Vector3 from, Vector3 to)
    {
        Vertex(from);
        Vertex(to);
    }

    /// <summary>
    /// Performs the line operation.
    /// </summary>
    protected void Line(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        Vertex(x1, y1, z1);
        Vertex(x2, y2, z2);
    }

    /// <summary>
    /// Performs the wire box operation.
    /// </summary>
    protected void WireBox(Vector3 min, Vector3 max)
    {
        float x0 = min.X, y0 = min.Y, z0 = min.Z;
        float x1 = max.X, y1 = max.Y, z1 = max.Z;

        Line(x0, y0, z0, x1, y0, z0);
        Line(x1, y0, z0, x1, y0, z1);
        Line(x1, y0, z1, x0, y0, z1);
        Line(x0, y0, z1, x0, y0, z0);

        Line(x0, y1, z0, x1, y1, z0);
        Line(x1, y1, z0, x1, y1, z1);
        Line(x1, y1, z1, x0, y1, z1);
        Line(x0, y1, z1, x0, y1, z0);

        Line(x0, y0, z0, x0, y1, z0);
        Line(x1, y0, z0, x1, y1, z0);
        Line(x1, y0, z1, x1, y1, z1);
        Line(x0, y0, z1, x0, y1, z1);
    }

    /// <summary>
    /// Performs the wire rect operation.
    /// </summary>
    protected void WireRect(Vector3 center, Vector3 axis1, Vector3 axis2, float size1, float size2)
    {
        var h1 = axis1 * (size1 * 0.5f);
        var h2 = axis2 * (size2 * 0.5f);

        var c0 = center - h1 - h2;
        var c1 = center + h1 - h2;
        var c2 = center + h1 + h2;
        var c3 = center - h1 + h2;

        Line(c0, c1);
        Line(c1, c2);
        Line(c2, c3);
        Line(c3, c0);
    }

    /// <summary>
    /// Performs the circle operation.
    /// </summary>
    protected void Circle(Vector3 center, Vector3 normal, float radius, int segments = 32)
    {
        var (u, v) = GetPlaneBasis(normal);

        for (int i = 0; i < segments; i++)
        {
            float a0 = 2.0f * MathF.PI * i / segments;
            float a1 = 2.0f * MathF.PI * (i + 1) / segments;

            var p0 = center + radius * (MathF.Cos(a0) * u + MathF.Sin(a0) * v);
            var p1 = center + radius * (MathF.Cos(a1) * u + MathF.Sin(a1) * v);

            Line(p0, p1);
        }
    }

    /// <summary>
    /// Performs the end operation.
    /// </summary>
    protected unsafe void End(Resources.PrimitiveType primitiveType = Resources.PrimitiveType.Lines)
    {
        if (_immVerts.Count == 0)
            return;

        if (_immVao == 0)
        {
            _immVao = gl.GenVertexArray();
            _immVbo = gl.GenBuffer();
        }

        gl.BindVertexArray(_immVao);
        gl.BindBuffer(GLEnum.ArrayBuffer, _immVbo);

        var data = _immVerts.ToArray();
        fixed (float* p = data)
        {
            gl.BufferData(GLEnum.ArrayBuffer,
                (nuint)(data.Length * sizeof(float)),
                p, GLEnum.DynamicDraw);
        }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 3 * sizeof(float), (void*)0);

        gl.DrawArrays(GetGLPrimitiveType(primitiveType), 0, (uint)(data.Length / 3));

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);

        _immVerts.Clear();
    }

    /// <summary>
    /// Performs the wire sphere operation.
    /// </summary>
    protected void WireSphere(Vector3 center, float radius, int segments = 24)
    {
        Circle(center, Vector3.UnitX, radius, segments);
        Circle(center, Vector3.UnitY, radius, segments);
        Circle(center, Vector3.UnitZ, radius, segments);
    }

    /// <summary>
    /// Performs the wire cone operation.
    /// </summary>
    protected void WireCone(Vector3 origin, Vector3 direction, float angleRadians, float length, int segments = 16)
    {
        var dir = Vector3.Normalize(direction);
        var baseCenter = origin + dir * length;
        float baseRadius = MathF.Tan(angleRadians) * length;

        var (u, v) = GetPlaneBasis(dir);

        // 底部圆
        for (int i = 0; i < segments; i++)
        {
            float a0 = 2.0f * MathF.PI * i / segments;
            float a1 = 2.0f * MathF.PI * (i + 1) / segments;

            var p0 = baseCenter + baseRadius * (MathF.Cos(a0) * u + MathF.Sin(a0) * v);
            var p1 = baseCenter + baseRadius * (MathF.Cos(a1) * u + MathF.Sin(a1) * v);

            Line(p0, p1);
        }

        // 从顶点到底部圆的连线（每 90 度一条）
        for (int i = 0; i < 4; i++)
        {
            float a = 2.0f * MathF.PI * i / 4;
            var p = baseCenter + baseRadius * (MathF.Cos(a) * u + MathF.Sin(a) * v);
            Line(origin, p);
        }
    }

    /// <summary>
    /// Performs the wire arrow operation.
    /// </summary>
    protected void WireArrow(Vector3 from, Vector3 to, float headSize = 0.15f)
    {
        var dir = Vector3.Normalize(to - from);
        float totalLength = Vector3.Distance(from, to);

        Line(from, to);

        var (perp1, perp2) = GetPlaneBasis(dir);
        var basePt = to - dir * headSize;
        float w = headSize * 0.35f;

        Line(to, basePt + perp1 * w);
        Line(to, basePt - perp1 * w);
        Line(to, basePt + perp2 * w);
        Line(to, basePt - perp2 * w);
    }

    /// <summary>
    /// Initializes a new instance of the static type.
    /// </summary>
    protected static (Vector3 u, Vector3 v) GetPlaneBasis(Vector3 normal)
    {
        var n = Vector3.Normalize(normal);

        Vector3 u;
        if (MathF.Abs(n.Y) < 0.9f)
            u = Vector3.Normalize(Vector3.Cross(n, Vector3.UnitY));
        else
            u = Vector3.Normalize(Vector3.Cross(n, Vector3.UnitX));

        var v = Vector3.Cross(n, u);
        return (u, v);
    }
}
