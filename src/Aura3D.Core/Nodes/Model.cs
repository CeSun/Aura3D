using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 模型节点，用于组织和管理包含多个 <see cref="Mesh"/> 的层级结构，支持骨骼动画。
/// </summary>
public class Model : Node
{
    /// <summary>
    /// 获取或设置模型的骨骼数据。
    /// </summary>
    public Skeleton? Skeleton { get; set; }

    /// <summary>
    /// 获取或设置模型的动画采样器。
    /// </summary>
    public IAnimationSampler? AnimationSampler { get; set; }

    /// <summary>
    /// 获取模型及其子节点中的所有网格。
    /// </summary>
    public IReadOnlyList<Mesh> Meshes => GetNodesInChildren<Mesh>();

    /// <summary>
    /// 获取一个值，指示该模型是否为骨骼模型。
    /// </summary>
    public bool IsSkinnedModel => Skeleton != null;

    /// <summary>
    /// 获取或设置包围盒的扩展量（模型局部空间），用于补偿动画导致的肢体超出。
    /// </summary>
    public float BoundingBoxPadding { get; set; }

    /// <summary>
    /// 获取或设置开发者手动指定的模型局部空间包围盒。
    /// 非 null 时将覆盖所有自动计算的包围盒。
    /// </summary>
    public BoundingBox? CustomBoundingBox { get; set; }

    /// <summary>
    /// 更新模型状态，驱动骨骼动画。包围盒不再随动画更新，仅在 WorldTransform 变更时刷新。
    /// </summary>
    /// <param name="delta">时间增量（秒）。</param>
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
    /// 克隆当前模型及其子节点。
    /// </summary>
    /// <param name="copyType">克隆类型，决定资源是共享还是深拷贝。</param>
    /// <returns>克隆后的模型。</returns>
    public virtual Model Clone(CopyType copyType = CopyType.SharedResource)
    {
        var model = (Model)clone(this, null);

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

    protected Node clone(Node node, Node? parentNode)
    {
        Node? cloneNode = null;

        if (node is Model model)
        {
            cloneNode = new Model();
            ((Model)cloneNode).Skeleton = model.Skeleton;
            ((Model)cloneNode).AnimationSampler = model.AnimationSampler;
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
            clone(child, cloneNode);
        }
        return cloneNode;

    }

    /// <summary>
    /// 获取模型所有网格合并后的边界框。
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
/// 提供模型相关的辅助方法。
/// </summary>
public static class ModelHelper
{

    /// <summary>
    /// 计算顶点的切线与副切线（TBN）。
    /// </summary>
    /// <param name="indices">索引列表。</param>
    /// <param name="vertexNormals">顶点法线列表。</param>
    /// <param name="uvs">纹理坐标列表。</param>
    /// <param name="tangents">输出的切线列表。</param>
    /// <param name="bitangents">输出的副切线列表。</param>
    public static void CalcVerticsTbn(List<uint> indices, List<float> vertexNormals, List<float> uvs, out List<float> tangents, out List<float> bitangents)
    {
        tangents = new List<float>();
        bitangents = new List<float>();

        // 参数合法性校验
        if (indices == null || vertexNormals == null || uvs == null)
            throw new ArgumentException("输入列表不能为null");

        if (indices.Count % 3 != 0)
            throw new ArgumentException("索引列表长度必须是3的倍数（每个三角形3个索引）");

        // 计算顶点数量（假设索引是连续的，取最大索引+1）
        /*
        uint maxIndex = 0;
        foreach (uint idx in indices)
        {
            if (idx > maxIndex) maxIndex = idx;
        }
        int vertexCount = (int)maxIndex + 1;
        
        // 验证法线和UV数据长度是否匹配顶点数量
        // if (vertexNormals.Count != vertexCount * 3)
        //    throw new ArgumentException($"法线列表长度应为{vertexCount * 3}（每个顶点3个分量），实际为{vertexNormals.Count}");

        if (uvs.Count != vertexCount * 2)
            throw new ArgumentException($"UV列表长度应为{vertexCount * 2}（每个顶点2个分量），实际为{uvs.Count}");
        */
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


