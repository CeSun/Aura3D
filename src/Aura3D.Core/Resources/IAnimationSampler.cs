using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Resources;

/// <summary>
/// Defines the contract for animation sampler.
/// </summary>
public interface IAnimationSampler
{
    /// <summary>
    /// Gets or sets the external update.
    /// </summary>
    public bool ExternalUpdate { get; set; }
    /// <summary>
    /// Gets the skeleton.
    /// </summary>
    public Skeleton Skeleton { get; }
    /// <summary>
    /// Gets the bones transform.
    /// </summary>
    public IReadOnlyList<Matrix4x4> BonesTransform { get; }
    /// <summary>
    /// Gets the bone matrix buffer.
    /// </summary>
    public BoneMatrixBuffer BoneMatrixBuffer { get; }
    /// <summary>
    /// Updates the associated data.
    /// </summary>
    public void Update(double deltaTime);
    /// <summary>
    /// Resets the associated data.
    /// </summary>
    public void Reset();
}
