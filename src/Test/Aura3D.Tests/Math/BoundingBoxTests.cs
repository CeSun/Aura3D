using Aura3D.Core.Math;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Math;

public class BoundingBoxTests
{
    [Fact]
    public void CreateFromPoints_ShouldComputeExtentsAndDerivedValues()
    {
        var box = BoundingBox.CreateFromPoints(
        [
            new Vector3(-1f, 2f, 0f),
            new Vector3(3f, -2f, 5f),
            new Vector3(1f, 4f, -3f)
        ]);

        Assert.Equal(new Vector3(-1f, -2f, -3f), box.Min);
        Assert.Equal(new Vector3(3f, 4f, 5f), box.Max);
        Assert.Equal(new Vector3(4f, 6f, 8f), box.Size);
        Assert.Equal(new Vector3(1f, 1f, 1f), box.Center);
    }

    [Fact]
    public void ContainsAndIntersects_ShouldReflectSpatialRelationship()
    {
        var outer = new BoundingBox(Vector3.Zero, new Vector3(10f, 10f, 10f));
        var inner = new BoundingBox(new Vector3(2f, 2f, 2f), new Vector3(4f, 4f, 4f));
        var separate = new BoundingBox(new Vector3(11f, 11f, 11f), new Vector3(12f, 12f, 12f));

        Assert.True(outer.Contains(inner));
        Assert.True(outer.Contains(new Vector3(10f, 10f, 10f)));
        Assert.True(outer.Intersects(inner));
        Assert.False(outer.Intersects(separate));
    }

    [Fact]
    public void Transform_ShouldTranslateBounds()
    {
        var box = new BoundingBox(new Vector3(-1f, -2f, -3f), new Vector3(1f, 2f, 3f));

        var transformed = box.Transform(Matrix4x4.CreateTranslation(5f, 6f, 7f));

        Assert.Equal(new Vector3(4f, 4f, 4f), transformed.Min);
        Assert.Equal(new Vector3(6f, 8f, 10f), transformed.Max);
        Assert.Equal(box.Size, transformed.Size);
    }

    [Fact]
    public void CreateMerged_ShouldSpanAllInputBoxes()
    {
        var merged = BoundingBox.CreateMerged(
        [
            new BoundingBox(new Vector3(-1f, -1f, -1f), Vector3.Zero),
            new BoundingBox(new Vector3(2f, 3f, 4f), new Vector3(5f, 6f, 7f))
        ]);

        Assert.Equal(new Vector3(-1f, -1f, -1f), merged.Min);
        Assert.Equal(new Vector3(5f, 6f, 7f), merged.Max);
    }
}
