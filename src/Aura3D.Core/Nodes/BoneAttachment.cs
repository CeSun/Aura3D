using Aura3D.Core.Math;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 把节点挂到蒙皮骨骼上的挂载点：每帧从 <see cref="Mesh.AnimationSampler"/> 的 BonesTransform 里
/// 取出目标骨骼的矩阵，经 <see cref="LocalOffset"/> 偏移后写成自身的 <see cref="Node.WorldTransform"/>，
/// 子节点（道具、粒子、点光源等）由此跟着骨骼一起走。
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
    /// Gets or sets the local offset. 在骨骼局部空间里生效（先于骨骼矩阵），
    /// 单位随 <see cref="NormalizeScale"/> 而定：默认是模型局部单位，开启后是世界单位（米）。
    /// </summary>
    public Matrix4x4 LocalOffset { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// 为 true 时挂载点只继承骨骼的位置与朝向，最终缩放取 <see cref="LocalOffset"/> 自带的缩放（默认 1）。
    /// <para>
    /// 很多 glTF 资产在场景根节点上带着整体缩放（例如按厘米建模的模型会有 0.01），这份缩放会通过
    /// <see cref="Node.WorldTransform"/> 传递到整个挂载子树，让按米建模的道具被缩小上百倍。开启本开关
    /// 就把这层缩放从挂载链里剔除，道具按世界单位建模即可直接贴在骨骼上。默认 false：完整继承模型缩放。
    /// </para>
    /// <para>
    /// 变换里含非均匀缩放或切变时，取出的旋转只是近似值。
    /// </para>
    /// </summary>
    public bool NormalizeScale { get; set; }

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
        var world = LocalOffset * boneMatrix * Mesh.WorldTransform;

        if (NormalizeScale)
        {
            // 只取位置与朝向，缩放换回 LocalOffset 自己的（默认 1）：
            // 等价于把模型根节点上的整体缩放从挂载链里剔除，道具回到世界单位。
            world = MatrixHelper.CreateTransform(world.Translation, world.Rotation(), LocalOffset.Scale());
        }

        WorldTransform = world;
    }
}
