using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace Aura3D.Core.Nodes;

/// <summary>
/// Represents the model type.
/// </summary>
public class Model : Node
{
    /// <summary>
    /// Gets or sets the skeleton.
    /// </summary>
    public Skeleton? Skeleton { get; set; }

    /// <summary>
    /// Gets or sets the animation sampler.
    /// </summary>
    public IAnimationSampler? AnimationSampler { get; set; }

    /// <summary>
    /// Gets the meshes.
    /// </summary>
    public IReadOnlyList<Mesh> Meshes => GetNodesInChildren<Mesh>();

    /// <summary>
    /// Gets a value indicating whether the object is skinned model.
    /// </summary>
    public bool IsSkinnedModel => Skeleton != null;

    /// <summary>
    /// Gets or sets the bounding box padding.
    /// </summary>
    public float BoundingBoxPadding { get; set; }

    /// <summary>
    /// Gets or sets the custom bounding box.
    /// </summary>
    public BoundingBox? CustomBoundingBox { get; set; }

    /// <summary>
    /// Updates the associated data.
    /// </summary>
    public override void Update(double delta)
    {
        if (IsSkinnedModel == false)
            return;
        if (AnimationSampler != null)
        {
            if(AnimationSampler.ExternalUpdate == false)
            {
                AnimationSampler.Update(delta);
            }
        }
    }

    /// <summary>
    /// Clones the associated data.
    /// </summary>
    public virtual Model Clone(CopyType copyType = CopyType.SharedResource)
    {
        var model = (Model)clone(this, null, copyType);

        // 复制包围盒相关属性
        model.BoundingBoxPadding = BoundingBoxPadding;
        model.CustomBoundingBox = CustomBoundingBox;

        foreach(var mesh in model.Meshes)
        {
            mesh.Model = model;

            if (copyType == CopyType.SharedResourceData)
            {
                mesh.Geometry = mesh.Geometry?.Clone();
                mesh.Material = mesh.Material?.DeepClone();
            }
            else if (copyType == CopyType.FullCopy)
            {
                mesh.Geometry = mesh.Geometry?.DeepClone();
                mesh.Material = mesh.Material?.DeepClone(deepCopyTextures: true);
            }
        }

        return model;
    }

    /// <summary>
    /// Performs the clone operation.
    /// </summary>
    protected Node clone(Node node, Node? parentNode, CopyType copyType)
    {
        Node? cloneNode = null;

        if (node is Model model)
        {
            cloneNode = new Model();
            ((Model)cloneNode).Skeleton = copyType == CopyType.FullCopy
                ? CloneSkeleton(model.Skeleton)
                : model.Skeleton;
            ((Model)cloneNode).AnimationSampler = copyType == CopyType.FullCopy
                ? null
                : model.AnimationSampler;
        }
        else if (node is Mesh mesh)
        {
            cloneNode = new Mesh();
            ((Mesh)cloneNode).Geometry = mesh.Geometry;
            ((Mesh)cloneNode).Material = mesh.Material;
            ((Mesh)cloneNode).Model = mesh.Model;
        }
        else
        {
            cloneNode = new Node();
        }
        

        cloneNode.LocalTransform = node.LocalTransform;
        cloneNode.Enable = node.Enable;
        cloneNode.Name = node.Name;

        if (parentNode != null)
        {
            parentNode.AddChild(cloneNode, AttachToParentRule.KeepLocal);
        }

        foreach (var child in node.Children)
        {
            clone(child, cloneNode, copyType);
        }
        return cloneNode;

    }

    private static Skeleton? CloneSkeleton(Skeleton? skeleton)
    {
        if (skeleton is null)
            return null;

        var clone = new Skeleton();
        var boneMap = new Dictionary<Bone, Bone>(ReferenceEqualityComparer.Instance);
        var sourceBones = new List<Bone>();
        var visited = new HashSet<Bone>(ReferenceEqualityComparer.Instance);

        void AddBone(Bone? bone)
        {
            if (bone is null || !visited.Add(bone))
                return;

            sourceBones.Add(bone);
            foreach (var child in bone.Children)
                AddBone(child);
        }

        AddBone(skeleton.Root);
        foreach (var bone in skeleton.Bones)
            AddBone(bone);

        foreach (var sourceBone in sourceBones)
        {
            boneMap[sourceBone] = new Bone
            {
                Name = sourceBone.Name,
                Index = sourceBone.Index,
                InverseWorldMatrix = sourceBone.InverseWorldMatrix,
                LocalMatrix = sourceBone.LocalMatrix,
                WorldMatrix = sourceBone.WorldMatrix
            };
        }

        foreach (var sourceBone in sourceBones)
        {
            var clonedBone = boneMap[sourceBone];
            clonedBone.Parent = sourceBone.Parent != null && boneMap.TryGetValue(sourceBone.Parent, out var parent)
                ? parent
                : null;

            foreach (var child in sourceBone.Children)
            {
                if (boneMap.TryGetValue(child, out var clonedChild))
                    clonedBone.Children.Add(clonedChild);
            }
        }

        clone.Root = boneMap.TryGetValue(skeleton.Root, out var clonedRoot)
            ? clonedRoot
            : new Bone();

        foreach (var sourceBone in skeleton.Bones)
        {
            if (boneMap.TryGetValue(sourceBone, out var clonedBone))
                clone.Bones.Add(clonedBone);
        }

        return clone;
    }

    /// <summary>
    /// Gets the bounding box.
    /// </summary>
    public BoundingBox BoundingBox
    {
        get 
        {
            List<BoundingBox> boundingBoxes = [];
            if (Meshes.Count > 0)
            {
                foreach (var mesh in Meshes)
                {
                    if (mesh == null)
                        continue;
                    if (mesh.BoundingBox == null)
                        continue;
                    boundingBoxes.Add(mesh.BoundingBox);
                }
            }
            return BoundingBox.CreateMerged(boundingBoxes);
        }
    }
}


/// <summary>
/// Represents the model helper type.
/// </summary>
public static class ModelHelper
{

    /// <summary>
    /// Performs the calc vertics tbn operation.
    /// </summary>
    public static void CalcVerticsTbn(IReadOnlyList<uint> indices, IReadOnlyList<float> positions, IReadOnlyList<float> vertexNormals, IReadOnlyList<float> uvs, out List<float> tangents, out List<float> bitangents)
    {
        // 参数合法性校验
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(vertexNormals);
        ArgumentNullException.ThrowIfNull(uvs);

        if (indices.Count % 3 != 0)
            throw Aura3D.Core.Exceptions.GeometryErrors.TriangleIndexCount(nameof(indices));

        tangents = new List<float>();
        bitangents = new List<float>();

        var vertexCount = positions.Count / 3;

        if (vertexCount == 0)
            return;

        // 每个顶点上按三角形累加的切线与副切线（未归一化）
        var tan = new Vector3[vertexCount];
        var bitan = new Vector3[vertexCount];

        // 遍历每个三角形（每3个索引为一组）
        for (int i = 0; i < indices.Count; i += 3)
        {
            // 获取三角形的三个顶点索引
            uint i0 = indices[i];
            uint i1 = indices[i + 1];
            uint i2 = indices[i + 2];

            // 提取三个顶点的UV坐标
            float uv0u = uvs[(int)i0 * 2];
            float uv0v = uvs[(int)i0 * 2 + 1];
            float uv1u = uvs[(int)i1 * 2];
            float uv1v = uvs[(int)i1 * 2 + 1];
            float uv2u = uvs[(int)i2 * 2];
            float uv2v = uvs[(int)i2 * 2 + 1];

            // 计算UV的差值
            float deltaU1 = uv1u - uv0u;
            float deltaV1 = uv1v - uv0v;
            float deltaU2 = uv2u - uv0u;
            float deltaV2 = uv2v - uv0v;

            // 计算分母（避免除零）
            float denominator = deltaU1 * deltaV2 - deltaU2 * deltaV1;
            float r = MathF.Abs(denominator) < 1e-6f ? 0 : 1.0f / denominator;

            // 三个顶点的位置（切空间方向由位置对 UV 的变化率定义）
            Vector3 p0 = new(positions[(int)i0 * 3], positions[(int)i0 * 3 + 1], positions[(int)i0 * 3 + 2]);
            Vector3 p1 = new(positions[(int)i1 * 3], positions[(int)i1 * 3 + 1], positions[(int)i1 * 3 + 2]);
            Vector3 p2 = new(positions[(int)i2 * 3], positions[(int)i2 * 3 + 1], positions[(int)i2 * 3 + 2]);

            Vector3 deltaPos1 = p1 - p0;
            Vector3 deltaPos2 = p2 - p0;

            // 该三角形上的切线与副切线
            Vector3 triangleTangent = (deltaPos1 * deltaV2 - deltaPos2 * deltaV1) * r;
            Vector3 triangleBitangent = (deltaPos2 * deltaU1 - deltaPos1 * deltaU2) * r;

            // 累加到三个顶点（副切线累加值保留 UV 手性，用于最终确定方向符号）
            tan[(int)i0] += triangleTangent;
            tan[(int)i1] += triangleTangent;
            tan[(int)i2] += triangleTangent;

            bitan[(int)i0] += triangleBitangent;
            bitan[(int)i1] += triangleBitangent;
            bitan[(int)i2] += triangleBitangent;
        }

        // 逐顶点正交化并归一化，保证 T ⟂ N、B ⟂ N、B ⟂ T
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 n = new(vertexNormals[i * 3], vertexNormals[i * 3 + 1], vertexNormals[i * 3 + 2]);
            n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.UnitY;

            // T = T - N * (N · T)
            Vector3 t = tan[i] - n * Vector3.Dot(n, tan[i]);

            if (t.LengthSquared() < 1e-12f)
            {
                // UV 退化或与法线共线：取一个与法线不共线的方向再正交化
                Vector3 seed = MathF.Abs(n.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
                t = seed - n * Vector3.Dot(n, seed);
            }

            t = Vector3.Normalize(t);

            // 副切线正交化后方向即 ±(N × T)，符号取自累加的 UV 手性，退化时按右手系
            Vector3 axis = Vector3.Cross(n, t);
            Vector3 b = Vector3.Dot(bitan[i], axis) < 0f ? -axis : axis;

            tangents.Add(t.X);
            tangents.Add(t.Y);
            tangents.Add(t.Z);

            bitangents.Add(b.X);
            bitangents.Add(b.Y);
            bitangents.Add(b.Z);
        }
    }
}

