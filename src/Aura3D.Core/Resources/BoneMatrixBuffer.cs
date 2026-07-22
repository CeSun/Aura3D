using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the bone matrix buffer type.
/// </summary>
public class BoneMatrixBuffer : IVersionedResource
{
    /// <summary>
    /// Defines the max bones value.
    /// </summary>
    public const int MaxBones = 256;

    /// <summary>
    /// Defines the binding index value.
    /// </summary>
    public const uint BindingIndex = 0;

    /// <summary>
    /// Gets the skeleton.
    /// </summary>
    public Skeleton Skeleton { get; }
    /// <summary>
    /// Gets the animation sampler.
    /// </summary>
    public IAnimationSampler? AnimationSampler { get; }
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public ulong Version { get; protected set; } = 1;

    /// <summary>
    /// Initializes a new instance of the bone matrix buffer type.
    /// </summary>
    public BoneMatrixBuffer(Skeleton skeleton, IAnimationSampler? animationSampler = null)
    {
        Skeleton = skeleton;
        AnimationSampler = animationSampler;
    }

    /// <summary>
    /// Marks the modified.
    /// </summary>
    public void MarkModified()
    {
        Version++;
    }

}
