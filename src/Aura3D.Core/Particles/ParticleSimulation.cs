using System.Numerics;

namespace Aura3D.Core.Particles;

/// <summary>
/// Represents the particle simulation type.
/// </summary>
public static class ParticleSimulation
{
    /// <summary>
    /// Run one simulation step for a single emitter.
    /// Operates on emitter.Particles / emitter.ActiveCount / emitter.MaxParticles directly.
    /// </summary>
    public static void Update(
        ParticleEmitter emitter, float deltaTime, Random rng,
        Vector3 worldOffset, Quaternion worldRotation = default)
    {
        if (worldRotation == default) worldRotation = Quaternion.Identity;

        var particles = emitter.Particles;
        int activeCount = emitter.ActiveCount;
        int max = emitter.MaxParticles;

        RemoveDead(particles!, ref activeCount);
        Emit(particles!, ref activeCount, max, emitter, deltaTime, rng, worldOffset, worldRotation);
        UpdateAlive(particles!, activeCount, emitter, deltaTime);

        emitter.ActiveCount = activeCount;
    }

    private static void RemoveDead(ParticleData[] p, ref int n)
    {
        int i = 0;
        while (i < n)
        {
            if (p[i].IsDead) { n--; if (i < n) p[i] = p[n]; }
            else i++;
        }
    }

    private static void Emit(
        ParticleData[] p, ref int n, int max, ParticleEmitter em,
        float dt, Random rng, Vector3 offset, Quaternion worldRotation)
    {
        if (n >= max) return;
        if (em.IsFinished) return;

        em.ElapsedTime += dt;
        float toEmit = em.EmissionRate * dt + em.EmissionAccumulator;
        int cnt = (int)toEmit;
        em.EmissionAccumulator = toEmit - cnt;
        int rem = max - n;
        if (cnt > rem) cnt = rem;
        for (int j = 0; j < cnt; j++) { p[n] = NewParticle(em, rng, offset, worldRotation); n++; }
    }

    private static ParticleData NewParticle(ParticleEmitter em, Random rng,
        Vector3 offset, Quaternion worldRotation)
    {
        var localPos = SamplePosition(em, rng);
        var localVel = em.Velocity.Random(rng);
        return new ParticleData
        {
            Position = offset + Vector3.Transform(localPos, worldRotation),
            Velocity = Vector3.Transform(localVel, worldRotation),
            Lifetime = em.Lifetime.Random(rng),
            StartSize = em.StartSize.Random(rng),
            EndSize = em.EndSize.Random(rng),
            StartColor = em.GetStartColorVector(),
            EndColor = em.GetEndColorVector(),
            Rotation = em.Rotation.Random(rng),
            AngularVelocity = em.AngularVelocity.Random(rng),
            EmitterIndex = 0, // all particles in this array belong to the owning emitter
        };
    }

    private static void UpdateAlive(ParticleData[] p, int n, ParticleEmitter em, float dt)
    {
        var gravity = em.Gravity;
        var damping = em.Damping;
        for (int i = 0; i < n; i++)
        {
            ref var r = ref p[i];
            r.Velocity += gravity * dt;
            r.Velocity *= MathF.Max(0f, 1f - damping * dt);
            r.Position += r.Velocity * dt;
            r.Rotation += r.AngularVelocity * dt;
            r.Age += dt;
        }
    }

    // ===== Shape sampling =====

    /// <summary>
    /// Samples the position.
    /// </summary>
    public static Vector3 SamplePosition(ParticleEmitter em, Random rng) => em.Shape switch
    {
        EmissionShape.Point => Vector3.Zero,
        EmissionShape.Sphere => SampleSphere(em.ShapeSize, rng, false),
        EmissionShape.SphereSurface => SampleSphere(em.ShapeSize, rng, true),
        EmissionShape.Box => SampleBox(em.ShapeSize, rng),
        EmissionShape.Cone => SampleCone(em.ShapeSize, rng, em.ConeAngle),
        EmissionShape.Circle => SampleCircle(em.ShapeSize, rng),
        EmissionShape.Hemisphere => SampleHemi(em.ShapeSize, rng),
        _ => Vector3.Zero,
    };

    private static Vector3 SampleSphere(Vector3 s, Random r, bool surf)
    {
        float th = (float)(r.NextDouble() * MathF.PI * 2);
        float ph = MathF.Acos((float)(2 * r.NextDouble() - 1));
        float rad = surf ? 1f : MathF.Cbrt((float)r.NextDouble());
        float rx = s.X * .5f, ry = s.Y * .5f, rz = s.Z * .5f;
        return new(rad * rx * MathF.Sin(ph) * MathF.Cos(th), rad * ry * MathF.Cos(ph), rad * rz * MathF.Sin(ph) * MathF.Sin(th));
    }

    private static Vector3 SampleBox(Vector3 s, Random r) =>
        new((float)(r.NextDouble() - .5) * s.X, (float)(r.NextDouble() - .5) * s.Y, (float)(r.NextDouble() - .5) * s.Z);

    private static Vector3 SampleCone(Vector3 s, Random r, float angDeg)
    {
        float rad = s.X * .5f, h = s.Y, a = angDeg * MathF.PI / 180f;
        float t = (float)r.NextDouble(), rr = t * rad, y = t * h;
        float th = (float)(r.NextDouble() * MathF.PI * 2);
        float mx = y * MathF.Tan(a);
        if (rr > mx && mx > 0) rr = (float)r.NextDouble() * mx;
        return new(rr * MathF.Cos(th), y, rr * MathF.Sin(th));
    }

    private static Vector3 SampleCircle(Vector3 s, Random r)
    {
        float rx = s.X * .5f, rz = s.Z * .5f;
        float a = (float)(r.NextDouble() * MathF.PI * 2);
        float rad = MathF.Sqrt((float)r.NextDouble());
        return new(rad * rx * MathF.Cos(a), 0, rad * rz * MathF.Sin(a));
    }

    private static Vector3 SampleHemi(Vector3 s, Random r)
    {
        float th = (float)(r.NextDouble() * MathF.PI * 2);
        float ph = MathF.Acos((float)r.NextDouble());
        float rad = MathF.Cbrt((float)r.NextDouble());
        float rx = s.X * .5f, ry = s.Y * .5f, rz = s.Z * .5f;
        return new(rad * rx * MathF.Sin(ph) * MathF.Cos(th), MathF.Abs(rad * ry * MathF.Cos(ph)), rad * rz * MathF.Sin(ph) * MathF.Sin(th));
    }
}
