using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Aura3D.Core.Geometries;

/// <summary>
/// 球体几何体，用于创建球体形状的网格数据。
/// </summary>
public class SphereGeometry : Geometry
{
    /// <summary>
    /// 获取或设置球体的半径。
    /// </summary>
    public float Radius { get; }
    /// <summary>
    /// 获取或设置球体宽度方向的分段数。
    /// </summary>
    public int WidthSegments { get; }
    /// <summary>
    /// 获取或设置球体高度方向的分段数。
    /// </summary>
    public int HeightSegments { get; }
    /// <summary>
    /// 获取或设置球体纬度方向的起始角度（弧度）。
    /// </summary>
    public float PhiStart { get; }
    /// <summary>
    /// 获取或设置球体纬度方向的总角度长度（弧度）。
    /// </summary>
    public float PhiLength { get; }
    /// <summary>
    /// 获取或设置球体经度方向的起始角度（弧度）。
    /// </summary>
    public float ThetaStart { get; }
    /// <summary>
    /// 获取或设置球体经度方向的总角度长度（弧度）。
    /// </summary>
    public float ThetaLength { get; }

    /// <summary>
    /// 初始化 <see cref="SphereGeometry"/> 类的新实例。
    /// </summary>
    /// <param name="radius">球体的半径。</param>
    /// <param name="widthSegments">宽度方向的分段数，必须大于等于3。</param>
    /// <param name="heightSegments">高度方向的分段数，必须大于等于2。</param>
    /// <param name="phiStart">纬度方向的起始角度（弧度）。</param>
    /// <param name="phiLength">纬度方向的总角度长度（弧度）。</param>
    /// <param name="thetaStart">经度方向的起始角度（弧度）。</param>
    /// <param name="thetaLength">经度方向的总角度长度（弧度）。</param>
    /// <exception cref="ArgumentOutOfRangeException">当 widthSegments 小于 3 时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">当 heightSegments 小于 2 时抛出。</exception>
    public SphereGeometry(
        float radius = 1f,
        int widthSegments = 32,
        int heightSegments = 16,
        float phiStart = 0f,
        float phiLength = MathF.PI * 2f,
        float thetaStart = 0f,
        float thetaLength = MathF.PI)
    {
        if (widthSegments < 3) throw new ArgumentOutOfRangeException(nameof(widthSegments), "widthSegments must be >= 3");
        if (heightSegments < 2) throw new ArgumentOutOfRangeException(nameof(heightSegments), "heightSegments must be >= 2");

        Radius = radius;
        WidthSegments = widthSegments;
        HeightSegments = heightSegments;
        PhiStart = phiStart;
        PhiLength = phiLength;
        ThetaStart = thetaStart;
        ThetaLength = thetaLength;

        Build();
    }

    void Build()
    {
        int xSegments = WidthSegments;
        int ySegments = HeightSegments;
        int vertexCount = (xSegments + 1) * (ySegments + 1);

        var positions = new List<float>(vertexCount * 3);
        var normals = new List<float>(vertexCount * 3);
        var uvs = new List<float>(vertexCount * 2);
        var indices = new List<uint>(xSegments * ySegments * 6);

        // Generate vertices
        for (int y = 0; y <= ySegments; y++)
        {
            float v = (float)y / ySegments;
            float theta = ThetaStart + v * ThetaLength;

            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            for (int x = 0; x <= xSegments; x++)
            {
                float u = (float)x / xSegments;
                float phi = PhiStart + u * PhiLength;

                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                // Cartesian coordinates (unit sphere scaled by Radius)
                float px = Radius * sinTheta * cosPhi;
                float py = Radius * cosTheta;
                float pz = Radius * sinTheta * sinPhi;

                positions.Add(px);
                positions.Add(py);
                positions.Add(pz);

                // normal = normalized position (for sphere centered at origin), outward facing
                var n = new Vector3(px, py, pz);
                if (n.LengthSquared() > 0f) n = Vector3.Normalize(n);
                normals.Add(n.X);
                normals.Add(n.Y);
                normals.Add(n.Z);

                // uv
                uvs.Add(u);
                uvs.Add(1f - v); // flip V so top is v=1
            }
        }

        // Generate indices
        for (int y = 0; y < ySegments; y++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                uint a = (uint)(x + (xSegments + 1) * y);
                uint b = (uint)(x + (xSegments + 1) * (y + 1));
                uint c = (uint)(x + 1 + (xSegments + 1) * (y + 1));
                uint d = (uint)(x + 1 + (xSegments + 1) * y);

                // two triangles (a, d, b) and (b, d, c) — CCW from outside
                indices.Add(a);
                indices.Add(d);
                indices.Add(b);

                indices.Add(b);
                indices.Add(d);
                indices.Add(c);
            }
        }

        SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normals);
        SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvs);
        SetIndices(indices);

        // 计算切线与副切线（与 BoxGeometry 保持一致的调用顺序）
        ModelHelper.CalcVerticsTbn(indices, normals, uvs, out var tangents, out var bitangents);

        SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);
        SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);

    }
}
