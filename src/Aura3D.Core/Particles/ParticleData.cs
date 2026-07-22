using System.Numerics;

namespace Aura3D.Core.Particles;

/// <summary>
/// Represents the particle data type.
/// </summary>
public struct ParticleData
{
    /// <summary>
    /// Gets or sets the position.
    /// </summary>
    public Vector3 Position { get; set; }
    /// <summary>
    /// Gets or sets the age.
    /// </summary>
    public float Age { get; set; }
    /// <summary>
    /// Gets or sets the velocity.
    /// </summary>
    public Vector3 Velocity { get; set; }
    /// <summary>
    /// Gets or sets the lifetime.
    /// </summary>
    public float Lifetime { get; set; }
    /// <summary>
    /// Gets or sets the start size.
    /// </summary>
    public float StartSize { get; set; }
    /// <summary>
    /// Gets or sets the end size.
    /// </summary>
    public float EndSize { get; set; }
    /// <summary>
    /// Gets or sets the start color.
    /// </summary>
    public Vector4 StartColor { get; set; }
    /// <summary>
    /// Gets or sets the end color.
    /// </summary>
    public Vector4 EndColor { get; set; }
    /// <summary>
    /// Gets or sets the rotation.
    /// </summary>
    public float Rotation { get; set; }
    /// <summary>
    /// Gets or sets the angular velocity.
    /// </summary>
    public float AngularVelocity { get; set; }
    /// <summary>
    /// Gets or sets the emitter index.
    /// </summary>
    public int EmitterIndex { get; set; }

    /// <summary>
    /// Gets a value indicating whether the object is dead.
    /// </summary>
    public readonly bool IsDead => Age >= Lifetime;

    /// <summary>
    /// Gets the age ratio.
    /// </summary>
    public readonly float AgeRatio
    {
        get
        {
            if (Lifetime <= 0f) return 0f;
            var r = Age / Lifetime;
            return r < 0f ? 0f : (r > 1f ? 1f : r);
        }
    }

    /// <summary>
    /// Gets the current size.
    /// </summary>
    public readonly float CurrentSize => StartSize + (EndSize - StartSize) * AgeRatio;
    /// <summary>
    /// Gets the current color.
    /// </summary>
    public readonly Vector4 CurrentColor => Vector4.Lerp(StartColor, EndColor, AgeRatio);
}

/// <summary>
/// Represents the range float type.
/// </summary>
public struct RangeFloat
{
    /// <summary>
    /// Gets or sets the min.
    /// </summary>
    public float Min { get; set; }
    /// <summary>
    /// Gets or sets the max.
    /// </summary>
    public float Max { get; set; }
    /// <summary>
    /// Initializes a new instance of the range float type.
    /// </summary>
    public RangeFloat(float min, float max) { Min = min; Max = max; }
    /// <summary>
    /// Performs the random operation.
    /// </summary>
    public readonly float Random(Random rng) => Min + (float)rng.NextDouble() * (Max - Min);
}

/// <summary>
/// Represents the range vector3 type.
/// </summary>
public struct RangeVector3
{
    /// <summary>
    /// Gets or sets the min.
    /// </summary>
    public Vector3 Min { get; set; }
    /// <summary>
    /// Gets or sets the max.
    /// </summary>
    public Vector3 Max { get; set; }
    /// <summary>
    /// Initializes a new instance of the range vector3 type.
    /// </summary>
    public RangeVector3(Vector3 min, Vector3 max) { Min = min; Max = max; }
    /// <summary>
    /// Initializes a new instance of the range vector3 type.
    /// </summary>
    public RangeVector3(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    { Min = new Vector3(minX, minY, minZ); Max = new Vector3(maxX, maxY, maxZ); }
    /// <summary>
    /// Performs the random operation.
    /// </summary>
    public readonly Vector3 Random(Random rng) => new(
        Min.X + (float)rng.NextDouble() * (Max.X - Min.X),
        Min.Y + (float)rng.NextDouble() * (Max.Y - Min.Y),
        Min.Z + (float)rng.NextDouble() * (Max.Z - Min.Z));
}

/// <summary>
/// Specifies values for emission shape.
/// </summary>
public enum EmissionShape
{
    /// <summary>
    /// Specifies point.
    /// </summary>
    Point,
    /// <summary>
    /// Specifies sphere.
    /// </summary>
    Sphere,
    /// <summary>
    /// Specifies sphere surface.
    /// </summary>
    SphereSurface,
    /// <summary>
    /// Specifies box.
    /// </summary>
    Box,
    /// <summary>
    /// Specifies cone.
    /// </summary>
    Cone,
    /// <summary>
    /// Specifies circle.
    /// </summary>
    Circle,
    /// <summary>
    /// Specifies hemisphere.
    /// </summary>
    Hemisphere,
}
