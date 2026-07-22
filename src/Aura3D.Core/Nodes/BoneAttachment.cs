using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the bone attachment type.
/// </summary>
public class BoneAttachment : Node
{
    /// <summary>
    /// Gets or sets the mesh.
    /// </summary>
    public Mesh? Mesh { get; set; }

    /// <summary>
    /// Gets or sets the bone name.
    /// </summary>
    public string BoneName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local offset.
    /// </summary>
    public Matrix4x4 LocalOffset { get; set; } = Matrix4x4.Identity;

    private int _cachedBoneIndex = -1;
    private Mesh? _cachedMesh;

    /// <summary>
    /// Updates the associated data.
    /// </summary>
    public override void Update(double delta)
    {
        if (Mesh == null) 
            return;

        if (!Mesh.IsSkinnedMesh)
            return;

        var sampler = Mesh.AnimationSampler;
        if (sampler == null)
            return;

        var skeleton = Mesh.Skeleton!;

        // 缓存失效时重新查找骨骼索引
        if (_cachedMesh != Mesh || _cachedBoneIndex < 0)
        {
            _cachedMesh = Mesh;
            _cachedBoneIndex = skeleton.GetBoneIndex(BoneName);
        }

        if (_cachedBoneIndex < 0 || _cachedBoneIndex >= sampler.BonesTransform.Count)
            return;

        var boneMatrix = sampler.BonesTransform[_cachedBoneIndex];

        // 与 DebugDrawPass 骨骼调试线完全一致：
        // boneMatrix 在 model-local 空间，Mesh.WorldTransform 变换到世界空间
        WorldTransform = LocalOffset * boneMatrix * Mesh.WorldTransform;
    }
}
