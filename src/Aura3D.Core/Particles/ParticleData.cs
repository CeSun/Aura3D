using System.Numerics;

namespace Aura3D.Core.Particles;

public struct ParticleData
{
    public Vector3 Position { get; set; }
    public float Age { get; set; }
    public Vector3 Velocity { get; set; }
    public float Lifetime { get; set; }
    public float StartSize { get; set; }
    public float EndSize { get; set; }
    public Vector4 StartColor { get; set; }
    public Vector4 EndColor { get; set; }
    public float Rotation { get; set; }
    public float AngularVelocity { get; set; }
    public int EmitterIndex { get; set; }

    public readonly bool IsDead => Age >= Lifetime;

    public readonly float AgeRatio
    {
        get
        {
            if (Lifetime <= 0f) return 0f;
            var r = Age / Lifetime;
            return r < 0f ? 0f : (r > 1f ? 1f : r);
        }
    }

    public readonly float CurrentSize => StartSize + (EndSize - StartSize) * AgeRatio;
    public readonly Vector4 CurrentColor => Vector4.Lerp(StartColor, EndColor, AgeRatio);
}

public struct RangeFloat
{
    public float Min { get; set; }
    public float Max { get; set; }
    public RangeFloat(float min, float max) { Min = min; Max = max; }
    public readonly float Random(Random rng) => Min + (float)rng.NextDouble() * (Max - Min);
}

public struct RangeVector3
{
    public Vector3 Min { get; set; }
    public Vector3 Max { get; set; }
    public RangeVector3(Vector3 min, Vector3 max) { Min = min; Max = max; }
    public RangeVector3(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    { Min = new Vector3(minX, minY, minZ); Max = new Vector3(maxX, maxY, maxZ); }
    public readonly Vector3 Random(Random rng) => new(
        Min.X + (float)rng.NextDouble() * (Max.X - Min.X),
        Min.Y + (float)rng.NextDouble() * (Max.Y - Min.Y),
        Min.Z + (float)rng.NextDouble() * (Max.Z - Min.Z));
}

public enum EmissionShape
{
    Point, Sphere, SphereSurface, Box, Cone, Circle, Hemisphere,
}
