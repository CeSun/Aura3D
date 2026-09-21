using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Nodes;

public class ModelHelperTests
{
    private static readonly uint[] QuadIndices = [0u, 1u, 2u, 0u, 2u, 3u];

    // XY 平面上的四边形，法线统一为 +Z，UV 的 u 沿 +Y 增长、v 沿 +X 增长。
    private static readonly Vector3[] PlanePositions =
    [
        new Vector3(0f, 0f, 0f),
        new Vector3(0f, 1f, 0f),
        new Vector3(1f, 1f, 0f),
        new Vector3(1f, 0f, 0f)
    ];

    [Fact]
    public void CalcVerticsTbn_ShouldUsePositionsInsteadOfNormals()
    {
        // 若把法线当作位置使用，同一平面上的位置差恒为零，切线会退化成兜底值 (1,0,0)。
        var geometry = CreateGeometry(
            PlanePositions,
            [new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)],
            new Vector3(0f, 0f, 1f));

        var tangents = GetAttribute(geometry, BuildInVertexAttribute.Tangent);

        for (int i = 0; i < PlanePositions.Length; i++)
        {
            AssertVectorEqual(new Vector3(0f, 1f, 0f), VertexAt(tangents, i));
        }
    }

    [Fact]
    public void CalcVerticsTbn_ShouldPreserveUvHandedness()
    {
        // 同一四边形，UV 手性为负（u 沿 +Y、v 沿 +X 时 N × T 指向 -X）。
        // 副切线必须跟随 UV 的 v 方向 (1,0,0)，而不是无条件取 N × T 的正方向。
        var geometry = CreateGeometry(
            PlanePositions,
            [new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)],
            new Vector3(0f, 0f, 1f));

        var bitangents = GetAttribute(geometry, BuildInVertexAttribute.Bitangent);

        for (int i = 0; i < PlanePositions.Length; i++)
        {
            AssertVectorEqual(new Vector3(1f, 0f, 0f), VertexAt(bitangents, i));
        }
    }

    [Fact]
    public void CalcVerticsTbn_ShouldKeepRightHandedBasisForStandardUvs()
    {
        // 标准 UV（u 沿 +X、v 沿 +Y）必须得到右手系切空间，不能被手性修正方向搞反。
        var geometry = CreateGeometry(
            PlanePositions,
            [new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f)],
            new Vector3(0f, 0f, 1f));

        var normals = GetAttribute(geometry, BuildInVertexAttribute.Normal);
        var tangents = GetAttribute(geometry, BuildInVertexAttribute.Tangent);
        var bitangents = GetAttribute(geometry, BuildInVertexAttribute.Bitangent);

        for (int i = 0; i < PlanePositions.Length; i++)
        {
            var n = VertexAt(normals, i);
            var t = VertexAt(tangents, i);
            var b = VertexAt(bitangents, i);

            AssertVectorEqual(new Vector3(1f, 0f, 0f), t);
            AssertVectorEqual(new Vector3(0f, 1f, 0f), b);
            Assert.True(Vector3.Dot(Vector3.Cross(n, t), b) > 0f, "标准 UV 应保持右手系。");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CalcVerticsTbn_ShouldProduceOrthonormalBasis(int geometryKind)
    {
        Geometry geometry = geometryKind == 0 ? new BoxGeometry() : new SphereGeometry();

        var normals = GetAttribute(geometry, BuildInVertexAttribute.Normal);
        var tangents = GetAttribute(geometry, BuildInVertexAttribute.Tangent);
        var bitangents = GetAttribute(geometry, BuildInVertexAttribute.Bitangent);

        var vertexCount = normals.Count / 3;
        Assert.True(vertexCount > 0);
        Assert.Equal(vertexCount * 3, tangents.Count);
        Assert.Equal(vertexCount * 3, bitangents.Count);

        for (int i = 0; i < vertexCount; i++)
        {
            var n = VertexAt(normals, i);
            var t = VertexAt(tangents, i);
            var b = VertexAt(bitangents, i);

            Assert.True(IsFinite(t), "切线包含非有限值。");
            Assert.True(IsFinite(b), "副切线包含非有限值。");

            Assert.InRange(t.Length(), 0.999f, 1.001f);
            Assert.InRange(b.Length(), 0.999f, 1.001f);
            Assert.InRange(MathF.Abs(Vector3.Dot(n, t)), 0f, 1e-3f);
            Assert.InRange(MathF.Abs(Vector3.Dot(n, b)), 0f, 1e-3f);
            Assert.InRange(MathF.Abs(Vector3.Dot(t, b)), 0f, 1e-3f);
        }
    }

    [Fact]
    public void CalcVerticsTbn_ShouldStayOrthonormalWhenUvsAreDegenerate()
    {
        // YZ 平面上的三角形，法线 +X，三个顶点 UV 完全相同 → 无法从 UV 推导方向。
        // 旧实现对退化输入回退到 (1,0,0)，与法线平行，切空间整体失效。
        var geometry = CreateGeometry(
            [new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f)],
            [new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f)],
            new Vector3(1f, 0f, 0f),
            [0u, 1u, 2u]);

        var normals = GetAttribute(geometry, BuildInVertexAttribute.Normal);
        var tangents = GetAttribute(geometry, BuildInVertexAttribute.Tangent);
        var bitangents = GetAttribute(geometry, BuildInVertexAttribute.Bitangent);

        for (int i = 0; i < 3; i++)
        {
            var n = VertexAt(normals, i);
            var t = VertexAt(tangents, i);
            var b = VertexAt(bitangents, i);

            Assert.InRange(t.Length(), 0.999f, 1.001f);
            Assert.InRange(b.Length(), 0.999f, 1.001f);
            Assert.InRange(MathF.Abs(Vector3.Dot(n, t)), 0f, 1e-3f);
            Assert.InRange(MathF.Abs(Vector3.Dot(t, b)), 0f, 1e-3f);
        }
    }

    private static Geometry CreateGeometry(Vector3[] positions, Vector2[] uvs, Vector3 normal, uint[]? indices = null)
    {
        var geometry = new Geometry();

        var positionData = positions.SelectMany(p => new[] { p.X, p.Y, p.Z }).ToList();
        var normalData = positions.SelectMany(_ => new[] { normal.X, normal.Y, normal.Z }).ToList();
        var uvData = uvs.SelectMany(uv => new[] { uv.X, uv.Y }).ToList();

        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positionData);
        geometry.SetVertexAttribute(BuildInVertexAttribute.Normal, 3, normalData);
        geometry.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, uvData);
        geometry.SetIndices(indices ?? QuadIndices);

        ModelHelper.CalcVerticsTbn(geometry.Indices, positionData, normalData, uvData, out var tangents, out var bitangents);
        geometry.SetVertexAttribute(BuildInVertexAttribute.Tangent, 3, tangents);
        geometry.SetVertexAttribute(BuildInVertexAttribute.Bitangent, 3, bitangents);

        return geometry;
    }

    private static IReadOnlyList<float> GetAttribute(Geometry geometry, BuildInVertexAttribute attribute)
    {
        var data = geometry.GetAttributeData(attribute);
        Assert.NotNull(data);
        return data!;
    }

    private static Vector3 VertexAt(IReadOnlyList<float> data, int index) =>
        new(data[index * 3], data[index * 3 + 1], data[index * 3 + 2]);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"期望 {expected}，实际 {actual}。");
    }
}
