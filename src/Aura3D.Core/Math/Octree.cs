using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Aura3D.Core.Math;

/// <summary>
/// 三维八叉树空间索引，用于高效的空间查询和物体管理（单线程版本）
/// </summary>
public class Octree<T> where T : IOctreeObject
{
    /// <summary>
    /// 八叉树最大深度
    /// </summary>
    public int MaxDepth => _maxDepth;
    private readonly int _maxDepth;

    /// <summary>
    /// 八叉树根节点尺寸
    /// </summary>
    public Vector3 Size => _size;

    private Vector3 _size;
    private readonly Vector3 _initialSize;

    /// <summary>
    /// 八叉树根节点
    /// </summary>
    private OctreeNode<T> _rootNode;

    /// <summary>
    /// 所有加入八叉树的物体
    /// </summary>
    private readonly HashSet<T> _allObjects = new();

    /// <summary>
    /// Query 去重专用，避免每次 Query 分配 HashSet
    /// </summary>
    private readonly HashSet<T> _queryDedupSet = new();

    /// <summary>
    /// 八叉树中存储的物体数量
    /// </summary>
    public int Count => _allObjects.Count;

    /// <summary>
    /// 八叉树中所有物体的只读集合
    /// </summary>
    public IReadOnlyCollection<T> Objects => _allObjects;

    /// <summary>
    /// 初始化八叉树
    /// </summary>
    /// <param name="size">根节点尺寸（沿X/Y/Z轴的长度）</param>
    /// <param name="maxDepth">八叉树最大深度（≥0）</param>
    /// <exception cref="ArgumentOutOfRangeException">maxDepth 为负数时抛出</exception>
    /// <exception cref="ArgumentException">size 无效（零/负数/NaN/Infinity）时抛出</exception>
    public Octree(Vector3 size, int maxDepth)
    {
        if (maxDepth < 0)
            throw Aura3D.Core.Exceptions.SpatialErrors.OctreeMaxDepth(nameof(maxDepth));

        if (BoundingBox.IsInvalidVector(size) || size.X <= 0 || size.Y <= 0 || size.Z <= 0)
            throw Aura3D.Core.Exceptions.SpatialErrors.OctreeSize(nameof(size));

        _size = size;
        _initialSize = size;
        _maxDepth = maxDepth;
        _rootNode = CreateRootNode();
    }

    /// <summary>
    /// 创建八叉树根节点
    /// </summary>
    private OctreeNode<T> CreateRootNode()
    {
        return CreateOctreeNode(Vector3.Zero, _size, 0);
    }

    /// <summary>
    /// 创建八叉树节点
    /// </summary>
    internal OctreeNode<T> CreateOctreeNode(Vector3 center, Vector3 size, int depth)
    {
        return new OctreeNode<T>(this, center, size, depth);
    }

    /// <summary>
    /// 确保根节点包含指定包围盒，必要时扩张并重建
    /// </summary>
    private void EnsureRootContains(BoundingBox bb)
    {
        if (_rootNode.BoundingBox.Contains(bb))
            return;

        var newSize = _size;

        while (!new BoundingBox(
            new Vector3(newSize.X / -2, newSize.Y / -2, newSize.Z / -2),
            new Vector3(newSize.X / 2, newSize.Y / 2, newSize.Z / 2)).Contains(bb))
        {
            if (bb.Max.X > newSize.X / 2 || bb.Min.X < newSize.X / -2)
                newSize.X *= 2;
            if (bb.Max.Y > newSize.Y / 2 || bb.Min.Y < newSize.Y / -2)
                newSize.Y *= 2;
            if (bb.Max.Z > newSize.Z / 2 || bb.Min.Z < newSize.Z / -2)
                newSize.Z *= 2;
        }

        // 清理旧 BelongingNodes 引用，Rebuild 会创建全新根节点重新分配
        foreach (var obj in _allObjects)
            obj.BelongingNodes.Clear();

        Rebuild(newSize);
    }

    private void Rebuild(Vector3 newSize)
    {
        _size = newSize;
        _rootNode = CreateRootNode();

        foreach (var obj in _allObjects)
        {
            _rootNode.Add(obj);
        }
    }

    /// <summary>
    /// 添加物体到八叉树
    /// </summary>
    /// <param name="obj">待添加的物体</param>
    /// <returns>添加成功返回 true，已存在返回 false</returns>
    /// <exception cref="ArgumentNullException">obj 为 null 时抛出</exception>
    /// <exception cref="ArgumentException">obj 的包围盒为 null 或包含无效值时抛出</exception>
    public bool Add(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var bb = obj.BoundingBox;
        if (bb == null)
            throw Aura3D.Core.Exceptions.SpatialErrors.ObjectBoundingBoxNull(nameof(obj));

        if (BoundingBox.IsInvalidVector(bb.Min) ||
            BoundingBox.IsInvalidVector(bb.Max))
            throw Aura3D.Core.Exceptions.SpatialErrors.ObjectBoundingBoxInvalid(nameof(obj));

        if (_allObjects.Contains(obj))
            return false;

        EnsureRootContains(bb);

        _rootNode.Add(obj);
        _allObjects.Add(obj);

        return true;
    }

    /// <summary>
    /// 检查物体是否已加入八叉树
    /// </summary>
    public bool Contains(T obj) => _allObjects.Contains(obj);

    /// <summary>
    /// 从八叉树移除物体
    /// </summary>
    /// <param name="obj">待移除的物体</param>
    /// <returns>移除成功返回 true，不存在返回 false</returns>
    /// <exception cref="ArgumentNullException">obj 为 null 时抛出</exception>
    public bool Remove(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (!_allObjects.Contains(obj))
            return false;

        foreach (var objNode in obj.BelongingNodes.ToArray())
        {
            if (objNode is OctreeNode<T> node)
                node.Remove(obj);
        }
        obj.BelongingNodes.Clear();

        _allObjects.Remove(obj);
        return true;
    }

    /// <summary>
    /// 更新物体在八叉树中的位置。
    /// 如果物体仍在所有所属节点内则跳过（快速路径），否则移除后重新添加并检查扩张。
    /// </summary>
    /// <param name="obj">待更新的物体</param>
    /// <exception cref="ArgumentNullException">obj 为 null 时抛出</exception>
    /// <exception cref="KeyNotFoundException">obj 未加入八叉树时抛出</exception>
    public void Update(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (!_allObjects.Contains(obj))
            throw Aura3D.Core.Exceptions.SpatialErrors.ObjectNotInOctree();

        var bb = obj.BoundingBox;
        if (bb == null)
            throw Aura3D.Core.Exceptions.SpatialErrors.ObjectBoundingBoxNullDuringUpdate();

        // 快速路径：如果仍在所有所属节点内，无需重插
        if (StillContainedInCurrentNodes(obj, bb))
            return;

        // 慢路径：移除后重新添加
        foreach (var objNode in obj.BelongingNodes.ToArray())
        {
            if (objNode is OctreeNode<T> node)
                node.RemoveFromNodeOnly(obj);
        }
        obj.BelongingNodes.Clear();

        EnsureRootContains(bb);

        _rootNode.Add(obj);
    }

    /// <summary>
    /// 检查物体是否仍被其所有所属节点完全包含。
    /// </summary>
    private static bool StillContainedInCurrentNodes(T obj, BoundingBox bb)
    {
        var nodes = obj.BelongingNodes;
        if (nodes.Count == 0)
            return false; // 异常状态：应重新插入

        foreach (var objNode in nodes)
        {
            if (objNode is OctreeNode<T> node)
            {
                if (!node.BoundingBox.Contains(bb))
                    return false;
            }
            else
            {
                return false; // 非节点引用，异常状态
            }
        }

        return true;
    }

    /// <summary>
    /// 收缩八叉树尺寸到当前所有物体的紧致包围盒，或回退到初始尺寸。
    /// 由调用者控制调用时机。
    /// </summary>
    public void Compact()
    {
        if (_allObjects.Count == 0)
        {
            // 空树：回退到初始尺寸
            _size = _initialSize;
            _rootNode = CreateRootNode();
            return;
        }

        // 计算所有物体的紧致 AABB
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        foreach (var obj in _allObjects)
        {
            var bb = obj.BoundingBox;
            if (bb == null) continue;
            min = Vector3.Min(min, bb.Min);
            max = Vector3.Max(max, bb.Max);
        }

        // 保持以原点为中心的立方体形状
        float halfSize = MathF.Max(
            MathF.Max(MathF.Max(MathF.Abs(min.X), MathF.Abs(max.X)),
                      MathF.Max(MathF.Abs(min.Y), MathF.Abs(max.Y))),
            MathF.Max(MathF.Abs(min.Z), MathF.Abs(max.Z)));
        halfSize = MathF.Max(halfSize, 1f);

        var newSize = new Vector3(halfSize * 2);

        // 不比当前小就不重建
        if (newSize.X >= _size.X && newSize.Y >= _size.Y && newSize.Z >= _size.Z)
            return;

        // 清理旧 BelongingNodes，Rebuild 会创建全新根节点重新分配
        foreach (var obj in _allObjects)
            obj.BelongingNodes.Clear();

        Rebuild(newSize);
    }

    /// <summary>
    /// 空间查询：获取指定包围盒内的所有物体
    /// </summary>
    /// <param name="queryBox">查询包围盒</param>
    /// <param name="result">查询结果（输出参数）</param>
    /// <exception cref="ArgumentNullException">queryBox 或 result 为 null 时抛出</exception>
    /// <remarks>不可重入：Query 的 filter 回调中禁止再次调用 Query。</remarks>
    public void Query(BoundingBox queryBox, List<T> result)
    {
        ArgumentNullException.ThrowIfNull(queryBox);
        ArgumentNullException.ThrowIfNull(result);

        _queryDedupSet.Clear();
        _rootNode.Query(queryBox, result, _queryDedupSet);
    }

    /// <summary>
    /// 空间查询：通过过滤函数获取符合条件的物体
    /// </summary>
    /// <param name="filter">判断函数</param>
    /// <param name="result">查询结果（输出参数）</param>
    /// <exception cref="ArgumentNullException">filter 或 result 为 null 时抛出</exception>
    /// <remarks>不可重入：filter 回调中禁止再次调用 Query。</remarks>
    public void Query(Func<BoundingBox, bool> filter, List<T> result)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(result);

        _queryDedupSet.Clear();
        _rootNode.Query(filter, result, _queryDedupSet);
    }

    /// <summary>
    /// 清空八叉树所有物体
    /// </summary>
    public void Clear()
    {
        foreach (var obj in _allObjects)
        {
            obj.BelongingNodes.Clear();
        }
        _rootNode.Clear();
        _allObjects.Clear();
    }
}

/// <summary>
/// 八叉树节点（内部实现）
/// </summary>
internal class OctreeNode<T> where T : IOctreeObject
{
    /// <summary>
    /// 子节点偏移量，8个方向
    /// </summary>
    private static readonly Vector3[] Offsets =
    [
        new(-1, -1, -1), new( 1, -1, -1),
        new(-1,  1, -1), new(-1, -1,  1),
        new( 1,  1, -1), new( 1, -1,  1),
        new(-1,  1,  1), new( 1,  1,  1)
    ];

    /// <summary>
    /// 所属八叉树
    /// </summary>
    private readonly Octree<T> _octree;

    /// <summary>
    /// 节点深度（根节点为 0）
    /// </summary>
    private readonly int _depth;

    /// <summary>
    /// 节点包围盒
    /// </summary>
    public BoundingBox BoundingBox { get; }

    /// <summary>
    /// 子节点（8个，初始化时为 null，按需创建）
    /// </summary>
    private List<OctreeNode<T>>? _children;

    /// <summary>
    /// 当前节点存储的物体
    /// </summary>
    private readonly HashSet<T> _objects = new();

    /// <summary>
    /// 初始化八叉树节点
    /// </summary>
    internal OctreeNode(Octree<T> octree, Vector3 center, Vector3 size, int depth)
    {
        _octree = octree;
        _depth = depth;
        BoundingBox = new BoundingBox(center - size / 2, center + size / 2);
    }

    /// <summary>
    /// 添加物体到节点（递归）。
    /// 调用者须确保此物体尚未添加到此树，且 BelongingNodes 已清理。
    /// </summary>
    internal void Add(T obj)
    {
        Debug.Assert(obj.BoundingBox != null, "物体的包围盒在添加到节点前不能为 null，调用者应在 Octree<T>.Add/Update 中校验");

        var bb = obj.BoundingBox!;

        // 达到最大深度，直接添加到当前节点
        if (_depth >= _octree.MaxDepth)
        {
            _objects.Add(obj);
            obj.BelongingNodes.Add(this);
            return;
        }

        // 物体尺寸超过子节点尺寸，直接添加到当前节点
        var childSize = BoundingBox.Size / 2;
        if (bb.Size.X > childSize.X + BoundingBox.DefaultEpsilon ||
            bb.Size.Y > childSize.Y + BoundingBox.DefaultEpsilon ||
            bb.Size.Z > childSize.Z + BoundingBox.DefaultEpsilon)
        {
            _objects.Add(obj);
            obj.BelongingNodes.Add(this);
            return;
        }

        // 按需创建子节点
        EnsureChildrenCreated();

        // 将物体添加到所有相交的子节点
        bool addedToChild = false;
        foreach (var child in _children!)
        {
            if (child.BoundingBox.Intersects(bb))
            {
                child.Add(obj);
                addedToChild = true;
            }
        }

        // 无相交子节点时，添加到当前节点
        if (!addedToChild)
        {
            _objects.Add(obj);
            obj.BelongingNodes.Add(this);
        }
    }

    /// <summary>
    /// 从节点移除物体（递归，同时清理 BelongingNodes 中指向此节点的引用）。
    /// 移除后自动剪枝：如果当前节点及所有子节点均为空，释放子节点。
    /// </summary>
    internal void Remove(T obj)
    {
        _objects.Remove(obj);
        obj.BelongingNodes.Remove(this);

        if (_children != null)
        {
            foreach (var child in _children)
                child.Remove(obj);

            TryPruneChildren();
        }
    }

    /// <summary>
    /// 仅从节点的物体集合中移除，不触碰 BelongingNodes。
    /// 用于 Rebuild / EnsureRootContains 等由调用者统一管理 BelongingNodes 的场景。
    /// 移除后自动剪枝。
    /// </summary>
    internal void RemoveFromNodeOnly(T obj)
    {
        _objects.Remove(obj);

        if (_children != null)
        {
            foreach (var child in _children)
                child.RemoveFromNodeOnly(obj);

            TryPruneChildren();
        }
    }

    /// <summary>
    /// 如果当前节点及其所有子节点均为空，则停用子节点列表以节省内存和加速遍历。
    /// </summary>
    private void TryPruneChildren()
    {
        if (_children == null || _objects.Count > 0)
            return;

        foreach (var child in _children)
        {
            if (child._objects.Count > 0 || child._children != null)
                return;
        }

        _children = null;
    }

    /// <summary>
    /// 空间查询：获取与查询盒相交的物体
    /// </summary>
    internal void Query(BoundingBox queryBox, List<T> result, HashSet<T> dedupSet)
    {
        if (!BoundingBox.Intersects(queryBox))
            return;

        foreach (var obj in _objects)
        {
            var bb = obj.BoundingBox;
            if (bb != null && bb.Intersects(queryBox) && dedupSet.Add(obj))
                result.Add(obj);
        }

        if (_children != null)
        {
            foreach (var child in _children)
                child.Query(queryBox, result, dedupSet);
        }
    }

    /// <summary>
    /// 空间查询：通过过滤函数获取符合条件的物体
    /// </summary>
    internal void Query(Func<BoundingBox, bool> filter, List<T> result, HashSet<T> dedupSet)
    {
        if (!filter.Invoke(this.BoundingBox))
            return;

        foreach (var obj in _objects)
        {
            var bb = obj.BoundingBox;
            if (bb != null && filter.Invoke(bb) && dedupSet.Add(obj))
                result.Add(obj);
        }

        if (_children != null)
        {
            foreach (var child in _children)
                child.Query(filter, result, dedupSet);
        }
    }

    /// <summary>
    /// 清空节点所有物体（递归），释放子节点以回收内存。
    /// </summary>
    internal void Clear()
    {
        _objects.Clear();
        _children = null;
    }

    /// <summary>
    /// 确保子节点已创建（按需初始化）
    /// </summary>
    private void EnsureChildrenCreated()
    {
        if (_children != null)
            return;

        _children = new List<OctreeNode<T>>(8);
        var center = BoundingBox.Center;
        var childSize = BoundingBox.Size / 2;
        var quarterSize = BoundingBox.Size / 4;

        foreach (var offset in Offsets)
        {
            var childCenter = center + offset * quarterSize;
            _children.Add(_octree.CreateOctreeNode(childCenter, childSize, _depth + 1));
        }
    }
}
