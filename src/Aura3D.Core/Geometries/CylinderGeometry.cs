using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Aura3D.Core.Geometries;

/// <summary>
/// 圆柱体几何体，用于创建圆柱体形状的网格数据。
/// </summary>
public class CylinderGeometry : Geometry
{
    /// <summary>
    /// 获取或设置圆柱体顶部的半径。
    /// </summary>
    public float RadiusTop { get; }
    /// <summary>
    /// 获取或设置圆柱体底部的半径。
    /// </summary>
    public float RadiusBottom { get; }
    /// <summary>
    /// 获取或设置圆柱体的高度。
    /// </summary>
    public float Height { get; }
    /// <summary>
    /// 获取或设置圆柱体的径向分段数（圆周方向的分段数）。
    /// </summary>
    public int RadialSegments { get; }
    /// <summary>
    /// 获取或设置圆柱体的高度分段数。
    /// </summary>
    public int HeightSegments { get; }
    /// <summary>
    /// 获取或设置一个值，指示圆柱体是否开放（无顶面和底面）。
    /// </summary>
    public bool OpenEnded { get; }
    /// <summary>
    /// 获取或设置圆柱体圆周方向的起始角度。
    /// </summary>
    public float ThetaStart { get; }
    /// <summary>
    /// 获取或设置圆柱体圆周方向的总角度长度。
    /// </summary>
    public float ThetaLength { get; }

    /// <summary>
    /// 初始化 <see cref="CylinderGeometry"/> 类的新实例。
    /// </summary>
    /// <param name="radiusTop">圆柱体顶部的半径。</param>
    /// <param name="radiusBottom">圆柱体底部的半径。</param>
    /// <param name="height">圆柱体的高度。</param>
    /// <param name="radialSegments">径向分段数，必须大于等于3。</param>
    /// <param name="heightSegments">高度分段数，必须大于等于1。</param>
    /// <param name="openEnded">是否开放（无顶面和底面）。</param>
    /// <param name="thetaStart">圆周方向的起始角度（弧度）。</param>
    /// <param name="thetaLength">圆周方向的总角度长度（弧度）。</param>
    /// <exception cref="ArgumentOutOfRangeException">当 radialSegments 小于 3 时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">当 heightSegments 小于 1 时抛出。</exception>
    public CylinderGeometry(
        float radiusTop = 1f,
        float radiusBottom = 1f,
        float height = 1f,
        int radialSegments = 32,
        int heightSegments = 1,
        bool openEnded = false,
        float thetaStart = 0f,
        float thetaLength = MathF.PI * 2f)
    {
        if (radialSegments < 3) throw new ArgumentOutOfRangeException(nameof(radialSegments), "radialSegments must be >= 3");
        if (heightSegments < 1) throw new ArgumentOutOfRangeException(nameof(heightSegments), "heightSegments must be >= 1");

        RadiusTop = radiusTop;
        RadiusBottom = radiusBottom;
        Height = height;
        RadialSegments = radialSegments;
        HeightSegments = heightSegments;
        OpenEnded = openEnded;
        ThetaStart = thetaStart;
        ThetaLength = thetaLength;

        Build();
    }

    void Build()
    {
        float halfHeight = Height / 2f;
        int index = 0;
        var indexArray = new List<List<int>>(HeightSegments + 1);

        var positions = new List<float>();
        var normals = new List<float>();
        var uvs = new List<float>();
        var indices = new List<uint>();

        // generate vertices, normals and uvs
        for (int y = 0; y <= HeightSegments; y++)
        {
            var indexRow = new List<int>();
            float v = (float)y / HeightSegments;
            // Replace MathF.Lerp with explicit linear interpolation to avoid missing API
            float radius = RadiusTop + v * (RadiusBottom - RadiusTop);
            float py = v * Height - halfHeight;

            for (int x = 0; x <= RadialSegments; x++)
            {
                float u = (float)x / RadialSegments;
                float theta = ThetaStart + u * ThetaLength;

                float sin = MathF.Sin(theta);
                float cos = MathF.Cos(theta);

                float px = radius * sin;
                float pz = radius * cos;

                // position
                positions.Add(px);
                positions.Add(py);
                positions.Add(pz);

                // compute normal (consider slope between top and bottom), outward facing
                float slope = (RadiusBottom - RadiusTop) / Height; // rise over run
                var normal = new Vector3(sin, slope, cos);
                if (normal.LengthSquared() > 0f) normal = Vector3.Normalize(normal);
                normals.Add(normal.X);
                normals.Add(normal.Y);
                normals.Add(normal.Z);

                // uv
                uvs.Add(u);
                uvs.Add(1f - v);

                indexRow.Add(index++);
            }

            indexArray.Add(indexRow);
        }

        // generate indices for the sides
        for (int y = 0; y < HeightSegments; y++)
        {
            for (int x = 0; x < RadialSegments; x++)
            {
                uint a = (uint)indexArray[y][x];
                uint b = (uint)indexArray[y + 1][x];
                uint c = (uint)indexArray[y + 1][x + 1];
                uint d = (uint)indexArray[y][x + 1];

                // two triangles (a, d, b) and (b, d, c) — CCW from outside
                indices.Add(a);
                indices.Add(d);
                indices.Add(b);

                indices.Add(b);
                indices.Add(d);
                indices.Add(c);
            }
        }

        // generate caps if not openEnded
        if (!OpenEnded)
        {
            // top cap — normal +Y (outward)
            if (RadiusTop > 0f)
            {
                int startIndex = index;
                // rim
                for (int x = 0; x < RadialSegments; x++)
                {
                    float u = (float)x / RadialSegments;
                    float theta = ThetaStart + u * ThetaLength;
                    float sin = MathF.Sin(theta);
                    float cos = MathF.Cos(theta);

                    float px = RadiusTop * sin;
                    float pz = RadiusTop * cos;
                    float py = halfHeight;

                    positions.Add(px); positions.Add(py); positions.Add(pz);
                    normals.Add(0f); normals.Add(1f); normals.Add(0f);
                    uvs.Add((sin * 0.5f) + 0.5f); uvs.Add((cos * 0.5f) + 0.5f);
                    index++;
                }

                // center
                int centerIndex = index++;
                positions.Add(0f); positions.Add(halfHeight); positions.Add(0f);
                normals.Add(0f); normals.Add(1f); normals.Add(0f);
                uvs.Add(0.5f); uvs.Add(0.5f);

                for (int x = 0; x < RadialSegments; x++)
                {
                    uint i1 = (uint)(startIndex + x);
                    uint i2 = (uint)(startIndex + ((x + 1) % RadialSegments));
                    // triangle (center, i1, i2) — CCW from above (outward normal +Y)
                    indices.Add((uint)centerIndex);
                    indices.Add(i1);
                    indices.Add(i2);
                }
            }

            // bottom cap (y = HeightSegments) — normal -Y
            if (RadiusBottom > 0f)
            {
                int startIndex = index;
                // rim
                for (int x = 0; x < RadialSegments; x++)
                {
                    float u = (float)x / RadialSegments;
                    float theta = ThetaStart + u * ThetaLength;
                    float sin = MathF.Sin(theta);
                    float cos = MathF.Cos(theta);

                    float px = RadiusBottom * sin;
                    float pz = RadiusBottom * cos;
                    float py = -halfHeight;

                    positions.Add(px); positions.Add(py); positions.Add(pz);
                    normals.Add(0f); normals.Add(-1f); normals.Add(0f);
                    uvs.Add((sin * 0.5f) + 0.5f); uvs.Add((cos * 0.5f) + 0.5f);
                    index++;
                }

                // center
                int centerIndex = index++;
                positions.Add(0f); positions.Add(-halfHeight); positions.Add(0f);
                normals.Add(0f); normals.Add(-1f); normals.Add(0f);
                uvs.Add(0.5f); uvs.Add(0.5f);

                for (int x = 0; x < RadialSegments; x++)
                {
                    uint i1 = (uint)(startIndex + x);
                    uint i2 = (uint)(startIndex + ((x + 1) % RadialSegments));
                    // triangle (center, i2, i1) — CCW from below (outward normal -Y)
                    indices.Add((uint)centerIndex);
                    indices.Add(i2);
                    indices.Add(i1);
                }
            }
        }

        // apply to base Geometry
        SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normals);
        SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvs);
        SetIndices(indices);

        // calc tangents/bitangents (保持与项目中其他几何体一致的调用方式)
        ModelHelper.CalcVerticsTbn(indices, normals, uvs, out var tangents, out var bitangents);

        SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);
        SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);

    }
}
