using Aura3D.Core.Math;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the instanced mesh group type.
/// </summary>
public class InstancedMeshGroup : Node
{
    /// <summary>
    /// Initializes a new instance of the instanced mesh group type.
    /// </summary>
    public InstancedMeshGroup(Mesh sourceMesh)
    {
        SourceMesh = sourceMesh ?? throw new ArgumentNullException(nameof(sourceMesh));
        Name = "InstancedMeshGroup";
    }

    /// <summary>
    /// Gets the source mesh.
    /// </summary>
    public Mesh SourceMesh { get; }

    /// <summary>
    /// Gets or sets the max instances per group.
    /// </summary>
    public int MaxInstancesPerGroup { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the max depth.
    /// </summary>
    public int MaxDepth { get; set; } = 6;

    /// <summary>
    /// Gets the groups.
    /// </summary>
    public IReadOnlyList<InstancedMesh> Groups => _groups;

    /// <summary>
    /// Gets the instance count.
    /// </summary>
    public int InstanceCount => _transforms.Count;

    /// <summary>
    /// Gets the group count.
    /// </summary>
    public int GroupCount => _groups.Count;

    /// <summary>
    /// Gets or sets the in place update count.
    /// </summary>
    public int InPlaceUpdateCount { get; private set; }

    /// <summary>
    /// Gets or sets the rebuild count.
    /// </summary>
    public int RebuildCount { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the object is building.
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
    /// Represents the build result type.
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
    /// Sets the instances.
    /// </summary>
    public void SetInstances(IReadOnlyList<Matrix4x4> transforms)
    {
        CancelBuild();
        _transforms.Clear();
        _transforms.AddRange(transforms);
        Invalidate();
    }

    /// <summary>
    /// Adds the instance.
    /// </summary>
    public int AddInstance(Matrix4x4 transform)
    {
        _transforms.Add(transform);
        _needsBuild = true;
        return _transforms.Count - 1;
    }

    /// <summary>
    /// Adds the instances.
    /// </summary>
    public void AddInstances(IEnumerable<Matrix4x4> transforms)
    {
        _transforms.AddRange(transforms);
        _needsBuild = true;
    }

    /// <summary>
    /// Updates the instance.
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
    /// Removes the instance.
    /// </summary>
    public void RemoveInstance(int index)
    {
        if (index < 0 || index >= _transforms.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _transforms.RemoveAt(index);
        _needsBuild = true;
    }

    /// <summary>
    /// Clears the instances.
    /// </summary>
    public void ClearInstances()
    {
        CancelBuild();
        _transforms.Clear();
        Invalidate();
    }

    /// <summary>
    /// Builds the associated data.
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
    /// Builds the if needed.
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
    /// Updates the associated data.
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
    /// Performs the finalize build operation.
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
    /// Performs the finalize empty operation.
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
/// Represents the instance octree node type.
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
