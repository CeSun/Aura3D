using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// 专用于 <see cref="Aura3D.Core.Nodes.InstancedMesh"/> 的几何资源。
/// 它不作为可共享资源复用，实例属性数据与基础几何数据一起由该对象独占持有。
/// </summary>
public class InstancedGeometry : Geometry
{
    private static readonly InstanceAttributePointer[] DefaultTransformPointers =
    [
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn0, ComponentCount = 4, Offset = 0 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn1, ComponentCount = 4, Offset = sizeof(float) * 4 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn2, ComponentCount = 4, Offset = sizeof(float) * 8 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn3, ComponentCount = 4, Offset = sizeof(float) * 12 },
    ];

    private static readonly InstanceAttributePointer[] DefaultNormalTransformPointers =
    [
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn0, ComponentCount = 4, Offset = 0 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn1, ComponentCount = 4, Offset = sizeof(float) * 4 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn2, ComponentCount = 4, Offset = sizeof(float) * 8 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn3, ComponentCount = 4, Offset = sizeof(float) * 12 },
    ];

    public Dictionary<string, InstanceAttribute> InstanceAttributes { get; } = [];

    public int InstanceCount { get; private set; }

    public InstancedGeometry(Geometry source)
    {
        PrimitiveType = source.PrimitiveType;
        Indices = new List<uint>(source.Indices);
        VertexAttributes = source.VertexAttributes.ToDictionary(
            kv => kv.Key,
            kv => new VertexAttribute
            {
                Name = kv.Value.Name,
                Location = kv.Value.Location,
                Size = kv.Value.Size,
                Data = new List<float>(kv.Value.Data),
                Enabled = kv.Value.Enabled
            });
    }

    public unsafe int AddInstance(Matrix4x4 transform)
    {
        EnsureDefaultAttributes();

        var transformAttr = InstanceAttributes["InstanceTransform"];
        var normalAttr = InstanceAttributes["InstanceNormalTransform"];

        float* p = (float*)&transform;
        for (int i = 0; i < 16; i++)
            transformAttr.Data.Add(p[i]);

        Matrix4x4.Invert(transform, out var inverseTransform);
        var normalMatrix = Matrix4x4.Transpose(inverseTransform);
        p = (float*)&normalMatrix;
        for (int i = 0; i < 16; i++)
            normalAttr.Data.Add(p[i]);

        InstanceCount++;
        NeedsUpload = true;
        return InstanceCount - 1;
    }

    public void RemoveInstance(int index)
    {
        foreach (var attr in InstanceAttributes.Values)
        {
            int floatsPerInstance = attr.Stride / sizeof(float);
            attr.Data.RemoveRange(index * floatsPerInstance, floatsPerInstance);
        }

        InstanceCount--;
        NeedsUpload = true;
    }

    public unsafe void UpdateInstance(int index, Matrix4x4 transform)
    {
        EnsureDefaultAttributes();

        var transformAttr = InstanceAttributes["InstanceTransform"];
        var normalAttr = InstanceAttributes["InstanceNormalTransform"];

        int baseIndex = index * 16;

        float* p = (float*)&transform;
        for (int i = 0; i < 16; i++)
            transformAttr.Data[baseIndex + i] = p[i];

        Matrix4x4.Invert(transform, out var inverseTransform);
        var normalMatrix = Matrix4x4.Transpose(inverseTransform);
        p = (float*)&normalMatrix;
        for (int i = 0; i < 16; i++)
            normalAttr.Data[baseIndex + i] = p[i];

        NeedsUpload = true;
    }

    public unsafe Matrix4x4? GetInstanceTransform(int index)
    {
        if (!InstanceAttributes.TryGetValue("InstanceTransform", out var attr))
            return null;

        int baseIdx = index * 16;
        if (baseIdx < 0 || baseIdx + 15 >= attr.Data.Count)
            return null;

        var m = new Matrix4x4();
        float* p = (float*)&m;
        for (int i = 0; i < 16; i++)
            p[i] = attr.Data[baseIdx + i];

        return m;
    }

    public void SetAttributeEnabled(string name, bool enabled)
    {
        if (InstanceAttributes.TryGetValue(name, out var attr))
        {
            attr.Enabled = enabled;
            NeedsUpload = true;
        }
    }

    public unsafe void SetInstanceAttribute<T>(BuildInVertexAttribute attribute, int componentCount, IReadOnlyList<T> data)
        where T : unmanaged
    {
        if (data.Count != InstanceCount)
            throw new ArgumentException($"数据数量 ({data.Count}) 与实例数量 ({InstanceCount}) 不一致。");

        var name = attribute.ToString();
        int elementSize = sizeof(T) / sizeof(float);
        int stride = componentCount * sizeof(float);
        var floatData = new List<float>(data.Count * componentCount);

        foreach (var item in data)
        {
            float* ptr = (float*)&item;
            for (int i = 0; i < componentCount; i++)
            {
                floatData.Add(i < elementSize ? ptr[i] : 0f);
            }
        }

        InstanceAttributes[name] = new InstanceAttribute
        {
            Name = name,
            Stride = stride,
            Data = floatData,
            Pointers =
            [
                new() { Location = (uint)attribute, ComponentCount = componentCount, Offset = 0 }
            ]
        };

        NeedsUpload = true;
    }

    public unsafe void SetInstances(IReadOnlyList<Matrix4x4> transforms)
    {
        EnsureDefaultAttributes();

        var transformAttr = InstanceAttributes["InstanceTransform"];
        var normalAttr = InstanceAttributes["InstanceNormalTransform"];

        transformAttr.Data.Clear();
        normalAttr.Data.Clear();

        int count = transforms.Count;
        transformAttr.Data.Capacity = count * 16;
        normalAttr.Data.Capacity = count * 16;

        for (int i = 0; i < count; i++)
        {
            var t = transforms[i];

            float* p = (float*)&t;
            for (int j = 0; j < 16; j++)
                transformAttr.Data.Add(p[j]);

            Matrix4x4.Invert(t, out var inv);
            var normalMatrix = Matrix4x4.Transpose(inv);
            p = (float*)&normalMatrix;
            for (int j = 0; j < 16; j++)
                normalAttr.Data.Add(p[j]);
        }

        InstanceCount = count;
        NeedsUpload = true;
    }

    private void EnsureDefaultAttributes()
    {
        if (!InstanceAttributes.ContainsKey("InstanceTransform"))
        {
            InstanceAttributes["InstanceTransform"] = new InstanceAttribute
            {
                Name = "InstanceTransform",
                Stride = 16 * sizeof(float),
                Pointers = new List<InstanceAttributePointer>(DefaultTransformPointers)
            };
        }

        if (!InstanceAttributes.ContainsKey("InstanceNormalTransform"))
        {
            InstanceAttributes["InstanceNormalTransform"] = new InstanceAttribute
            {
                Name = "InstanceNormalTransform",
                Stride = 16 * sizeof(float),
                Pointers = new List<InstanceAttributePointer>(DefaultNormalTransformPointers)
            };
        }
    }
}
