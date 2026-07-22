using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Aura3D.Core.Math;

/// <summary>
/// Represents the octree type.
/// </summary>
public class Octree<T> where T : IOctreeObject
{
    /// <summary>
    /// Gets the max depth.
    /// </summary>
    public int MaxDepth => _maxDepth;
    private readonly int _maxDepth;

    /// <summary>
    /// Gets the size.
    /// </summary>
    public Vector3 Size => _size;

    private Vector3 _size;
    private readonly Vector3 _initialSize;

    /// <summary>
    /// Gets the root node.
    /// </summary>
    private OctreeNode<T> _rootNode;

    /// <summary>
    /// Performs the new operation.
    /// </summary>
    private readonly HashSet<T> _allObjects = new();

    /// <summary>
    /// Performs the new operation.
    /// </summary>
    private readonly HashSet<T> _queryDedupSet = new();

    /// <summary>
    /// Gets the count.
    /// </summary>
    public int Count => _allObjects.Count;

    /// <summary>
    /// Gets the objects.
    /// </summary>
    public IReadOnlyCollection<T> Objects => _allObjects;

    /// <summary>
    /// Initializes a new instance of the octree type.
    /// </summary>
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
    /// Creates the root node.
    /// </summary>
    private OctreeNode<T> CreateRootNode()
    {
        return CreateOctreeNode(Vector3.Zero, _size, 0);
    }

    /// <summary>
    /// Creates the octree node.
    /// </summary>
    internal OctreeNode<T> CreateOctreeNode(Vector3 center, Vector3 size, int depth)
    {
        return new OctreeNode<T>(this, center, size, depth);
    }

    /// <summary>
    /// Ensures the root contains.
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
    /// Adds the associated data.
    /// </summary>
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
    /// Performs the contains operation.
    /// </summary>
    public bool Contains(T obj) => _allObjects.Contains(obj);

    /// <summary>
    /// Removes the associated data.
    /// </summary>
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
    /// Updates the associated data.
    /// </summary>
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
    /// Performs the still contained in current nodes operation.
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
    /// Performs the compact operation.
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
    /// Performs the query operation.
    /// </summary>
    public void Query(BoundingBox queryBox, List<T> result)
    {
        ArgumentNullException.ThrowIfNull(queryBox);
        ArgumentNullException.ThrowIfNull(result);

        _queryDedupSet.Clear();
        _rootNode.Query(queryBox, result, _queryDedupSet);
    }

    /// <summary>
    /// Performs the query operation.
    /// </summary>
    public void Query(Func<BoundingBox, bool> filter, List<T> result)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(result);

        _queryDedupSet.Clear();
        _rootNode.Query(filter, result, _queryDedupSet);
    }

    /// <summary>
    /// Clears the associated data.
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
/// Represents the octree node type.
/// </summary>
internal class OctreeNode<T> where T : IOctreeObject
{
    /// <summary>
    /// Gets the offsets.
    /// </summary>
    private static readonly Vector3[] Offsets =
    [
        new(-1, -1, -1), new( 1, -1, -1),
        new(-1,  1, -1), new(-1, -1,  1),
        new( 1,  1, -1), new( 1, -1,  1),
        new(-1,  1,  1), new( 1,  1,  1)
    ];

    /// <summary>
    /// Gets the octree.
    /// </summary>
    private readonly Octree<T> _octree;

    /// <summary>
    /// Gets the depth.
    /// </summary>
    private readonly int _depth;

    /// <summary>
    /// Gets the bounding box.
    /// </summary>
    public BoundingBox BoundingBox { get; }

    /// <summary>
    /// Gets the children.
    /// </summary>
    private List<OctreeNode<T>>? _children;

    /// <summary>
    /// Performs the new operation.
    /// </summary>
    private readonly HashSet<T> _objects = new();

    /// <summary>
    /// Initializes a new instance of the octree node type.
    /// </summary>
    internal OctreeNode(Octree<T> octree, Vector3 center, Vector3 size, int depth)
    {
        _octree = octree;
        _depth = depth;
        BoundingBox = new BoundingBox(center - size / 2, center + size / 2);
    }

    /// <summary>
    /// Adds the associated data.
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
    /// Removes the associated data.
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
    /// Removes the from node only.
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
    /// Performs the try prune children operation.
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
    /// Performs the query operation.
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
    /// Performs the query operation.
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
    /// Clears the associated data.
    /// </summary>
    internal void Clear()
    {
        _objects.Clear();
        _children = null;
    }

    /// <summary>
    /// Ensures the children created.
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
