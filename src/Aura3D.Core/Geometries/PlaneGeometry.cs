using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System;
using System.Collections.Generic;

namespace Aura3D.Core.Geometries;

/// <summary>
/// 平面几何体，用于创建矩形平面形状的网格数据。
/// </summary>
public class PlaneGeometry : Geometry
{
    /// <summary>
    /// 获取或设置平面的宽度。
    /// </summary>
    public float Width { get; }
    /// <summary>
    /// 获取或设置平面的高度。
    /// </summary>
    public float Height { get; }
    /// <summary>
    /// 获取或设置平面宽度方向的分段数。
    /// </summary>
    public int WidthSegments { get; }
    /// <summary>
    /// 获取或设置平面高度方向的分段数。
    /// </summary>
    public int HeightSegments { get; }

    /// <summary>
    /// 初始化 <see cref="PlaneGeometry"/> 类的新实例。
    /// </summary>
    /// <param name="width">平面的宽度。</param>
    /// <param name="height">平面的高度。</param>
    /// <param name="widthSegments">宽度方向的分段数，必须大于等于1。</param>
    /// <param name="heightSegments">高度方向的分段数，必须大于等于1。</param>
    /// <exception cref="ArgumentOutOfRangeException">当 segments 参数小于 1 时抛出。</exception>
    public PlaneGeometry(float width = 1f, float height = 1f, int widthSegments = 1, int heightSegments = 1)
    {
        if (widthSegments < 1) throw new ArgumentOutOfRangeException(nameof(widthSegments));
        if (heightSegments < 1) throw new ArgumentOutOfRangeException(nameof(heightSegments));

        Width = width;
        Height = height;
        WidthSegments = widthSegments;
        HeightSegments = heightSegments;

        Build();
    }

    void Build()
    {
        int gridX = WidthSegments;
        int gridY = HeightSegments;
        int gridX1 = gridX + 1;
        int gridY1 = gridY + 1;

        float halfWidth = Width / 2f;
        float halfHeight = Height / 2f;
        float segmentWidth = Width / gridX;
        float segmentHeight = Height / gridY;

        var positions = new List<float>(gridX1 * gridY1 * 3);
        var normals = new List<float>(gridX1 * gridY1 * 3);
        var uvs = new List<float>(gridX1 * gridY1 * 2);
        var indices = new List<uint>(gridX * gridY * 6);

        // Build vertices, normals and uvs (plane lies on XZ, normal +Y)
        for (int iy = 0; iy < gridY1; iy++)
        {
            float z = iy * segmentHeight - halfHeight;
            float v = (float)iy / gridY;
            for (int ix = 0; ix < gridX1; ix++)
            {
                float x = ix * segmentWidth - halfWidth;
                float u = (float)ix / gridX;

                // position (x, 0, z)
                positions.Add(x);
                positions.Add(0f);
                positions.Add(z);

                // normal up
                normals.Add(0f);
                normals.Add(1f);
                normals.Add(0f);

                // uv (u, v)
                uvs.Add(u);
                uvs.Add(v);
            }
        }

        // Build indices
        for (int iy = 0; iy < gridY; iy++)
        {
            for (int ix = 0; ix < gridX; ix++)
            {
                uint a = (uint)(ix + gridX1 * iy);
                uint b = (uint)(ix + gridX1 * (iy + 1));
                uint c = (uint)((ix + 1) + gridX1 * (iy + 1));
                uint d = (uint)((ix + 1) + gridX1 * iy);

                // two triangles (a, b, d) and (b, c, d)
                indices.Add(a);
                indices.Add(b);
                indices.Add(d);

                indices.Add(b);
                indices.Add(c);
                indices.Add(d);
            }
        }

        SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normals);
        SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvs);
        SetIndices(indices);

        ModelHelper.CalcVerticsTbn(indices, normals, uvs, out var tangents, out var bitangents);

        SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);

        SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);

    }
}
