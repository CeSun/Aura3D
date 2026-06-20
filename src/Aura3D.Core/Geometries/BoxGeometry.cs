using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Geometries;

/// <summary>
/// 长方体几何体，用于创建立方体形状的网格数据。
/// </summary>
public class BoxGeometry : Geometry
{
    /// <summary>
    /// 获取或设置长方体的宽度。
    /// </summary>
    public float Width { get; }
    /// <summary>
    /// 获取或设置长方体的高度。
    /// </summary>
    public float Height { get; }
    /// <summary>
    /// 获取或设置长方体的深度。
    /// </summary>
    public float Depth { get; }

    /// <summary>
    /// 初始化 <see cref="BoxGeometry"/> 类的新实例。
    /// </summary>
    /// <param name="width">长方体的宽度。</param>
    /// <param name="height">长方体的高度。</param>
    /// <param name="depth">长方体的深度。</param>
    public BoxGeometry(float width = 1f, float height = 1f, float depth = 1f)
    {
        Width = width;
        Height = height;
        Depth = depth;

        Build();
    }

    void Build()
    {
        float hx = Width / 2f;
        float hy = Height / 2f;
        float hz = Depth / 2f;

        // 每个面使用 4 个顶点（为每个面的法线和 UV 保持独立），共 6 个面 => 24 顶点
        var positions = new List<float>(24 * 3);
        var normals = new List<float>(24 * 3);
        var uvs = new List<float>(24 * 2);
        var indices = new List<uint>(36);

        // Helper to append a face (4 verts)
        void AddFace(
            (float x, float y, float z) v0,
            (float x, float y, float z) v1,
            (float x, float y, float z) v2,
            (float x, float y, float z) v3,
            (float x, float y, float z) normal)
        {
            uint baseIndex = (uint)(positions.Count / 3);

            positions.AddRange([v0.x, v0.y, v0.z]);
            positions.AddRange([v1.x, v1.y, v1.z]);
            positions.AddRange([v2.x, v2.y, v2.z]);
            positions.AddRange([v3.x, v3.y, v3.z]);

            for (int i = 0; i < 4; i++)
            {
                normals.AddRange(new[] { normal.x, normal.y, normal.z });
            }

            // 标准四边 UV
            uvs.AddRange([0f, 0f]);
            uvs.AddRange([1f, 0f]);
            uvs.AddRange([1f, 1f]);
            uvs.AddRange([0f, 1f]);

            // 两个三角形（保证面对外的逆时针/正向）
            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);

            indices.Add(baseIndex);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }

        // +Z 前面
        AddFace(
            (-hx, -hy, hz),
            ( hx, -hy, hz),
            ( hx,  hy, hz),
            (-hx,  hy, hz),
            (0f, 0f, 1f)
        );

        // -Z 后面
        AddFace(
            ( hx, -hy, -hz),
            (-hx, -hy, -hz),
            (-hx,  hy, -hz),
            ( hx,  hy, -hz),
            (0f, 0f, -1f)
        );

        // +X 右面
        AddFace(
            ( hx, -hy, hz),
            ( hx, -hy, -hz),
            ( hx,  hy, -hz),
            ( hx,  hy, hz),
            (1f, 0f, 0f)
        );

        // -X 左面
        AddFace(
            (-hx, -hy, -hz),
            (-hx, -hy, hz),
            (-hx,  hy, hz),
            (-hx,  hy, -hz),
            (-1f, 0f, 0f)
        );

        // +Y 顶面
        AddFace(
            (-hx, hy, hz),
            ( hx, hy, hz),
            ( hx, hy, -hz),
            (-hx, hy, -hz),
            (0f, 1f, 0f)
        );

        // -Y 底面
        AddFace(
            (-hx, -hy, -hz),
            ( hx, -hy, -hz),
            ( hx, -hy, hz),
            (-hx, -hy, hz),
            (0f, -1f, 0f)
        );

        // 将数据设置到基类 Geometry
        SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normals);
        SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvs);

        SetIndices(indices);

        ModelHelper.CalcVerticsTbn(indices, normals, uvs, out var tangents, out var bitangents);

        SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);

        SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);

    }

    // 若需要，可返回一个浅拷贝（保留引用）或深拷贝方法；基类已提供 Clone/DeepClone。
}
