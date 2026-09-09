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

    /// <summary>
    /// Validates and returns a frame delta used by animation samplers.
    /// </summary>
    protected static double ValidateDeltaTime(double deltaTime)
    {
        if (!double.IsFinite(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be finite and non-negative.");
        return deltaTime;
    }

    /// <summary>
    /// Blends two transforms through their translation, rotation, and scale components.
    /// </summary>
    protected static Matrix4x4 BlendTransforms(Matrix4x4 from, Matrix4x4 to, float amount)
    {
        amount = System.Math.Clamp(amount, 0f, 1f);
        if (!Matrix4x4.Decompose(from, out var fromScale, out var fromRotation, out var fromTranslation) ||
            !Matrix4x4.Decompose(to, out var toScale, out var toRotation, out var toTranslation))
        {
            return Matrix4x4.Lerp(from, to, amount);
        }

        return Matrix4x4.CreateScale(Vector3.Lerp(fromScale, toScale, amount))
            * Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(fromRotation, toRotation, amount))
            * Matrix4x4.CreateTranslation(Vector3.Lerp(fromTranslation, toTranslation, amount));
    }

    /// <inheritdoc />
    public abstract void Update(double deltaTime);

    /// <inheritdoc />
    public abstract void Reset();
}
