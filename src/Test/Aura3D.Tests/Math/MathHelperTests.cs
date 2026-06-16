using Aura3D.Core.Math;
using System.Drawing;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Math;

public class MathHelperTests
{
    [Fact]
    public void DegreeAndRadians_ShouldRoundTrip()
    {
        const float degree = 90f;

        var radians = degree.DegreeToRadians();
        var roundTrip = radians.RadiansToDegree();

        Assert.Equal(MathF.PI / 2f, radians, 5);
        Assert.Equal(degree, roundTrip, 5);
    }

    [Fact]
    public void Scale_ShouldExtractScaleComponents()
    {
        var matrix = MatrixHelper.CreateTransform(Vector3.Zero, Quaternion.Identity, new Vector3(2f, 3f, 4f));

        var scale = matrix.Scale();

        Assert.Equal(2f, scale.X, 5);
        Assert.Equal(3f, scale.Y, 5);
        Assert.Equal(4f, scale.Z, 5);
    }

    [Fact]
    public void ColorConversions_ShouldRoundTrip()
    {
        var color = Color.FromArgb(255, 64, 128, 192);

        var vector = color.ToVector4();
        var roundTrip = vector.ToColor();

        Assert.Equal(64 / 255f, vector.X, 5);
        Assert.Equal(128 / 255f, vector.Y, 5);
        Assert.Equal(192 / 255f, vector.Z, 5);
        Assert.Equal(1f, vector.W, 5);
        Assert.Equal(color, roundTrip);
    }
}
