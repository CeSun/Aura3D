using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Resources;

/// <summary>
/// 骨骼系统，包含所有骨骼的层级结构
/// </summary>
public class Skeleton
{
    /// <summary>
    /// 所有骨骼的列表
    /// </summary>
    public List<Bone> Bones { get; } = new();

    /// <summary>
    /// 根骨骼
    /// </summary>
    public Bone Root { get; set; } = new();

    /// <summary>
    /// 骨骼名称到索引的映射缓存，用于快速查找
    /// </summary>
    private Dictionary<string, int>? _boneIndexCache;

    /// <summary>
    /// 静态绑定位姿的骨骼矩阵 UBO（懒创建）。
    /// 当网格有 <see cref="IAnimationSampler"/> 时优先使用采样器的缓冲区。
    /// </summary>
    private BoneMatrixBuffer? _boneMatrixBuffer;
    public BoneMatrixBuffer BoneMatrixBuffer
    {
        get
        {
            _boneMatrixBuffer ??= new BoneMatrixBuffer(this, null);
            return _boneMatrixBuffer;
        }
    }

    /// <summary>
    /// 获取骨骼名称到索引的映射。首次访问时构建缓存。
    /// </summary>
    /// <returns>骨骼名称到索引的字典</returns>
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
    /// 根据骨骼名称获取索引
    /// </summary>
    /// <param name="boneName">骨骼名称</param>
    /// <returns>骨骼索引，如果未找到则返回 -1</returns>
    public int GetBoneIndex(string boneName)
    {
        if (GetBoneIndexMap().TryGetValue(boneName, out var index))
        {
            return index;
        }
        return -1;
    }
}

/// <summary>
/// 骨骼类，表示骨骼层级中的一个节点
/// </summary>
public class Bone
{
    /// <summary>
    /// 骨骼名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 骨骼索引
    /// </summary>
    public int Index { get; set; } = -1;

    /// <summary>
    /// 逆世界矩阵，用于蒙皮
    /// </summary>
    public Matrix4x4 InverseWorldMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// 局部矩阵
    /// </summary>
    public Matrix4x4 LocalMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// 世界矩阵
    /// </summary>
    public Matrix4x4 WorldMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// 父骨骼
    /// </summary>
    public Bone? Parent { get; set; }

    /// <summary>
    /// 子骨骼列表
    /// </summary>
    public List<Bone> Children { get; } = new();
}

