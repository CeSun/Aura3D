using Aura3D.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// 骨骼系统，包含骨架层级结构与运行时缓存。
/// </summary>
[AuraChunk(chunkType: 5, chunkVersion: 1)]
public partial class Skeleton
{
    private List<Bone> _bones = new();
    private int _rootIndex = -1;
    private Bone _root = new();
    private Dictionary<string, int>? _boneIndexCache;
    private BoneMatrixBuffer? _boneMatrixBuffer;

    /// <summary>
    /// 所有骨骼列表。
    /// </summary>
    [AuraField(since: 1)]
    public List<Bone> Bones
    {
        get => _bones;
        set
        {
            _bones = value ?? new List<Bone>();
            RebuildHierarchy();
        }
    }

    /// <summary>
    /// 根骨骼索引。用于反序列化后重建 <see cref="Root"/>。
    /// </summary>
    [AuraField(since: 1)]
    public int RootIndex
    {
        get => _rootIndex;
        set
        {
            _rootIndex = value;
            RebuildHierarchy();
        }
    }

    /// <summary>
    /// 运行时根骨骼，不单独参与序列化。
    /// </summary>
    public Bone Root
    {
        get => _root;
        set
        {
            _root = value;
            _rootIndex = value.Index;
            InvalidateRuntimeState();
        }
    }

    /// <summary>
    /// 静态绑定姿态的骨骼矩阵缓冲区，按需创建。
    /// </summary>
    private BoneMatrixBuffer BoneMatrixBufferInternal
    {
        get
        {
            _boneMatrixBuffer ??= new BoneMatrixBuffer(this, null);
            return _boneMatrixBuffer;
        }
    }

    public BoneMatrixBuffer BoneMatrixBuffer => BoneMatrixBufferInternal;

    /// <summary>
    /// 获取骨骼名到索引的映射。
    /// </summary>
    public Dictionary<string, int> GetBoneIndexMap()
    {
        if (_boneIndexCache == null)
        {
            _boneIndexCache = new Dictionary<string, int>(Bones.Count);
            foreach (var bone in Bones)
            {
                _boneIndexCache[bone.Name] = bone.Index;
            }
        }

        return _boneIndexCache;
    }

    /// <summary>
    /// 根据骨骼名称获取索引。
    /// </summary>
    public int GetBoneIndex(string boneName)
    {
        return GetBoneIndexMap().TryGetValue(boneName, out var index) ? index : -1;
    }

    private void RebuildHierarchy()
    {
        InvalidateRuntimeState();

        if (_bones.Count == 0)
        {
            _root = new Bone();
            _rootIndex = -1;
            return;
        }

        var boneByIndex = new Dictionary<int, Bone>(_bones.Count);
        foreach (var bone in _bones)
        {
            bone.Children.Clear();
            bone.SetParent(null, updateParentIndex: false);
            boneByIndex[bone.Index] = bone;
        }

        foreach (var bone in _bones)
        {
            if (bone.ParentIndex < 0)
                continue;

            if (!boneByIndex.TryGetValue(bone.ParentIndex, out var parent))
                continue;

            bone.SetParent(parent, updateParentIndex: false);
            parent.Children.Add(bone);
        }

        if (!boneByIndex.TryGetValue(_rootIndex, out var rootBone))
        {
            rootBone = _bones.Find(bone => bone.Parent == null) ?? _bones[0];
            _rootIndex = rootBone.Index;
        }

        _root = rootBone;
    }

    private void InvalidateRuntimeState()
    {
        _boneIndexCache = null;
        _boneMatrixBuffer = null;
    }
}

/// <summary>
/// 骨骼节点。
/// </summary>
[AuraChunk(chunkType: 7, chunkVersion: 1)]
public partial class Bone
{
    [AuraField(since: 1)]
    public string Name = string.Empty;

    [AuraField(since: 1)]
    public int Index = -1;

    /// <summary>
    /// 父骨骼索引。用于反序列化后重建层级。
    /// </summary>
    [AuraField(since: 1)]
    public int ParentIndex = -1;

    [AuraField(since: 1)]
    public Matrix4x4 InverseWorldMatrix = Matrix4x4.Identity;

    [AuraField(since: 1)]
    public Matrix4x4 LocalMatrix = Matrix4x4.Identity;

    [AuraField(since: 1)]
    public Matrix4x4 WorldMatrix = Matrix4x4.Identity;

    private Bone? _parent;

    // Parent/Children are runtime-only and rebuilt from ParentIndex after deserialization.
    public Bone? Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            ParentIndex = value?.Index ?? -1;
        }
    }

    public List<Bone> Children = new();

    internal void SetParent(Bone? parent, bool updateParentIndex)
    {
        _parent = parent;
        if (updateParentIndex)
        {
            ParentIndex = parent?.Index ?? -1;
        }
    }
}
