using Aura3D.Core.Resources;
using Aura3D.Model;
using Xunit;

namespace Aura3D.Tests.Resources;

/// <summary>
/// Verifies the point-cloud data path behind issue #21: a pure xyz/rgb PLY (no faces, no
/// normals) must load as a Points-primitive mesh whose material is preset for point
/// rendering (unlit + vertex colors + a visible point size), so it shows up correctly in
/// the default pipelines instead of vanishing into 1px dots or lit NaN shading.
/// </summary>
public class AssimpPointcloudTests
{
    private const string PointCloudPly = """
        ply
        format ascii 1.0
        element vertex 4
        property float x
        property float y
        property float z
        property uchar red
        property uchar green
        property uchar blue
        end_header
        0 0 0 255 0 0
        1 0 0 0 255 0
        0 1 0 0 0 255
        1 1 0 255 255 0
        """;

    [Fact]
    public void PurePointcloudPly_ShouldLoadAsUnlitVertexColoredPoints()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aura3d-test-{Guid.NewGuid():N}.ply");

        File.WriteAllText(path, PointCloudPly);

        try
        {
            var model = AssimpLoader.Load(path);

            var mesh = Assert.Single(model.Meshes);

            Assert.NotNull(mesh.Geometry);
            Assert.Equal(PrimitiveType.Points, mesh.Geometry!.PrimitiveType);

            var colors = mesh.Geometry.GetAttributeData(BuildInVertexAttribute.Color_0);

            Assert.NotNull(colors);
            Assert.Equal(4 * 4, colors!.Count);

            var material = mesh.Material;

            Assert.NotNull(material);

            Assert.Equal(1f, Assert.IsType<float>(material.Parameters[Material.UnlitParameterName]));
            Assert.Equal(1f, Assert.IsType<float>(material.Parameters[Material.UseVertexColorParameterName]));
            Assert.Equal(3f, Assert.IsType<float>(material.Parameters[Material.PointSizeParameterName]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TriangleMeshPly_ShouldStayLitWithoutPointParameters()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aura3d-test-{Guid.NewGuid():N}.ply");

        // Two triangles (no vertex colors) - the default lit material path must be untouched.
        File.WriteAllText(path, """
            ply
            format ascii 1.0
            element vertex 4
            property float x
            property float y
            property float z
            element face 2
            property list uchar int vertex_indices
            end_header
            0 0 0
            1 0 0
            0 1 0
            1 1 0
            0 1 2
            1 2 3
            """);

        try
        {
            var model = AssimpLoader.Load(path);

            var mesh = Assert.Single(model.Meshes);

            Assert.NotNull(mesh.Geometry);
            Assert.Equal(PrimitiveType.Triangles, mesh.Geometry!.PrimitiveType);

            var material = mesh.Material;

            Assert.NotNull(material);

            Assert.False(material.Parameters.ContainsKey(Material.UnlitParameterName));
            Assert.False(material.Parameters.ContainsKey(Material.UseVertexColorParameterName));
            Assert.False(material.Parameters.ContainsKey(Material.PointSizeParameterName));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
