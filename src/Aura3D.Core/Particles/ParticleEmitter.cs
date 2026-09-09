using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Particles;

/// <summary>
/// Represents the particle emitter type.
/// </summary>
public class ParticleEmitter
{
    // ---- Emission ----

    /// <summary>
    /// Gets or sets the emission rate.
    /// </summary>
    public float EmissionRate
    {
        get => _emissionRate;
        set => _emissionRate = ValidateNonNegativeFinite(value, nameof(EmissionRate));
    }

    private float _emissionRate = 100f;
    /// <summary>
    /// Gets or sets the shape.
    /// </summary>
    public EmissionShape Shape
    {
        get => _shape;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(Shape), value, "Unsupported emission shape.");
            _shape = value;
        }
    }

    private EmissionShape _shape = EmissionShape.Point;
    /// <summary>
    /// Gets or sets the shape size.
    /// </summary>
    public Vector3 ShapeSize
    {
        get => _shapeSize;
        set
        {
            ValidateFinite(value, nameof(ShapeSize));
            if (value.X < 0f || value.Y < 0f || value.Z < 0f)
                throw new ArgumentOutOfRangeException(nameof(ShapeSize), "Shape dimensions must be non-negative.");
            _shapeSize = value;
        }
    }

    private Vector3 _shapeSize = Vector3.One;
    /// <summary>
    /// Gets or sets the cone angle.
    /// </summary>
    public float ConeAngle
    {
        get => _coneAngle;
        set
        {
            if (!float.IsFinite(value) || value < 0f || value >= 90f)
                throw new ArgumentOutOfRangeException(nameof(ConeAngle), "Cone angle must be finite and in the range [0, 90) degrees.");
            _coneAngle = value;
        }
    }

    private float _coneAngle = 30f;
    /// <summary>
    /// Gets or sets the looping.
    /// </summary>
    public bool Looping { get; set; } = true;
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    public float Duration
    {
        get => _duration;
        set => _duration = ValidateNonNegativeFinite(value, nameof(Duration));
    }

    private float _duration;

    // ---- Particle properties ----

    /// <summary>
    /// Gets or sets the lifetime.
    /// </summary>
    public RangeFloat Lifetime
    {
        get => _lifetime;
        set
        {
            ValidateRange(value, nameof(Lifetime), requirePositive: true);
            _lifetime = value;
        }
    }

    private RangeFloat _lifetime = new(1f, 3f);
    /// <summary>
    /// Gets or sets the velocity.
    /// </summary>
    public RangeVector3 Velocity
    {
        get => _velocity;
        set
        {
            ValidateRange(value, nameof(Velocity));
            _velocity = value;
        }
    }

    private RangeVector3 _velocity = new(new(0, 5, 0), new(0, 10, 0));
    /// <summary>
    /// Gets or sets the start size.
    /// </summary>
    public RangeFloat StartSize
    {
        get => _startSize;
        set
        {
            ValidateRange(value, nameof(StartSize), requireNonNegative: true);
            _startSize = value;
        }
    }

    private RangeFloat _startSize = new(0.1f, 0.3f);
    /// <summary>
    /// Gets or sets the end size.
    /// </summary>
    public RangeFloat EndSize
    {
        get => _endSize;
        set
        {
            ValidateRange(value, nameof(EndSize), requireNonNegative: true);
            _endSize = value;
        }
    }

    private RangeFloat _endSize = new(0.01f, 0.05f);
    /// <summary>
    /// Gets or sets the start color.
    /// </summary>
    public Color StartColor { get; set; } = Color.White;
    /// <summary>
    /// Gets or sets the end color.
    /// </summary>
    public Color EndColor { get; set; } = Color.Transparent;
    /// <summary>
    /// Gets or sets the rotation.
    /// </summary>
    public RangeFloat Rotation
    {
        get => _rotation;
        set
        {
            ValidateRange(value, nameof(Rotation));
            _rotation = value;
        }
    }

    private RangeFloat _rotation = new(0f, MathF.PI * 2);
    /// <summary>
    /// Gets or sets the angular velocity.
    /// </summary>
    public RangeFloat AngularVelocity
    {
        get => _angularVelocity;
        set
        {
            ValidateRange(value, nameof(AngularVelocity));
            _angularVelocity = value;
        }
    }

    private RangeFloat _angularVelocity = new(-1f, 1f);

    // ---- Physics ----

    /// <summary>
    /// Gets or sets the gravity.
    /// </summary>
    public Vector3 Gravity
    {
        get => _gravity;
        set
        {
            ValidateFinite(value, nameof(Gravity));
            _gravity = value;
        }
    }

    private Vector3 _gravity = new(0f, -9.8f, 0f);
    /// <summary>
    /// Gets or sets the damping.
    /// </summary>
    public float Damping
    {
        get => _damping;
        set => _damping = ValidateNonNegativeFinite(value, nameof(Damping));
    }

    private float _damping;

    // ---- Billboard rendering (per-emitter) ----

    /// <summary>
    /// Texture for billboard particles. When set, the billboard shader samples this texture.
    /// When null, a procedural circle is drawn.
    /// </summary>
    public Texture? Texture { get; set; }

    /// <summary>
    /// Flipbook grid dimensions for texture animation. Default (1,1) disables flipbook.
    /// Example: (8,8) for a 64-frame fire texture laid out in an 8x8 grid.
    /// </summary>
    public Vector2 FlipbookTiles
    {
        get => _flipbookTiles;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
                value.X < 1f || value.Y < 1f ||
                value.X != MathF.Truncate(value.X) || value.Y != MathF.Truncate(value.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(FlipbookTiles), "Flipbook dimensions must be finite positive integers.");
            }
            _flipbookTiles = value;
        }
    }

    private Vector2 _flipbookTiles = Vector2.One;

    /// <summary>
    /// Blend mode for this emitter's particles. Each emitter can have a different blend mode,
    /// enabling mixed Opaque/Translucent effects within a single ParticleSystem.
    /// </summary>
    public BlendMode BlendMode
    {
        get => _blendMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(BlendMode), value, "Unsupported blend mode.");
            _blendMode = value;
        }
    }

    private BlendMode _blendMode = BlendMode.Translucent;

    // ---- Mesh rendering (per-emitter) ----

    /// <summary>
    /// When set, this emitter renders particles as 3D mesh instances instead of billboard quads.
    /// Each emitter can use a different mesh.
    /// </summary>
    public Mesh? Mesh { get; set; }

    /// <summary>
    /// Optional material override for mesh-mode particles on this emitter.
    /// When null, the material from Mesh is used.
    /// </summary>
    public Material? Material { get; set; }

    /// <summary>
    /// Maximum particle count for this emitter. Allocated during Play().
    /// </summary>
    public int MaxParticles
    {
        get => _maxParticles;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxParticles), "Maximum particle count must be greater than zero.");
            _maxParticles = value;
        }
    }

    private int _maxParticles = 1000;

    /// <summary>
    /// Scale multiplier for mesh-based particles. Applied on top of the per-particle size.
    /// Ignored for billboard particles.
    /// </summary>
    public float MeshScale
    {
        get => _meshScale;
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(MeshScale), "Mesh scale must be finite and greater than zero.");
            _meshScale = value;
        }
    }

    private float _meshScale = 1f;

    // ---- Runtime state (managed by ParticleSystem) ----

    /// <summary>
    /// Gets or sets the elapsed time.
    /// </summary>
    public float ElapsedTime { get; internal set; }
    /// <summary>
    /// Gets or sets the emission accumulator.
    /// </summary>
    public float EmissionAccumulator { get; internal set; }
    /// <summary>
    /// Gets a value indicating whether the object is finished.
    /// </summary>
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

    /// <summary>
    /// Gets the start color vector.
    /// </summary>
    public Vector4 GetStartColorVector() =>
        new(StartColor.R / 255f, StartColor.G / 255f, StartColor.B / 255f, StartColor.A / 255f);

    /// <summary>
    /// Gets the end color vector.
    /// </summary>
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

    private static float ValidateNonNegativeFinite(float value, string paramName)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(paramName, "Value must be finite and non-negative.");
        return value;
    }

    private static void ValidateFinite(Vector3 value, string paramName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new ArgumentOutOfRangeException(paramName, "All components must be finite.");
    }

    private static void ValidateRange(
        RangeFloat value,
        string paramName,
        bool requirePositive = false,
        bool requireNonNegative = false)
    {
        if (!float.IsFinite(value.Min) || !float.IsFinite(value.Max) || value.Min > value.Max ||
            (requirePositive && value.Min <= 0f) ||
            (requireNonNegative && value.Min < 0f))
        {
            throw new ArgumentOutOfRangeException(paramName, "Range bounds must be finite, ordered, and within the property's valid domain.");
        }
    }

    private static void ValidateRange(RangeVector3 value, string paramName)
    {
        ValidateFinite(value.Min, paramName);
        ValidateFinite(value.Max, paramName);
        if (value.Min.X > value.Max.X || value.Min.Y > value.Max.Y || value.Min.Z > value.Max.Z)
            throw new ArgumentOutOfRangeException(paramName, "Each minimum component must be less than or equal to its maximum component.");
    }
}
