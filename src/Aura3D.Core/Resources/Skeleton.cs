using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the skeleton type.
/// </summary>
public class Skeleton
{
    /// <summary>
    /// Gets the bones.
    /// </summary>
    public List<Bone> Bones { get; } = new();

    /// <summary>
    /// Gets or sets the root.
    /// </summary>
    public Bone Root { get; set; } = new();

    /// <summary>
    /// Gets the bone index cache.
    /// </summary>
    private Dictionary<string, int>? _boneIndexCache;

    /// <summary>
    /// Gets the bone matrix buffer.
    /// </summary>
    private BoneMatrixBuffer? _boneMatrixBuffer;
    /// <summary>
    /// Gets the bone matrix buffer.
    /// </summary>
    public BoneMatrixBuffer BoneMatrixBuffer
    {
        get
        {
            _boneMatrixBuffer ??= new BoneMatrixBuffer(this, null);
            return _boneMatrixBuffer;
        }
    }

    /// <summary>
    /// Gets the bone index map.
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
    /// Gets the bone index.
    /// </summary>
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
/// Represents the bone type.
/// </summary>
public class Bone
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the index.
    /// </summary>
    public int Index { get; set; } = -1;

    /// <summary>
    /// Gets or sets the inverse world matrix.
    /// </summary>
    public Matrix4x4 InverseWorldMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// Gets or sets the local matrix.
    /// </summary>
    public Matrix4x4 LocalMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// Gets or sets the world matrix.
    /// </summary>
    public Matrix4x4 WorldMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    public Bone? Parent { get; set; }

    /// <summary>
    /// Gets the children.
    /// </summary>
    public List<Bone> Children { get; } = new();
}

