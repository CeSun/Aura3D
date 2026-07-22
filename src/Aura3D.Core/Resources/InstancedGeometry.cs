using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the instanced geometry type.
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

    private readonly Dictionary<string, InstanceAttribute> instanceAttributes = [];

    /// <summary>
    /// Gets the instance attributes.
    /// </summary>
    public IReadOnlyDictionary<string, InstanceAttribute> InstanceAttributes => instanceAttributes;

    /// <summary>
    /// Gets or sets the instance count.
    /// </summary>
    public int InstanceCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of the instanced geometry type.
    /// </summary>
    public InstancedGeometry(Geometry source)
    {
        PrimitiveType = source.PrimitiveType;
        SetIndicesBuffer(new List<uint>(source.Indices));
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

    /// <summary>
    /// Adds the instance.
    /// </summary>
    public unsafe int AddInstance(Matrix4x4 transform)
    {
        EnsureDefaultAttributes();

        var transformAttr = instanceAttributes["InstanceTransform"];
        var normalAttr = instanceAttributes["InstanceNormalTransform"];

        float* p = (float*)&transform;
        for (int i = 0; i < 16; i++)
            transformAttr.DataBuffer.Add(p[i]);

        Matrix4x4.Invert(transform, out var inverseTransform);
        var normalMatrix = Matrix4x4.Transpose(inverseTransform);
        p = (float*)&normalMatrix;
        for (int i = 0; i < 16; i++)
            normalAttr.DataBuffer.Add(p[i]);

        InstanceCount++;
        MarkModified();
        return InstanceCount - 1;
    }

    /// <summary>
    /// Removes the instance.
    /// </summary>
    public void RemoveInstance(int index)
    {
        foreach (var attr in instanceAttributes.Values)
        {
            int floatsPerInstance = attr.Stride / sizeof(float);
            attr.DataBuffer.RemoveRange(index * floatsPerInstance, floatsPerInstance);
        }

        InstanceCount--;
        MarkModified();
    }

    /// <summary>
    /// Updates the instance.
    /// </summary>
    public unsafe void UpdateInstance(int index, Matrix4x4 transform)
    {
        EnsureDefaultAttributes();

        var transformAttr = instanceAttributes["InstanceTransform"];
        var normalAttr = instanceAttributes["InstanceNormalTransform"];

        int baseIndex = index * 16;

        float* p = (float*)&transform;
        for (int i = 0; i < 16; i++)
            transformAttr.DataBuffer[baseIndex + i] = p[i];

        Matrix4x4.Invert(transform, out var inverseTransform);
        var normalMatrix = Matrix4x4.Transpose(inverseTransform);
        p = (float*)&normalMatrix;
        for (int i = 0; i < 16; i++)
            normalAttr.DataBuffer[baseIndex + i] = p[i];

        MarkModified();
    }

    /// <summary>
    /// Gets the instance transform.
    /// </summary>
    public unsafe Matrix4x4? GetInstanceTransform(int index)
    {
        if (!instanceAttributes.TryGetValue("InstanceTransform", out var attr))
            return null;

        int baseIdx = index * 16;
        if (baseIdx < 0 || baseIdx + 15 >= attr.DataBuffer.Count)
            return null;

        var m = new Matrix4x4();
        float* p = (float*)&m;
        for (int i = 0; i < 16; i++)
            p[i] = attr.DataBuffer[baseIdx + i];

        return m;
    }

    /// <summary>
    /// Sets the attribute enabled.
    /// </summary>
    public void SetAttributeEnabled(string name, bool enabled)
    {
        if (instanceAttributes.TryGetValue(name, out var attr))
        {
            attr.Enabled = enabled;
            MarkModified();
        }
    }

    /// <summary>
    /// Sets the instance attribute.
    /// </summary>
    public unsafe void SetInstanceAttribute<T>(BuildInVertexAttribute attribute, int componentCount, IReadOnlyList<T> data)
        where T : unmanaged
    {
        if (data.Count != InstanceCount)
            throw Aura3D.Core.Exceptions.ResourceErrors.InstanceAttributeCountMismatch(data.Count, InstanceCount);

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

        instanceAttributes[name] = new InstanceAttribute
        {
            Name = name,
            Stride = stride,
            DataBuffer = floatData,
            PointersBuffer =
            [
                new() { Location = (uint)attribute, ComponentCount = componentCount, Offset = 0 }
            ]
        };

        MarkModified();
    }

    /// <summary>
    /// Sets the instances.
    /// </summary>
    public unsafe void SetInstances(IReadOnlyList<Matrix4x4> transforms)
    {
        EnsureDefaultAttributes();

        var transformAttr = instanceAttributes["InstanceTransform"];
        var normalAttr = instanceAttributes["InstanceNormalTransform"];

        transformAttr.DataBuffer.Clear();
        normalAttr.DataBuffer.Clear();

        int count = transforms.Count;
        transformAttr.DataBuffer.Capacity = count * 16;
        normalAttr.DataBuffer.Capacity = count * 16;

        for (int i = 0; i < count; i++)
        {
            var t = transforms[i];

            float* p = (float*)&t;
            for (int j = 0; j < 16; j++)
                transformAttr.DataBuffer.Add(p[j]);

            Matrix4x4.Invert(t, out var inv);
            var normalMatrix = Matrix4x4.Transpose(inv);
            p = (float*)&normalMatrix;
            for (int j = 0; j < 16; j++)
                normalAttr.DataBuffer.Add(p[j]);
        }

        InstanceCount = count;
        MarkModified();
    }

    private void EnsureDefaultAttributes()
    {
        if (!instanceAttributes.ContainsKey("InstanceTransform"))
        {
            instanceAttributes["InstanceTransform"] = new InstanceAttribute
            {
                Name = "InstanceTransform",
                Stride = 16 * sizeof(float),
                PointersBuffer = new List<InstanceAttributePointer>(DefaultTransformPointers)
            };
        }

        if (!instanceAttributes.ContainsKey("InstanceNormalTransform"))
        {
            instanceAttributes["InstanceNormalTransform"] = new InstanceAttribute
            {
                Name = "InstanceNormalTransform",
                Stride = 16 * sizeof(float),
                PointersBuffer = new List<InstanceAttributePointer>(DefaultNormalTransformPointers)
            };
        }
    }
}
