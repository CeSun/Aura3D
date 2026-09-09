using Aura3D.Core.Nodes;
using Xunit;

namespace Aura3D.Tests.Nodes;

public class CameraTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ProjectionScalars_ShouldRejectNonPositiveOrNonFiniteValues(float value)
    {
        var camera = new Camera();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.NearPlane = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.FarPlane = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.OrthographicSize = value);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(180f)]
    [InlineData(float.NaN)]
    public void FieldOfView_ShouldRejectValuesOutsideOpenProjectionRange(float value)
    {
        var camera = new Camera();
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.FieldOfView = value);
    }

    [Fact]
    public void ClippingPlanes_ShouldMaintainNearFarOrdering()
    {
        var camera = new Camera();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.NearPlane = camera.FarPlane);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.FarPlane = camera.NearPlane);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.SetClippingPlanes(10f, 5f));

        camera.SetClippingPlanes(10f, 500f);
        Assert.Equal(10f, camera.NearPlane);
        Assert.Equal(500f, camera.FarPlane);
    }

    [Fact]
    public void ProjectionType_ShouldRejectUnknownEnumValue()
    {
        var camera = new Camera();
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.ProjectionType = (ProjectionType)99);
    }
}
