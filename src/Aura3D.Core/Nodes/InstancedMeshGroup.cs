using Aura3D.Core.Math;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 类似 UE Hierarchical Instanced Static Mesh (HISM) 的层次化实例网格组。
/// 将大量实例按空间位置通过八叉树分组，每组生成一个 <see cref="InstancedMesh"/>，
/// 借助 <see cref="InstancedMesh"/> 自带的视锥体剔除实现高效的按组裁剪。
///
/// <para>异步重建：<see cref="Build"/> 将八叉树构建、实例数据填充等 CPU 重活
/// 放到后台线程执行，主线程仅在 <see cref="BuildIfNeeded"/> 中完成场景图操作
/// （旧分组移除、新分组挂载），GPU 上传由 RenderPipeline 自动处理。</para>
///
/// <para>增量更新：当 <see cref="UpdateInstance"/> 发现实例仍在原空间分组内时，
/// 原地修改 InstancedMesh 的实例数据并递增资源版本，不触发重建；
/// 仅在实例跨分组移动、增删实例时才回退到全量重建。</para>
///
/// 使用方式：
/// <code>
/// var group = new InstancedMeshGroup(sourceMesh);
/// group.SetInstances(transforms);
/// group.Build();  // 立即返回，后台线程执行
/// scene.AddNode(group);
///
/// // 每帧 Update 自动调用 BuildIfNeeded() 完成主线程收尾
/// </code>
/// </summary>
public class InstancedMeshGroup : Node
{
    /// <summary>
    /// 初始化实例网格组。
    /// </summary>
    /// <param name="sourceMesh">源网格，所有实例将共享此网格的几何体与材质。</param>
    /// <exception cref="ArgumentNullException">sourceMesh 为 null 时抛出。</exception>
    public InstancedMeshGroup(Mesh sourceMesh)
    {
        SourceMesh = sourceMesh ?? throw new ArgumentNullException(nameof(sourceMesh));
        Name = "InstancedMeshGroup";
    }

    /// <summary>
    /// 获取源网格。
    /// </summary>
    public Mesh SourceMesh { get; }

    /// <summary>
    /// 每个 InstancedMesh 分组最多容纳的实例数。默认 1024。
    /// 数值越小分组越细，剔除精度越高但 DrawCall 也越多。
    /// </summary>
    public int MaxInstancesPerGroup { get; set; } = 1024;

    /// <summary>
    /// 八叉树最大深度。默认 6。
    /// 防止实例过于密集时无限递归。
    /// </summary>
    public int MaxDepth { get; set; } = 6;

    /// <summary>
    /// 获取当前已创建的分组列表（调用 <see cref="Build"/> 后可用）。
    /// </summary>
    public IReadOnlyList<InstancedMesh> Groups => _groups;

    /// <summary>
    /// 获取实例总数。
    /// </summary>
    public int InstanceCount => _transforms.Count;

    /// <summary>
    /// 获取分组数量。
    /// </summary>
    public int GroupCount => _groups.Count;

    /// <summary>
    /// 获取自上次重建以来执行过的原地更新次数。
    /// </summary>
    public int InPlaceUpdateCount { get; private set; }

    /// <summary>
    /// 获取自上次重建以来触发过的全量重建次数。
    /// </summary>
    public int RebuildCount { get; private set; }

    /// <summary>
    /// 获取是否有重建任务正在后台执行。
    /// </summary>
    public bool IsBuilding => _buildTask != null && !_buildTask.IsCompleted;

    private readonly List<InstancedMesh> _groups = new();
    private readonly List<Matrix4x4> _transforms = new();
    private readonly List<int> _instanceGroupIndex = new();
    private readonly List<int> _instanceIndexInGroup = new();
    private InstanceOctreeNode? _rootNode;
    private bool _needsBuild;
    private bool _built;

    // 异步重建
    private Task? _buildTask;
    private BuildResult? _pendingResult;
    private CancellationTokenSource? _buildCts;

    /// <summary>
    /// 后台构建的结果，由工作线程产出、主线程消费。
    /// </summary>
    private sealed class BuildResult
    {
        public InstanceOctreeNode RootNode = null!;
        public List<InstancedMesh> Groups = null!;
        public List<int> InstanceGroupIndex = null!;
        public List<int> InstanceIndexInGroup = null!;
    }

    // ========================================================================
    // Public API
    // ========================================================================

    /// <summary>
    /// 一次性设置所有实例变换，取消当前后台构建并标记需要重建。
    /// </summary>
    /// <param name="transforms">实例的世界变换矩阵列表。</param>
    public void SetInstances(IReadOnlyList<Matrix4x4> transforms)
    {
        CancelBuild();
        _transforms.Clear();
        _transforms.AddRange(transforms);
        Invalidate();
    }

    /// <summary>
    /// 追加一个实例并标记需要重建。
    /// </summary>
    /// <param name="transform">实例的世界变换矩阵。</param>
    /// <returns>新实例的索引。</returns>
    public int AddInstance(Matrix4x4 transform)
    {
        _transforms.Add(transform);
        _needsBuild = true;
        return _transforms.Count - 1;
    }

    /// <summary>
    /// 批量追加实例并标记需要重建。
    /// </summary>
    /// <param name="transforms">要追加的变换矩阵集合。</param>
    public void AddInstances(IEnumerable<Matrix4x4> transforms)
    {
        _transforms.AddRange(transforms);
        _needsBuild = true;
    }

    /// <summary>
    /// 更新指定索引的实例变换。
    /// 同区域内 → O(1) 原地更新；跨区域 → 标记重建。
    /// </summary>
    public void UpdateInstance(int index, Matrix4x4 transform)
    {
        if (index < 0 || index >= _transforms.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _transforms[index] = transform;

        if (TryIncrementalUpdate(index, transform))
            return;

        _needsBuild = true;
    }

    /// <summary>
    /// 移除指定索引的实例并标记需要重建。
    /// </summary>
    public void RemoveInstance(int index)
    {
        if (index < 0 || index >= _transforms.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _transforms.RemoveAt(index);
        _needsBuild = true;
    }

    /// <summary>
    /// 清空所有实例。
    /// </summary>
    public void ClearInstances()
    {
        CancelBuild();
        _transforms.Clear();
        Invalidate();
    }

    /// <summary>
    /// 启动后台重建（立即返回）。
    /// 八叉树构建、实例分组的 CPU 重活在后台线程执行，
    /// 主线程在 <see cref="BuildIfNeeded"/> 中完成场景图挂载。
    /// </summary>
    public void Build()
    {
        if (_transforms.Count == 0)
        {
            FinalizeEmpty();
            return;
        }

        CancelBuild();

        // 快照当前数据，防止后台线程读取时被主线程修改
        var transforms = new List<Matrix4x4>(_transforms);
        var sourceMesh = SourceMesh;
        var maxPerGroup = MaxInstancesPerGroup;
        var maxDepth = MaxDepth;
        var name = Name;

        _buildCts = new CancellationTokenSource();
        var token = _buildCts.Token;

        _buildTask = Task.Run(() =>
        {
            if (token.IsCancellationRequested) return;

            // 计算整体包围盒
            var overallBB = ComputeOverallBoundingBox(sourceMesh, transforms);
            if (overallBB == null || token.IsCancellationRequested) return;

            // 构建八叉树
            var rootNode = new InstanceOctreeNode(overallBB, -1);
            for (int i = 0; i < transforms.Count; i++)
            {
                rootNode.Insert(i, transforms[i].Translation);
            }
            if (token.IsCancellationRequested) return;
            rootNode.Subdivide(transforms, maxPerGroup, maxDepth);

            // 收集叶子节点
            var leafNodes = new List<InstanceOctreeNode>();
            rootNode.CollectLeaves(leafNodes);
            if (token.IsCancellationRequested) return;

            // 创建 InstancedMesh 并填充实例数据（纯 CPU，不上传 GPU）
            var groups = new List<InstancedMesh>();
            var instanceGroupIndex = new List<int>(new int[transforms.Count]);
            var instanceIndexInGroup = new List<int>(new int[transforms.Count]);

            foreach (var leaf in leafNodes)
            {
                if (token.IsCancellationRequested) return;
                if (leaf.InstanceIndices.Count == 0) continue;

                var groupIdx = groups.Count;
                leaf.GroupIndex = groupIdx;

                var im = InstancedMesh.FromMesh(sourceMesh);
                im.Name = $"{name}_Group{groupIdx}";

                for (int j = 0; j < leaf.InstanceIndices.Count; j++)
                {
                    var instanceIdx = leaf.InstanceIndices[j];
                    im.AddInstance(transforms[instanceIdx]);
                    instanceGroupIndex[instanceIdx] = groupIdx;
                    instanceIndexInGroup[instanceIdx] = j;
                }

                groups.Add(im);
            }

            if (token.IsCancellationRequested) return;

            _pendingResult = new BuildResult
            {
                RootNode = rootNode,
                Groups = groups,
                InstanceGroupIndex = instanceGroupIndex,
                InstanceIndexInGroup = instanceIndexInGroup,
            };
        }, token);
    }

    /// <summary>
    /// 如果后台构建完成则在主线程完成场景图挂载，然后 GPU 上传由 RenderPipeline 处理。
    /// 通常由 <see cref="Update"/> 每帧自动调用。
    /// </summary>
    public void BuildIfNeeded()
    {
        // 1. 后台构建完成 → 主线程收尾
        if (_pendingResult != null)
        {
            FinalizeBuild(_pendingResult);
            _pendingResult = null;
            _buildTask = null;
            _buildCts?.Dispose();
            _buildCts = null;
        }

        // 2. 需要构建且没有进行中的任务 → 启动后台构建
        if (_needsBuild && _buildTask == null)
        {
            Build();
        }
    }

    /// <summary>
    /// 每帧更新：自动调用 <see cref="BuildIfNeeded"/>。
    /// </summary>
    public override void Update(double delta)
    {
        base.Update(delta);
        BuildIfNeeded();
    }

    // ========================================================================
    // Build internals
    // ========================================================================

    private void CancelBuild()
    {
        _buildCts?.Cancel();
        _buildCts?.Dispose();
        _buildCts = null;
        _buildTask = null;
        _pendingResult = null;
    }

    /// <summary>
    /// 在主线程完成构建结果的场景图挂载。
    /// </summary>
    private void FinalizeBuild(BuildResult result)
    {
        DestroyGroups();
        _groups.Clear();
        _instanceGroupIndex.Clear();
        _instanceIndexInGroup.Clear();
        _rootNode = null;
        InPlaceUpdateCount = 0;
        RebuildCount++;

        _rootNode = result.RootNode;
        _groups.AddRange(result.Groups);
        _instanceGroupIndex.AddRange(result.InstanceGroupIndex);
        _instanceIndexInGroup.AddRange(result.InstanceIndexInGroup);

        foreach (var im in _groups)
        {
            AddChild(im, AttachToParentRule.KeepWorld);
        }

        _needsBuild = false;
        _built = true;
    }

    /// <summary>
    /// 空实例时的收尾（直接在主线程完成）。
    /// </summary>
    private void FinalizeEmpty()
    {
        DestroyGroups();
        _groups.Clear();
        _instanceGroupIndex.Clear();
        _instanceIndexInGroup.Clear();
        _rootNode = null;
        InPlaceUpdateCount = 0;
        RebuildCount++;
        _needsBuild = false;
        _built = true;
    }

    private void Invalidate()
    {
        _needsBuild = true;
        _built = false;
        _rootNode = null;
    }

    private void DestroyGroups()
    {
        foreach (var group in _groups)
        {
            if (_children.Contains(group))
                RemoveChild(group, AttachToParentRule.KeepWorld);
        }
    }

    // ========================================================================
    // Incremental update
    // ========================================================================

    private bool TryIncrementalUpdate(int index, Matrix4x4 transform)
    {
        if (!_built || _rootNode == null)
            return false;
        if (index >= _instanceGroupIndex.Count || index >= _instanceIndexInGroup.Count)
            return false;

        var newPos = transform.Translation;
        var targetLeaf = _rootNode.FindLeafForPosition(newPos);
        if (targetLeaf == null || targetLeaf.GroupIndex < 0)
            return false;

        var oldGroupIdx = _instanceGroupIndex[index];
        if (targetLeaf.GroupIndex != oldGroupIdx)
            return false;

        var idxInGroup = _instanceIndexInGroup[index];
        _groups[oldGroupIdx].UpdateInstance(idxInGroup, transform);
        InPlaceUpdateCount++;
        return true;
    }

    // ========================================================================
    // Static helpers (thread-safe, no instance state access)
    // ========================================================================

    private static BoundingBox? ComputeOverallBoundingBox(Mesh sourceMesh, List<Matrix4x4> transforms)
    {
        var localBB = sourceMesh.LocalBoundingBox;
        if (localBB != null && transforms.Count > 0)
        {
            var boxes = new List<BoundingBox>(System.Math.Min(transforms.Count, 1024));
            foreach (var t in transforms)
            {
                boxes.Add(localBB.Transform(t));
            }
            return BoundingBox.CreateMerged(boxes);
        }

        return ComputeBoundingBoxFromPositions(transforms);
    }

    private static BoundingBox? ComputeBoundingBoxFromPositions(List<Matrix4x4> transforms)
    {
        if (transforms.Count == 0)
            return null;

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var t in transforms)
        {
            var pos = t.Translation;
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }

        var padding = new Vector3(0.1f);
        return new BoundingBox(min - padding, max + padding);
    }
}

/// <summary>
/// 内部八叉树节点，用于按空间位置对实例进行分组。
/// Build 后保留树结构供增量更新时按位置查找所属分组。
/// </summary>
internal class InstanceOctreeNode
{
    public BoundingBox Bounds { get; }
    public int GroupIndex { get; set; } = -1;
    public List<int> InstanceIndices { get; } = new();
    public InstanceOctreeNode[]? Children { get; private set; }
    public bool IsLeaf => Children == null;

    public InstanceOctreeNode(BoundingBox bounds, int groupIndex)
    {
        Bounds = bounds;
        GroupIndex = groupIndex;
    }

    public void Insert(int instanceIndex, Vector3 position)
    {
        InstanceIndices.Add(instanceIndex);
    }

    public void Subdivide(List<Matrix4x4> transforms, int maxPerNode, int maxDepth, int currentDepth = 0)
    {
        if (InstanceIndices.Count <= maxPerNode || currentDepth >= maxDepth)
            return;

        var center = Bounds.Center;
        var childSize = Bounds.Size / 2;
        var quarter = childSize / 2;

        Children = new InstanceOctreeNode[8];
        var offsets = new Vector3[]
        {
            new(-1, -1, -1), new( 1, -1, -1),
            new(-1,  1, -1), new(-1, -1,  1),
            new( 1,  1, -1), new( 1, -1,  1),
            new(-1,  1,  1), new( 1,  1,  1),
        };

        for (int i = 0; i < 8; i++)
        {
            var childCenter = center + offsets[i] * quarter;
            Children[i] = new InstanceOctreeNode(
                new BoundingBox(childCenter - childSize / 2, childCenter + childSize / 2),
                -1);
        }

        var remaining = new List<int>();
        foreach (var idx in InstanceIndices)
        {
            var pos = transforms[idx].Translation;
            bool assigned = false;

            for (int i = 0; i < 8; i++)
            {
                if (Children[i].Bounds.Contains(pos))
                {
                    Children[i].InstanceIndices.Add(idx);
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
                remaining.Add(idx);
        }

        InstanceIndices.Clear();
        InstanceIndices.AddRange(remaining);

        bool allChildrenEmpty = true;
        for (int i = 0; i < 8; i++)
        {
            Children[i].Subdivide(transforms, maxPerNode, maxDepth, currentDepth + 1);
            if (Children[i].InstanceIndices.Count > 0 || !Children[i].IsLeaf)
                allChildrenEmpty = false;
        }

        if (allChildrenEmpty && InstanceIndices.Count == 0)
        {
            Children = null;
        }
    }

    public void CollectLeaves(List<InstanceOctreeNode> leaves)
    {
        if (IsLeaf)
        {
            if (InstanceIndices.Count > 0)
                leaves.Add(this);
        }
        else
        {
            foreach (var child in Children!)
            {
                child.CollectLeaves(leaves);
            }
        }
    }

    public InstanceOctreeNode? FindLeafForPosition(Vector3 position)
    {
        if (!Bounds.Contains(position))
            return null;

        if (IsLeaf)
            return InstanceIndices.Count > 0 ? this : null;

        if (Children != null)
        {
            foreach (var child in Children)
            {
                var result = child.FindLeafForPosition(position);
                if (result != null)
                    return result;
            }
        }

        return InstanceIndices.Count > 0 ? this : null;
    }
}
