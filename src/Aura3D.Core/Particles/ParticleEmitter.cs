using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Serialization;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Particles;

[AuraChunk(chunkType: AuraChunkType.ParticleEmitter, chunkVersion: 1)]
public partial class ParticleEmitter
{
    // ---- Emission ----

    [AuraField(since: 1)]
    public float EmissionRate { get; set; } = 100f;
    [AuraField(since: 1)]
    public EmissionShape Shape { get; set; } = EmissionShape.Point;
    [AuraField(since: 1)]
    public Vector3 ShapeSize { get; set; } = Vector3.One;
    [AuraField(since: 1)]
    public float ConeAngle { get; set; } = 30f;
    [AuraField(since: 1)]
    public bool Looping { get; set; } = true;
    [AuraField(since: 1)]
    public float Duration { get; set; } = 0f;

    // ---- Particle properties ----

    [AuraField(since: 1)]
    public RangeFloat Lifetime { get; set; } = new(1f, 3f);
    [AuraField(since: 1)]
    public RangeVector3 Velocity { get; set; } = new(new(0, 5, 0), new(0, 10, 0));
    [AuraField(since: 1)]
    public RangeFloat StartSize { get; set; } = new(0.1f, 0.3f);
    [AuraField(since: 1)]
    public RangeFloat EndSize { get; set; } = new(0.01f, 0.05f);
    [AuraField(since: 1)]
    public Color StartColor { get; set; } = Color.White;
    [AuraField(since: 1)]
    public Color EndColor { get; set; } = Color.Transparent;
    [AuraField(since: 1)]
    public RangeFloat Rotation { get; set; } = new(0f, MathF.PI * 2);
    [AuraField(since: 1)]
    public RangeFloat AngularVelocity { get; set; } = new(-1f, 1f);

    // ---- Physics ----

    [AuraField(since: 1)]
    public Vector3 Gravity { get; set; } = new(0f, -9.8f, 0f);
    [AuraField(since: 1)]
    public float Damping { get; set; } = 0f;

    // ---- Billboard rendering (per-emitter) ----

    /// <summary>
    /// Texture for billboard particles. When set, the billboard shader samples this texture.
    /// When null, a procedural circle is drawn.
    /// </summary>
    [AuraField(since: 1)]
    [AuraReference]
    public ITexture? Texture { get; set; }

    /// <summary>
    /// Flipbook grid dimensions for texture animation. Default (1,1) disables flipbook.
    /// Example: (8,8) for a 64-frame fire texture laid out in an 8x8 grid.
    /// </summary>
    [AuraField(since: 1)]
    public Vector2 FlipbookTiles { get; set; } = Vector2.One;

    /// <summary>
    /// Blend mode for this emitter's particles. Each emitter can have a different blend mode,
    /// enabling mixed Opaque/Translucent effects within a single ParticleSystem.
    /// </summary>
    [AuraField(since: 1)]
    public BlendMode BlendMode { get; set; } = BlendMode.Translucent;

    // ---- Mesh rendering (per-emitter) ----

    /// <summary>
    /// When set, this emitter renders particles as 3D mesh instances instead of billboard quads.
    /// Each emitter can use a different mesh.
    /// </summary>
    [AuraField(since: 1)]
    [AuraReference]
    public Mesh? Mesh { get; set; }

    /// <summary>
    /// Optional material override for mesh-mode particles on this emitter.
    /// When null, the material from Mesh is used.
    /// </summary>
    [AuraField(since: 1)]
    [AuraReference]
    public Material? Material { get; set; }

    /// <summary>
    /// Maximum particle count for this emitter. Allocated during Play().
    /// </summary>
    [AuraField(since: 1)]
    public int MaxParticles { get; set; } = 1000;

    /// <summary>
    /// Scale multiplier for mesh-based particles. Applied on top of the per-particle size.
    /// Ignored for billboard particles.
    /// </summary>
    [AuraField(since: 1)]
    public float MeshScale { get; set; } = 1f;

    // ---- Runtime state (managed by ParticleSystem) ----

    public float ElapsedTime { get; internal set; }
    public float EmissionAccumulator { get; internal set; }
    public bool IsFinished => !Looping && Duration > 0 && ElapsedTime >= Duration;

    /// <summary>Whether this emitter renders through the mesh pipeline (true) or billboard pipeline (false).</summary>
    public bool UseMeshRenderer => Mesh != null;

    internal ParticleData[]? Particles;
    internal int ActiveCount;
    internal ParticleGpuBuffer? GpuBuffer;
    internal InstancedMesh? InstancedMesh;
    internal Random? Rng;
    internal Matrix4x4[]? InstanceTransforms;

    // ---- Color helpers ----

    public Vector4 GetStartColorVector() =>
        new(StartColor.R / 255f, StartColor.G / 255f, StartColor.B / 255f, StartColor.A / 255f);

    public Vector4 GetEndColorVector() =>
        new(EndColor.R / 255f, EndColor.G / 255f, EndColor.B / 255f, EndColor.A / 255f);

    // ---- Internal methods (moved from ParticleSystem) ----

    /// <summary>
    /// Sort particles by distance to camera (back-to-front) for correct alpha blending.
    /// Called by ParticlePass during rendering (billboard mode only).
    /// </summary>
    internal void SortByDistance(Vector3 camPos)
    {
        if (ActiveCount <= 1 || BlendMode == BlendMode.Opaque || BlendMode == BlendMode.Masked) return;
        if (Particles == null) return;
        int n = ActiveCount;
        var camPosLocal = camPos;
        System.Array.Sort(Particles, 0, n, Comparer<ParticleData>.Create((a, b) =>
        {
            float da = Vector3.DistanceSquared(a.Position, camPosLocal);
            float db = Vector3.DistanceSquared(b.Position, camPosLocal);
            return db.CompareTo(da);
        }));
    }

    /// <summary>
    /// Convert alive particles to world-space transforms and update the InstancedMesh.
    /// </summary>
    internal void UpdateMeshInstances(Quaternion systemWorldRotation)
    {
        if (InstancedMesh == null || Particles == null) return;

        int n = ActiveCount;
        if (InstanceTransforms == null || InstanceTransforms.Length < n)
            InstanceTransforms = new Matrix4x4[n];

        for (int i = 0; i < n; i++)
        {
            ref var p = ref Particles[i];
            float meshScale = MeshScale;

            var spinRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, p.Rotation);
            var finalRot = Quaternion.Concatenate(systemWorldRotation, spinRot);

            InstanceTransforms[i] =
                Matrix4x4.CreateScale(p.CurrentSize * meshScale)
                * Matrix4x4.CreateFromQuaternion(finalRot)
                * Matrix4x4.CreateTranslation(p.Position);
        }

        InstancedMesh.SetInstances(
            new ArraySegment<Matrix4x4>(InstanceTransforms, 0, n));
    }
}
