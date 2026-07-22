using Aura3D.Core.Math;
using System.Linq;
using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the geometry type.
/// </summary>
public class Geometry : IClone<Geometry>, IVersionedResource
{
    private List<uint> _indices = [];

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public ulong Version { get; protected set; } = 1;

    /// <summary>
    /// Marks the modified.
    /// </summary>
    protected void MarkModified()
    {
        Version++;
    }

    internal Dictionary<string, VertexAttribute> VertexAttributes { get; private protected set; } = new();

    private BoundingBox? boundingBox;

    /// <summary>
    /// Gets the bounding box.
    /// </summary>
    public BoundingBox? BoundingBox
    {
        get
        {
            if (boundingBox == null)
                CalcBoundingBox();
            return boundingBox;
        }
    }

    /// <summary>
    /// Gets the indices.
    /// </summary>
    public IReadOnlyList<uint> Indices => _indices.AsReadOnly();

    /// <summary>
    /// Gets the indices count.
    /// </summary>
    public int IndicesCount => Indices.Count;

    /// <summary>
    /// Gets the vertex count.
    /// </summary>
    public int VertexCount
    {
        get
        {
            if (VertexAttributes.TryGetValue("Position", out var attr))
                return attr.Data.Count / attr.Size;
            return 0;
        }
    }

    /// <summary>
    /// Gets the primitive type.
    /// </summary>
    private PrimitiveType _primitiveType = PrimitiveType.Triangles;
    /// <summary>
    /// Gets the primitive type.
    /// </summary>
    public PrimitiveType PrimitiveType
    {
        get => _primitiveType;
        set
        {
            if (_primitiveType == value)
                return;
            _primitiveType = value;
            MarkModified();
        }
    }

    /// <summary>
    /// Sets the vertex attribute.
    /// </summary>
    public void SetVertexAttribute(string name, uint location, int size, List<float> data)
    {
        if (data.Count % size != 0)
            throw Aura3D.Core.Exceptions.ResourceErrors.VertexAttributeLengthMismatch(data.Count, size);

        if (VertexAttributes.TryGetValue(name, out var vertexAttribute))
        {
            VertexAttributes.Remove(name);
        }

        VertexAttributes.Add(name, new VertexAttribute
        {
            Name = name,
            Location = location,
            Size = size,
            Data = data,
            Enabled = (location <= 7)
        });

        MarkModified();

        // Position 属性变更时清空局部包围盒缓存，下次访问时重建
        if (name == BuildInVertexAttribute.Position.ToString())
            boundingBox = null;
    }

    /// <summary>
    /// Sets the vertex attribute.
    /// </summary>
    public void SetVertexAttribute(BuildInVertexAttribute attribute, uint size, List<float> data)
    {
        SetVertexAttribute(attribute.ToString(), (uint)attribute, (int)size, data);
    }

    /// <summary>
    /// Sets the indices.
    /// </summary>
    public void SetIndices(IReadOnlyList<uint> indices)
    {
        SetIndicesBuffer(new List<uint>(indices));
        MarkModified();
    }

    internal List<uint> GetIndicesBuffer() => _indices;

    private protected void SetIndicesBuffer(List<uint> indices)
    {
        _indices = indices;
    }

    /// <summary>
    /// Gets the attribute data.
    /// </summary>
    public IReadOnlyList<float>? GetAttributeData(string name)
    {
        if (!VertexAttributes.ContainsKey(name))
            return null;
        return VertexAttributes[name].Data.AsReadOnly();
    }

    /// <summary>
    /// Gets the attribute data.
    /// </summary>
    public IReadOnlyList<float>? GetAttributeData(BuildInVertexAttribute attribute)
    {
        return GetAttributeData(attribute.ToString());
    }

    /// <summary>
    /// Performs the calc bounding box operation.
    /// </summary>
    private void CalcBoundingBox()
    {
        var positionData = GetAttributeData(BuildInVertexAttribute.Position);
        if (positionData == null || positionData.Count < 3)
        {
            boundingBox = null;
            return;
        }

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        for (int i = 0; i + 2 < positionData.Count; i += 3)
        {
            var v = new Vector3(positionData[i], positionData[i + 1], positionData[i + 2]);
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        boundingBox = new BoundingBox(min, max);
    }

    /// <summary>
    /// Sets the attribute enabled.
    /// </summary>
    public void SetAttributeEnabled(BuildInVertexAttribute attribute, bool enabled)
    {
        var name = attribute.ToString();
        if (VertexAttributes.TryGetValue(name, out var attr))
        {
            attr.Enabled = enabled;
            VertexAttributes[name] = attr;
            MarkModified();
        }
    }

    /// <summary>
    /// Clones the associated data.
    /// </summary>
    public Geometry Clone()
    {
        var geometry = new Geometry
        {
            VertexAttributes = VertexAttributes,
            PrimitiveType = PrimitiveType
        };
        geometry.SetIndicesBuffer(_indices);
        return geometry;
    }

    /// <summary>
    /// Deep-clones the associated data.
    /// </summary>
    public Geometry DeepClone()
    {
        var geometry = new Geometry
        {
            VertexAttributes = VertexAttributes.ToDictionary(
                kv => kv.Key,
                kv => new VertexAttribute
                {
                    Name = kv.Value.Name,
                    Location = kv.Value.Location,
                    Size = kv.Value.Size,
                    Data = new List<float>(kv.Value.Data),
                    Enabled = kv.Value.Enabled
                }),
            PrimitiveType = PrimitiveType
        };
        geometry.SetIndicesBuffer(new List<uint>(_indices));
        return geometry;
    }
}


/// <summary>
/// Represents the vertex attribute type.
/// </summary>
public struct VertexAttribute
{
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name;
    /// <summary>
    /// Gets the location.
    /// </summary>
    public uint Location;
    /// <summary>
    /// Gets the size.
    /// </summary>
    public int Size;
    /// <summary>
    /// Gets the data.
    /// </summary>
    public List<float> Data;
    /// <summary>
    /// Gets the enabled.
    /// </summary>
    public bool Enabled;
}

/// <summary>
/// Represents the instance attribute pointer type.
/// </summary>
public struct InstanceAttributePointer
{
    /// <summary>
    /// Gets the location.
    /// </summary>
    public uint Location;
    /// <summary>
    /// Gets the component count.
    /// </summary>
    public int ComponentCount;
    /// <summary>
    /// Gets the offset.
    /// </summary>
    public int Offset;
}

/// <summary>
/// Represents the instance attribute type.
/// </summary>
public class InstanceAttribute
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Gets the data.
    /// </summary>
    public IReadOnlyList<float> Data => DataBuffer.AsReadOnly();
    internal List<float> DataBuffer { get; set; } = new();
    /// <summary>
    /// Gets or sets the stride.
    /// </summary>
    public int Stride { get; init; }
    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    public bool Enabled { get; internal set; } = true;
    /// <summary>
    /// Gets the pointers.
    /// </summary>
    public IReadOnlyList<InstanceAttributePointer> Pointers => PointersBuffer.AsReadOnly();
    internal List<InstanceAttributePointer> PointersBuffer { get; set; } = new();
}

/// <summary>
/// Specifies values for build in vertex attribute.
/// </summary>
public enum BuildInVertexAttribute
{
    /// <summary>
    /// Gets the position.
    /// </summary>
    Position = 0,
    /// <summary>
    /// Gets the tex coord 0.
    /// </summary>
    TexCoord_0 = 1,
    /// <summary>
    /// Gets the color 0.
    /// </summary>
    Color_0 = 2,
    /// <summary>
    /// Gets the normal.
    /// </summary>
    Normal = 3,
    /// <summary>
    /// Gets the tangent.
    /// </summary>
    Tangent = 4,
    /// <summary>
    /// Gets the bitangent.
    /// </summary>
    Bitangent = 5,
    /// <summary>
    /// Gets the joints 0.
    /// </summary>
    Joints_0 = 6,
    /// <summary>
    /// Gets the weights 0.
    /// </summary>
    Weights_0 = 7,

    /// <summary>
    /// Gets the instanced transform column0.
    /// </summary>
    InstancedTransformColumn0 = 8,
    /// <summary>
    /// Gets the instanced transform column1.
    /// </summary>
    InstancedTransformColumn1 = 9,
    /// <summary>
    /// Gets the instanced transform column2.
    /// </summary>
    InstancedTransformColumn2 = 10,
    /// <summary>
    /// Gets the instanced transform column3.
    /// </summary>
    InstancedTransformColumn3 = 11,

    /// <summary>
    /// Gets the instanced normal transform column0.
    /// </summary>
    InstancedNormalTransformColumn0 = 12,
    /// <summary>
    /// Gets the instanced normal transform column1.
    /// </summary>
    InstancedNormalTransformColumn1 = 13,
    /// <summary>
    /// Gets the instanced normal transform column2.
    /// </summary>
    InstancedNormalTransformColumn2 = 14,
    /// <summary>
    /// Gets the instanced normal transform column3.
    /// </summary>
    InstancedNormalTransformColumn3 = 15,

    /// <summary>
    /// Gets the tex coord 1.
    /// </summary>
    TexCoord_1 = 16,
    /// <summary>
    /// Gets the tex coord 2.
    /// </summary>
    TexCoord_2 = 17,
    /// <summary>
    /// Gets the tex coord 3.
    /// </summary>
    TexCoord_3 = 18,
    /// <summary>
    /// Gets the joints 1.
    /// </summary>
    Joints_1 = 19,
    /// <summary>
    /// Gets the weights 1.
    /// </summary>
    Weights_1 = 20,
}

/// <summary>
/// Specifies values for primitive type.
/// </summary>
public enum PrimitiveType
{
    /// <summary>
    /// Specifies triangles.
    /// </summary>
    Triangles,
    /// <summary>
    /// Specifies points.
    /// </summary>
    Points,
    /// <summary>
    /// Specifies lines.
    /// </summary>
    Lines,
    /// <summary>
    /// Specifies line strip.
    /// </summary>
    LineStrip,
    /// <summary>
    /// Specifies line loop.
    /// </summary>
    LineLoop,
    /// <summary>
    /// Specifies triangle strip.
    /// </summary>
    TriangleStrip,
    /// <summary>
    /// Specifies triangle fan.
    /// </summary>
    TriangleFan,
}
