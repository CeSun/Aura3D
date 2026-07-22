using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Aura3D.Core.Geometries;

/// <summary>
/// Represents the sphere geometry type.
/// </summary>
public class SphereGeometry : Geometry
{
    /// <summary>
    /// Gets the radius.
    /// </summary>
    public float Radius { get; }
    /// <summary>
    /// Gets the width segments.
    /// </summary>
    public int WidthSegments { get; }
    /// <summary>
    /// Gets the height segments.
    /// </summary>
    public int HeightSegments { get; }
    /// <summary>
    /// Gets the phi start.
    /// </summary>
    public float PhiStart { get; }
    /// <summary>
    /// Gets the phi length.
    /// </summary>
    public float PhiLength { get; }
    /// <summary>
    /// Gets the theta start.
    /// </summary>
    public float ThetaStart { get; }
    /// <summary>
    /// Gets the theta length.
    /// </summary>
    public float ThetaLength { get; }

    /// <summary>
    /// Initializes a new instance of the sphere geometry type.
    /// </summary>
    public SphereGeometry(
        float radius = 1f,
        int widthSegments = 32,
        int heightSegments = 16,
        float phiStart = 0f,
        float phiLength = MathF.PI * 2f,
        float thetaStart = 0f,
        float thetaLength = MathF.PI)
    {
        if (widthSegments < 3) throw Aura3D.Core.Exceptions.GeometryErrors.MinimumSegmentCount(nameof(widthSegments), 3, widthSegments);
        if (heightSegments < 2) throw Aura3D.Core.Exceptions.GeometryErrors.MinimumSegmentCount(nameof(heightSegments), 2, heightSegments);

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
