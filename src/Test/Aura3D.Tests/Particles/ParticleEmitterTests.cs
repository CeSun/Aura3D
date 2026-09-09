using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Particles;

public class ParticleEmitterTests
{
    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NonNegativeScalars_ShouldRejectInvalidValues(float value)
    {
        var emitter = new ParticleEmitter();

        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.EmissionRate = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Duration = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Damping = value);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(90f)]
    [InlineData(float.NaN)]
    public void ConeAngle_ShouldRejectValuesOutsideSamplingRange(float value)
    {
        var emitter = new ParticleEmitter();
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.ConeAngle = value);
    }

    [Fact]
    public void Ranges_ShouldRequireFiniteOrderedBounds()
    {
        var emitter = new ParticleEmitter();

        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Lifetime = new RangeFloat(0f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.StartSize = new RangeFloat(2f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.EndSize = new RangeFloat(-1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Rotation = new RangeFloat(float.NaN, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Velocity = new RangeVector3(Vector3.One, Vector3.Zero));
    }

    [Fact]
    public void ShapeAndFlipbookDimensions_ShouldRejectInvalidValues()
    {
        var emitter = new ParticleEmitter();

        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Shape = (EmissionShape)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.BlendMode = (BlendMode)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.ShapeSize = new Vector3(1f, -1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.FlipbookTiles = new Vector2(1.5f, 2f));
    }

    [Fact]
    public void AllocationAndMeshScale_ShouldBePositive()
    {
        var emitter = new ParticleEmitter();

        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.MaxParticles = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.MeshScale = 0f);
    }
}
