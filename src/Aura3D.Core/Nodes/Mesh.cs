using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using Aura3D.Core.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 网格节点，包含几何体与材质信息，支持静态网格与骨骼网格。
/// </summary>
[AuraChunk(chunkType: AuraChunkType.Mesh, chunkVersion: 1)]
public partial class Mesh : Node, IOctreeObject
{
    private Material? material;

    /// <summary>
    /// 获取或设置网格的材质。
    /// </summary>
    [AuraField(since: 1)]
    [AuraReference]
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
    /// 获取一个值，指示该网格是否为骨骼网格。
    /// </summary>
    [MemberNotNullWhen(returnValue: true, nameof(Model), nameof(Skeleton))]
    public bool IsSkinnedMesh => Model != null && Model.Skeleton != null;

    /// <summary>
    /// 获取一个值，指示该网格是否为静态网格。
    /// </summary>
    [MemberNotNullWhen(returnValue: false, nameof(Model), nameof(Skeleton))]
    public bool IsStaticMesh => !IsSkinnedMesh;

    /// <summary>
    /// 世界空间中的边界框。
    /// Geometry 顶点 AABB 经 WorldTransform 变换，可经 Model.CustomBoundingBox 覆盖。
    /// </summary>
    public BoundingBox? BoundingBox => boundingBox;

    /// <summary>
    /// 获取或设置网格的几何体数据。
    /// </summary>
    [AuraField(since: 1)]
    [AuraReference]
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
    /// 获取或设置该网格所属的模型。
    /// </summary>
    public Model? Model { get; set; }

    /// <summary>
    /// 获取所属节点列表。
    /// </summary>
    public List<object> BelongingNodes => belongingNodes;

    private List<object> belongingNodes = [];

    /// <summary>
    /// 获取局部空间中的边界框，委托给 Geometry 计算并缓存。
    /// </summary>
    public BoundingBox? LocalBoundingBox => Geometry?.BoundingBox;

    /// <summary>
    /// 当边界框发生变化时触发的事件。
    /// </summary>
    public event Action<IOctreeObject>? OnBoundingBoxChanged = delegate { };

    /// <summary>
    /// 计算局部空间中的 AABB。
    /// CustomBoundingBox 优先，否则回退到 Geometry.BoundingBox（含 padding）。
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
    /// 更新世界空间中的边界框。
    /// 先获取局部 AABB，再经 WorldTransform 变换。
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
    /// 获取骨骼数据。
    /// </summary>
    public Skeleton? Skeleton => Model?.Skeleton;

    /// <summary>
    /// 获取动画采样器。
    /// </summary>
    public IAnimationSampler? AnimationSampler => Model?.AnimationSampler;

    protected override void OnWorldTransformChanged()
    {
        base.OnWorldTransformChanged();
        UpdateWorldBoundingBox();
        OnBoundingBoxChanged?.Invoke(this);
    }


}
