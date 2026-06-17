using System.Numerics;
using Aura3D.Core.Serialization;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 骨骼附着节点，将其自身变换绑定到指定骨骼的世界矩阵上，
/// 使子节点能够跟随骨骼动画运动。
/// 使用 Mesh 的世界矩阵进行计算，正确处理 Model 与 Mesh 之间的中间节点变换。
/// </summary>
[AuraChunk(chunkType: AuraChunkType.BoneAttachment, chunkVersion: 1)]
public partial class BoneAttachment : Node
{
    /// <summary>
    /// 目标骨骼网格，提供骨骼数据、动画采样器和世界变换
    /// </summary>
    [AuraField(since: 1)]
    [AuraReference]
    public Mesh? Mesh { get; set; }

    /// <summary>
    /// 目标骨骼名称
    /// </summary>
    [AuraField(since: 1)]
    public string BoneName { get; set; } = string.Empty;

    /// <summary>
    /// 相对于骨骼空间的局部偏移矩阵
    /// </summary>
    [AuraField(since: 1)]
    public Matrix4x4 LocalOffset { get; set; } = Matrix4x4.Identity;

    private int _cachedBoneIndex = -1;
    private Mesh? _cachedMesh;

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
