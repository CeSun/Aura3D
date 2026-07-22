using Aura3D.Core.Math;
using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// High-performance particle system node using CPU simulation.
/// Each emitter manages its own particles, GPU buffer (billboard mode),
/// and InstancedMesh (mesh mode) independently.
/// The system provides shared lifecycle management and bounding box estimation.
/// </summary>
public class ParticleSystem : Node
{
    /// <summary>
    /// System-level maximum particle count. Each emitter has its own MaxParticles;
    /// this property can be used as a convenience to set all emitter capacities at once.
    /// Only settable when not playing.
    /// </summary>
    public int MaxParticles
    {
        get => _maxParticles;
        set
        {
            if (!_isPlaying)
                _maxParticles = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the object is playing.
    /// </summary>
    public bool IsPlaying => _isPlaying;
    /// <summary>
    /// Gets the active count.
    /// </summary>
    public int ActiveCount => Emitters.Sum(e => e.ActiveCount);
    /// <summary>
    /// Gets the emitters.
    /// </summary>
    public List<ParticleEmitter> Emitters { get; } = new();

    // ---- Visibility culling ----

    private BoundingBox? _customBoundingBox;

    /// <summary>
    /// Optional custom world-space bounding box for all emitters.
    /// When set, this overrides the automatic estimate.
    /// </summary>
    public BoundingBox? CustomBoundingBox
    {
        get => _customBoundingBox;
        set
        {
            _customBoundingBox = value;
            _estimatedBbox = value ?? EstimateWorldBoundingBox();
            if (_estimatedBbox != null)
            {
                foreach (var em in Emitters)
                {
                    if (em.InstancedMesh != null)
                        em.InstancedMesh.SetStaticWorldBoundingBox(_estimatedBbox);
                }
            }
        }
    }

    /// <summary>
    /// When enabled, the particle simulation is skipped if the system's bounding box
    /// is outside the main camera frustum.
    /// </summary>
    public bool EnableVisibilityCulling { get; set; } = false;

    /// <summary>
    /// World-space bounding box for debug display and visibility culling.
    /// </summary>
    public BoundingBox? WorldBoundingBox => _estimatedBbox;

    // ---- Lifecycle ----

    /// <summary>
    /// Starts the associated data.
    /// </summary>
    public void Play()
    {
        if (_isPlaying) return;
        _isPlaying = true;
        _isPaused = false;
        _accumulatedSkippedTime = 0f;

        _estimatedBbox = _customBoundingBox ?? EstimateWorldBoundingBox();

        foreach (var em in Emitters)
        {
            em.ElapsedTime = 0;
            em.EmissionAccumulator = 0;
            em.Particles = new ParticleData[em.MaxParticles];
            em.ActiveCount = 0;
            em.Rng = new Random();
            em.GpuBuffer = new ParticleGpuBuffer();
            em.InstanceTransforms = null;

            // Mesh mode: create InstancedMesh per emitter
            if (em.UseMeshRenderer)
            {
                em.InstancedMesh = InstancedMesh.FromMesh(em.Mesh!);
                if (em.Material != null)
                    em.InstancedMesh.Material = em.Material;
                em.InstancedMesh.Name = $"{Name}_Emitter{Emitters.IndexOf(em)}_Instances";
                em.InstancedMesh.EnableFrustumCulling = false;

                if (_estimatedBbox != null)
                    em.InstancedMesh.SetStaticWorldBoundingBox(_estimatedBbox);

                AddChild(em.InstancedMesh, AttachToParentRule.KeepWorld);
            }
        }
    }

    /// <summary>
    /// Stops the associated data.
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;
        _isPaused = false;

        foreach (var em in Emitters)
        {
            em.Particles = Array.Empty<ParticleData>();
            em.ActiveCount = 0;
            em.GpuBuffer = null;
            em.Rng = null;
            em.InstanceTransforms = null;

            if (em.InstancedMesh != null)
            {
                if (_children.Contains(em.InstancedMesh))
                    RemoveChild(em.InstancedMesh, AttachToParentRule.KeepWorld);
                em.InstancedMesh = null;
            }
        }
    }

    /// <summary>
    /// Pauses the associated data.
    /// </summary>
    public void Pause() => _isPaused = !_isPaused;

    /// <summary>
    /// Notifies the gpu resources changed.
    /// </summary>
    public void NotifyGpuResourcesChanged() { }

    // ---- Update ----

    /// <summary>
    /// Updates the associated data.
    /// </summary>
    public override void Update(double delta)
    {
        base.Update(delta);
        if (!_isPlaying || _isPaused) return;
        float dt = (float)delta;

        UpdateCachedFrustumPlanes();

        bool isVisible = !EnableVisibilityCulling
            || _estimatedBbox == null
            || _cachedFrustumPlanes == null
            || _estimatedBbox.IsBoxInsideFrustum(_cachedFrustumPlanes);

        if (!isVisible)
        {
            _accumulatedSkippedTime += dt;
            return;
        }

        if (_accumulatedSkippedTime > 0f)
        {
            float catchUp = MathF.Min(_accumulatedSkippedTime, 2f);
            _accumulatedSkippedTime = 0f;
            Simulate(catchUp);
        }

        Simulate(dt);
    }

    private void Simulate(float dt)
    {
        var worldPos = WorldTransform.Translation;
        var worldRot = WorldTransform.Rotation();

        foreach (var em in Emitters)
        {
            if (em.Rng == null || em.Particles == null) continue;

            ParticleSimulation.Update(em, dt, em.Rng, worldPos, worldRot);

            if (em.UseMeshRenderer)
            {
                em.UpdateMeshInstances(worldRot);
            }
            else
            {
                em.GpuBuffer?.SetParticleData(em.Particles, em.ActiveCount);
            }
        }
    }

    // ---- Frustum culling ----

    private void UpdateCachedFrustumPlanes()
    {
        if (!EnableVisibilityCulling) return;
        var cameras = CurrentScene?.RenderPipeline?.Cameras;
        if (cameras == null || cameras.Count == 0) return;
        var cam = cameras[0];
        if (!cam.Enable) return;

        var vp = cam.View * cam.Projection;
        _cachedFrustumPlanes ??= new Plane[6];
        MatrixHelper.ExtractPlanes(vp, _cachedFrustumPlanes);
    }

    // ---- Bounding box estimation ----

    /// <summary>
    /// Estimate a static world bounding box from all emitter settings.
    /// Uses max velocity × max lifetime to determine spread radius.
    /// </summary>
    private BoundingBox? EstimateWorldBoundingBox()
    {
        if (Emitters.Count == 0) return null;

        float maxVx = 0f, maxVy = 0f, maxVz = 0f;
        float maxLifetime = 0f;
        float maxSize = 0f;
        float gravityY = 0f;

        foreach (var em in Emitters)
        {
            maxVx = MathF.Max(maxVx, MathF.Max(MathF.Abs(em.Velocity.Min.X), MathF.Abs(em.Velocity.Max.X)));
            maxVy = MathF.Max(maxVy, MathF.Max(MathF.Abs(em.Velocity.Min.Y), MathF.Abs(em.Velocity.Max.Y)));
            maxVz = MathF.Max(maxVz, MathF.Max(MathF.Abs(em.Velocity.Min.Z), MathF.Abs(em.Velocity.Max.Z)));

            maxLifetime = MathF.Max(maxLifetime, em.Lifetime.Max);
            maxSize = MathF.Max(maxSize, em.StartSize.Max);
            gravityY = MathF.Min(gravityY, em.Gravity.Y);
        }

        float rx = maxVx * maxLifetime * 0.2f + maxSize;
        float rz = maxVz * maxLifetime * 0.2f + maxSize;

        float upY, downY;
        if (gravityY < 0)
        {
            float g = MathF.Abs(gravityY);
            upY = (maxVy * maxVy) / (2f * g) + maxSize;
            float velDown = maxVy * maxLifetime * 0.2f;
            float gravDrop = 0.15f * g * maxLifetime * maxLifetime;
            downY = velDown + gravDrop + maxSize;
        }
        else
        {
            upY = maxVy * maxLifetime * 0.2f + maxSize;
            downY = upY;
        }

        var center = WorldTransform.Translation;
        return new BoundingBox(
            new Vector3(center.X - rx, center.Y - downY, center.Z - rz),
            new Vector3(center.X + rx, center.Y + upY,  center.Z + rz));
    }

    // ---- Fields ----

    private int _maxParticles = 10000;
    private bool _isPlaying, _isPaused;
    private Plane[]? _cachedFrustumPlanes;
    private BoundingBox? _estimatedBbox;
    private float _accumulatedSkippedTime;
}
