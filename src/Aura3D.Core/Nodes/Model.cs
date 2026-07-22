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
    public static void CalcVerticsTbn(IReadOnlyList<uint> indices, IReadOnlyList<float> vertexNormals, IReadOnlyList<float> uvs, out List<float> tangents, out List<float> bitangents)
    {
        tangents = new List<float>();
        bitangents = new List<float>();

        // 参数合法性校验
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(vertexNormals);
        ArgumentNullException.ThrowIfNull(uvs);

        if (indices.Count % 3 != 0)
            throw Aura3D.Core.Exceptions.GeometryErrors.TriangleIndexCount(nameof(indices));

        // 初始化切线和副切线数组（初始值为0）
        float[] tan = new float[vertexNormals.Count];
        float[] bitan = new float[vertexNormals.Count];

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

            // 提取三个顶点的法线（作为临时位置向量，实际应传入顶点位置，这里用法线替代）
            // 注意：完整实现应传入顶点位置列表，此处为适配你的函数参数，临时用法线替代
            float v0x = vertexNormals[(int)i0 * 3];
            float v0y = vertexNormals[(int)i0 * 3 + 1];
            float v0z = vertexNormals[(int)i0 * 3 + 2];
            float v1x = vertexNormals[(int)i1 * 3];
            float v1y = vertexNormals[(int)i1 * 3 + 1];
            float v1z = vertexNormals[(int)i1 * 3 + 2];
            float v2x = vertexNormals[(int)i2 * 3];
            float v2y = vertexNormals[(int)i2 * 3 + 1];
            float v2z = vertexNormals[(int)i2 * 3 + 2];

            // 计算位置差值
            float deltaPos1x = v1x - v0x;
            float deltaPos1y = v1y - v0y;
            float deltaPos1z = v1z - v0z;
            float deltaPos2x = v2x - v0x;
            float deltaPos2y = v2y - v0y;
            float deltaPos2z = v2z - v0z;

            // 计算切线和副切线的临时值
            float tx = (deltaV2 * deltaPos1x - deltaV1 * deltaPos2x) * r;
            float ty = (deltaV2 * deltaPos1y - deltaV1 * deltaPos2y) * r;
            float tz = (deltaV2 * deltaPos1z - deltaV1 * deltaPos2z) * r;

            float bx = (deltaU1 * deltaPos2x - deltaU2 * deltaPos1x) * r;
            float by = (deltaU1 * deltaPos2y - deltaU2 * deltaPos1y) * r;
            float bz = (deltaU1 * deltaPos2z - deltaU2 * deltaPos1z) * r;

            // 将计算结果累加到对应顶点的切线/副切线
            tan[(int)i0 * 3] += tx;
            tan[(int)i0 * 3 + 1] += ty;
            tan[(int)i0 * 3 + 2] += tz;
            tan[(int)i1 * 3] += tx;
            tan[(int)i1 * 3 + 1] += ty;
            tan[(int)i1 * 3 + 2] += tz;
            tan[(int)i2 * 3] += tx;
            tan[(int)i2 * 3 + 1] += ty;
            tan[(int)i2 * 3 + 2] += tz;

            bitan[(int)i0 * 3] += bx;
            bitan[(int)i0 * 3 + 1] += by;
            bitan[(int)i0 * 3 + 2] += bz;
            bitan[(int)i1 * 3] += bx;
            bitan[(int)i1 * 3 + 1] += by;
            bitan[(int)i1 * 3 + 2] += bz;
            bitan[(int)i2 * 3] += bx;
            bitan[(int)i2 * 3 + 1] += by;
            bitan[(int)i2 * 3 + 2] += bz;
        }

        // 对切线进行正交化（确保与法线垂直），并归一化
        for (int i = 0; i < vertexNormals.Count / 3; i++)
        {
            // 获取顶点法线
            float nx = vertexNormals[i * 3];
            float ny = vertexNormals[i * 3 + 1];
            float nz = vertexNormals[i * 3 + 2];

            // 获取累加后的切线
            float tx = tan[i * 3];
            float ty = tan[i * 3 + 1];
            float tz = tan[i * 3 + 2];

            // 正交化：T = T - N * (N · T)
            float dot = nx * tx + ny * ty + nz * tz;
            tx = tx - nx * dot;
            ty = ty - ny * dot;
            tz = tz - nz * dot;

            // 归一化切线
            float length = (float)MathF.Sqrt(tx * tx + ty * ty + tz * tz);
            if (length > 1e-6f)
            {
                tx /= length;
                ty /= length;
                tz /= length;
            }
            else
            {
                // 避免零长度，使用默认值
                tx = 1; ty = 0; tz = 0;
            }

            // 计算副切线（B = N × T）
            float bx = ny * tz - nz * ty;
            float by = nz * tx - nx * tz;
            float bz = nx * ty - ny * tx;

            // 归一化副切线
            length = (float)MathF.Sqrt(bx * bx + by * by + bz * bz);
            if (length > 1e-6f)
            {
                bx /= length;
                by /= length;
                bz /= length;
            }
            else
            {
                bx = 0; by = 1; bz = 0;
            }

            // 将结果添加到输出列表
            tangents.Add(tx);
            tangents.Add(ty);
            tangents.Add(tz);

            bitangents.Add(bx);
            bitangents.Add(by);
            bitangents.Add(bz);
        }
    }
}

