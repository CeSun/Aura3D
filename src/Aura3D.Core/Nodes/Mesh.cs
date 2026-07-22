using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the mesh type.
/// </summary>
public class Mesh : Node, IOctreeObject
{
    private Material? material;

    /// <summary>
    /// Gets the material.
    /// </summary>
    public Material? Material
    {
        get => material;
        set
        {
            if (material == value) return;
            material = value;
        }
    }

    private Geometry? geometry;

    private BoundingBox? boundingBox;

    /// <summary>
    /// Gets a value indicating whether the object is skinned mesh.
    /// </summary>
    [MemberNotNullWhen(returnValue: true, nameof(Model), nameof(Skeleton))]
    public bool IsSkinnedMesh => Model != null && Model.Skeleton != null;

    /// <summary>
    /// Gets a value indicating whether the object is static mesh.
    /// </summary>
    [MemberNotNullWhen(returnValue: false, nameof(Model), nameof(Skeleton))]
    public bool IsStaticMesh => !IsSkinnedMesh;

    /// <summary>
    /// Gets the bounding box.
    /// </summary>
    public BoundingBox? BoundingBox => boundingBox;

    /// <summary>
    /// Gets the geometry.
    /// </summary>
    public Geometry? Geometry
    {
        get => geometry;
        set
        {
            if (value == geometry)
                return;

            geometry = value;

            UpdateWorldBoundingBox();

            OnBoundingBoxChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public Model? Model { get; set; }

    /// <summary>
    /// Gets the belonging nodes.
    /// </summary>
    public List<object> BelongingNodes => belongingNodes;

    private List<object> belongingNodes = [];

    /// <summary>
    /// Gets the local bounding box.
    /// </summary>
    public BoundingBox? LocalBoundingBox => Geometry?.BoundingBox;

    /// <summary>
    /// Represents the method that handles on bounding box changed.
    /// </summary>
    public event Action<IOctreeObject>? OnBoundingBoxChanged = delegate { };

    /// <summary>
    /// Computes the local bounding box.
    /// </summary>
    private BoundingBox? ComputeLocalBoundingBox()
    {
        // 开发者手动指定 → 直接使用
        if (Model?.CustomBoundingBox != null)
        {
            var bb = Model.CustomBoundingBox;
            if (Model.BoundingBoxPadding > 0)
                bb = bb.Expand(Model.BoundingBoxPadding);
            return bb;
        }

        // 回退到几何体的包围盒（含 padding）
        var fallback = Geometry?.BoundingBox;
        if (fallback != null && Model != null && Model.BoundingBoxPadding > 0)
            fallback = fallback.Expand(Model.BoundingBoxPadding);
        return fallback;
    }

    /// <summary>
    /// Updates the world bounding box.
    /// </summary>
    public virtual void UpdateWorldBoundingBox()
    {
        var localBB = ComputeLocalBoundingBox();

        if (localBB == null)
        {
            boundingBox = null;
            return;
        }
        boundingBox = localBB.Transform(WorldTransform);
    }

    /// <summary>
    /// Gets the skeleton.
    /// </summary>
    public Skeleton? Skeleton => Model?.Skeleton;

    /// <summary>
    /// Gets the animation sampler.
    /// </summary>
    public IAnimationSampler? AnimationSampler => Model?.AnimationSampler;

    /// <summary>
    /// Performs the on world transform changed operation.
    /// </summary>
    protected override void OnWorldTransformChanged()
    {
        base.OnWorldTransformChanged();
        UpdateWorldBoundingBox();
        OnBoundingBoxChanged?.Invoke(this);
    }


}
