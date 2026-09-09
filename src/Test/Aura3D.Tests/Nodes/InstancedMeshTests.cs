using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Diagnostics;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Nodes;

public class InstancedMeshTests
{
    [Fact]
    public void SetInstances_ShouldRefreshPerInstanceBoundingBoxes()
    {
        var mesh = InstancedMesh.FromMesh(CreateSourceMesh());
        var transforms = new[]
        {
            Matrix4x4.Identity,
            Matrix4x4.CreateTranslation(10, 0, 0)
        };

        mesh.SetInstances(transforms);

        Assert.NotNull(mesh.GetInstanceWorldBoundingBox(0));
        var second = Assert.IsType<Aura3D.Core.Math.BoundingBox>(mesh.GetInstanceWorldBoundingBox(1));
        Assert.True(second.Min.X >= 10);
    }

    [Fact]
    public void InstancedMeshGroup_ShouldDiscardStaleAsyncBuildResults()
    {
        var group = new InstancedMeshGroup(CreateSourceMesh())
        {
            MaxInstancesPerGroup = 64,
            MaxDepth = 8
        };
        group.SetInstances(Enumerable.Range(0, 20_000)
            .Select(i => Matrix4x4.CreateTranslation(i % 200, i / 200, 0))
            .ToArray());

        group.Build();
        group.AddInstance(Matrix4x4.CreateTranslation(500, 500, 500));
        group.RemoveInstance(0);

        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            group.BuildIfNeeded();
            if (!group.IsBuilding &&
                group.RebuildCount > 0 &&
                group.Groups.Sum(item => item.InstanceCount) == group.InstanceCount)
            {
                break;
            }
            Thread.Yield();
        }

        Assert.False(group.IsBuilding);
        Assert.Equal(group.InstanceCount, group.Groups.Sum(item => item.InstanceCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxInstancesPerGroup_ShouldRejectInvalidValues(int value)
    {
        var group = new InstancedMeshGroup(CreateSourceMesh());
        Assert.Throws<ArgumentOutOfRangeException>(() => group.MaxInstancesPerGroup = value);
    }

    private static Mesh CreateSourceMesh()
    {
        var geometry = new Geometry();
        geometry.SetVertexAttribute(
            BuildInVertexAttribute.Position,
            3,
            [0, 0, 0, 1, 0, 0, 0, 1, 0]);
        geometry.SetIndices([0, 1, 2]);
        return new Mesh { Geometry = geometry };
    }
}
