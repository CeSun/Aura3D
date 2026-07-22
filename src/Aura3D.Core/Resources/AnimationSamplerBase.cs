using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the animation sampler base type.
/// </summary>
public abstract class AnimationSamplerBase : IAnimationSampler
{
    /// <summary>
    /// Gets the skeleton.
    /// </summary>
    public Skeleton Skeleton { get; }

    /// <summary>
    /// Gets the bone matrix buffer.
    /// </summary>
    public BoneMatrixBuffer BoneMatrixBuffer { get; }

    /// <summary>
    /// Gets the bones transform.
    /// </summary>
    public IReadOnlyList<Matrix4x4> BonesTransform => _bonesTransform;

    /// <summary>
    /// Gets the bones transform.
    /// </summary>
    protected readonly Matrix4x4[] _bonesTransform;

    /// <summary>
    /// Gets or sets the external update.
    /// </summary>
    public bool ExternalUpdate { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the animation sampler base type.
    /// </summary>
    protected AnimationSamplerBase(Skeleton skeleton)
    {
        Skeleton = skeleton;
        _bonesTransform = new Matrix4x4[skeleton.Bones.Count];
        BoneMatrixBuffer = new BoneMatrixBuffer(Skeleton, this);
    }

    /// <summary>
    /// Initializes the pose from.
    /// </summary>
    protected void InitializePoseFrom(IReadOnlyList<Matrix4x4> source)
    {
        for (var i = 0; i < _bonesTransform.Length; i++)
        {
            _bonesTransform[i] = source[i];
        }
    }

    /// <summary>
    /// Initializes the pose from world matrices.
    /// </summary>
    protected void InitializePoseFromWorldMatrices()
    {
        for (var i = 0; i < _bonesTransform.Length; i++)
        {
            _bonesTransform[i] = Skeleton.Bones[i].WorldMatrix;
        }
    }

    /// <inheritdoc />
    public abstract void Update(double deltaTime);

    /// <inheritdoc />
    public abstract void Reset();
}