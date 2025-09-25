using Silk.NET.OpenGLES;
using System.Linq;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

public class Geometry : IGpuResource, IClone<Geometry>
{

    public bool NeedsUpload { get; set; } = true;

    protected Dictionary<string, VertexAttribute> VertexAttributes = new();

    public List<uint> Indices { get; protected set; } = [];

    protected HashSet<uint> VertexAttributeLocations = new();

    protected List<uint> VboIds = new();

    public int IndicesCount => Indices.Count;

    public uint Vao;

    public uint Ebo;

    public void SetVertexAttribute(string name, uint location, int size, List<float> data)
    {
        if (data.Count % size != 0)
            throw new Exception($"The length of vertex attribute data must be a multiple of its size. Data length: {data.Count}, Size: {size}");

        if (VertexAttributes.TryGetValue(name, out var vertexAttribute))
        {
            VertexAttributes.Remove(name);
            VertexAttributeLocations.Remove(vertexAttribute.Location);
        }

        VertexAttributes.Add(name, new VertexAttribute
        {
            Name = name,
            Location = location,
            Size = size,
            Data = data
        });
        VertexAttributeLocations.Add(location);
    }

    public void SetIndices(List<uint> indices)
    {
        Indices = indices;
    }

    public List<float>? GetAttributeData(string name)
    {
        if (!VertexAttributes.ContainsKey(name))
            return null;
        return VertexAttributes[name].Data;
    }

    public void Destroy(GL gl)
    {
        foreach (var vbo in VboIds)
        {
            gl.DeleteBuffer(vbo);
        }
        VboIds.Clear();
        if (Ebo != 0)
        {
            gl.DeleteBuffer(Ebo);
            Ebo = 0;
        }
        if (Vao != 0)
        {
            gl.DeleteVertexArray(Vao);
            Vao = 0;
        }
    }

    public unsafe void Upload(GL gl)
    {
        Vao = gl.GenVertexArray();

        gl.BindVertexArray(Vao);

        foreach (var(_, attribute) in VertexAttributes)
        {
            uint vbo = gl.GenBuffer();
            VboIds.Add(vbo);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            unsafe
            {
                fixed (float* dataPtr = CollectionsMarshal.AsSpan(attribute.Data))
                {
                    gl.BufferData(GLEnum.ArrayBuffer, (nuint)(attribute.Data.Count * sizeof(float)), dataPtr, GLEnum.StaticDraw);
                }
            }
            gl.EnableVertexAttribArray(attribute.Location);
            gl.VertexAttribPointer(attribute.Location, attribute.Size, GLEnum.Float, false, (uint)(sizeof(float) * attribute.Size), (void*)0);
        }

        Ebo = gl.GenBuffer();
        gl.BindBuffer(GLEnum.ElementArrayBuffer, Ebo);

        fixed (uint* indexPtr = CollectionsMarshal.AsSpan(Indices))
        {
            // 上传索引数据到 GPU
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(Indices.Count * sizeof(uint)), indexPtr, GLEnum.StaticDraw);
        }


    }

    public Geometry Clone()
    {
        return new Geometry
        {
            Indices = Indices,
            VertexAttributes = VertexAttributes,
            VertexAttributeLocations = VertexAttributeLocations
        };
    }

    public Geometry DeepClone()
    {
        return new Geometry
        {
            Indices = new List<uint>(Indices),
            VertexAttributes = VertexAttributes.ToDictionary(),
            VertexAttributeLocations = new HashSet<uint>(VertexAttributeLocations)
        };
    }
}


public struct VertexAttribute
{
    public string Name;
    public uint Location;
    public int Size;
    public List<float> Data;
}