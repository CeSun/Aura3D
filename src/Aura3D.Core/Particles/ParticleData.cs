using System.Numerics;
using Aura3D.Core.Serialization;

namespace Aura3D.Core.Particles;

public struct ParticleData
{
    public Vector3 Position;
    public float Age;
    public Vector3 Velocity;
    public float Lifetime;
    public float StartSize;
    public float EndSize;
    public Vector4 StartColor;
    public Vector4 EndColor;
    public float Rotation;
    public float AngularVelocity;
    public int EmitterIndex;

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

[AuraChunk(chunkType: AuraChunkType.RangeFloat, chunkVersion: 1)]
public partial struct RangeFloat
{
    [AuraField(since: 1)]
    public float Min;
    [AuraField(since: 1)]
    public float Max;
    public RangeFloat(float min, float max) { Min = min; Max = max; }
    public readonly float Random(Random rng) => Min + (float)rng.NextDouble() * (Max - Min);
}

[AuraChunk(chunkType: AuraChunkType.RangeVector3, chunkVersion: 1)]
public partial struct RangeVector3
{
    [AuraField(since: 1)]
    public Vector3 Min;
    [AuraField(since: 1)]
    public Vector3 Max;
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
